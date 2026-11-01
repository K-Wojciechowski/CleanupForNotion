using System.Globalization;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging.Testing;
using Notion.Client;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Plugins.Implementations;

[TestClass]
public class EstimateExpensesWithSubscriptionsTests {
  private const string DataSourceId = "dataSourceId";
  private const string PropertyName = "estimatedTotal";
  private const string OutputPageId = "outputPageId";
  private const string VendorPropertyName = "vendor";
  private const string AmountPropertyName = "amount";
  private const string PluginDescription = "pluginDescription";
  private static readonly DateTime _lastEditedTime = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
  private static readonly DateTimeOffset _lastEdited = new(_lastEditedTime);

  private static readonly string _lastEditedString =
      _lastEdited.ToString("o", CultureInfo.InvariantCulture);

  private static readonly ExpenseSubscription[] _subscriptions = [
      new() { Names = ["Sub1"], ExpectedTotal = 300 },
      new() { Names = ["Sub2", "2 Sub"], ExpectedTotal = 1500 }
  ];

  [TestMethod]
  public void Name_Get_ReturnsEstimateExpensesWithSubscriptions() {
    CreatePlugin(Substitute.For<IPluginStateProvider>()).Name.ShouldBe("EstimateExpensesWithSubscriptions");
  }

  [TestMethod]
  public async Task Run_ExpensesExist_UpdatesTotalAndSavesQueryState() {
    var pages = new List<Page> {
        CreatePage("subscription1", ["Sub1"], 5),
        CreatePage("subscription2", ["  SUB", "2 "], 8.15),
        CreatePage("subscription3", ["2 sub"], 4.05),
        CreatePage("not sub", ["2sub"], 0.14),
        CreatePage("other", ["unconfigured"], 1.234)
    };
    var client = CreateClient(pages);
    var stateProvider = Substitute.For<IPluginStateProvider>();
    stateProvider.GetString(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), CancellationToken.None)
        .Returns((string?)null);
    var plugin = CreatePlugin(stateProvider);

    await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    var update = client.ReceivedCalls()
        .Single(call => call.GetMethodInfo().Name == "UpdatePageAsync")
        .GetArguments();
    update[0].ShouldBe(OutputPageId);
    var parameters = update[1].ShouldBeOfType<PagesUpdateParameters>();
    parameters.Properties.ShouldContainKey(PropertyName);
    parameters.Properties[PropertyName].ShouldBeOfType<NumberPropertyValue>().Number.ShouldBe(21.37);

    await stateProvider.Received().SetString(
            plugin.Name, PluginDescription, "LastEdit", _lastEditedString, CancellationToken.None)
        .ConfigureAwait(false);
    await stateProvider.Received().SetString(
            plugin.Name, PluginDescription, "PageCount", pages.Count.ToString(CultureInfo.InvariantCulture), CancellationToken.None)
        .ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_QueryStateUnchanged_DoesNotUpdateOutputOrState() {
    var client = CreateClient([CreatePage("subscription", ["sub1"], 8.15)]);
    var stateProvider = Substitute.For<IPluginStateProvider>();
    stateProvider.GetString(Arg.Any<string>(), Arg.Any<string>(), "PageCount", CancellationToken.None)
        .Returns("1");
    stateProvider.GetString(Arg.Any<string>(), Arg.Any<string>(), "LastEdit", CancellationToken.None)
        .Returns("2025-01-02T03:04:05.0000000+00:00");
    var plugin = CreatePlugin(stateProvider);

    await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    await client.DidNotReceiveWithAnyArgs()
        .UpdatePageAsync(Arg.Any<string>(), Arg.Any<PagesUpdateParameters>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);
    await stateProvider.DidNotReceiveWithAnyArgs()
        .SetString(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<CancellationToken>())
        .ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_PageCountChanged_RecalculatesAndSavesState() {
    var stateProvider = SubstituteState("2", _lastEditedString);
    var plugin = CreatePlugin(stateProvider);
    var client = CreateClient([CreatePage("subscription", ["Sub1"], 1.23)]);

    await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    GetUpdatedTotal(client).ShouldBe(18);
    await stateProvider.Received().SetString(
        plugin.Name, PluginDescription, "PageCount", "1", CancellationToken.None).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_LastEditChanged_RecalculatesAndSavesState() {
    const string previousLastEdited = "2025-01-02T03:04:04.0000000+00:00";
    var stateProvider = SubstituteState("1", previousLastEdited);
    var plugin = CreatePlugin(stateProvider);
    var client = CreateClient([CreatePage("subscription", ["sub1 "], 8.15)]);

    await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    GetUpdatedTotal(client).ShouldBe(23.15);
    await stateProvider.Received().SetString(
        plugin.Name, PluginDescription, "LastEdit", _lastEditedString, CancellationToken.None).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_NoPages_UpdatesToExpectedSubscriptionMinimumsAndSavesPlaceholderState() {
    var stateProvider = SubstituteState(null, null);
    var plugin = CreatePlugin(stateProvider);
    var client = CreateClient([]);

    await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    GetUpdatedTotal(client).ShouldBe(18);
    await stateProvider.Received().SetString(
            plugin.Name, PluginDescription, "LastEdit", "2000-01-01T00:00:00.0000000+00:00", CancellationToken.None)
        .ConfigureAwait(false);
    await stateProvider.Received().SetString(
        plugin.Name, PluginDescription, "PageCount", "0", CancellationToken.None).ConfigureAwait(false);
  }

  private static EstimateExpensesWithSubscriptions CreatePlugin(IPluginStateProvider stateProvider) => new(
      new FakeLogger<EstimateExpensesWithSubscriptions>(),
      stateProvider,
      new EstimateExpensesWithSubscriptionsOptions(
          DataSourceId,
          PropertyName,
          OutputPageId,
          VendorPropertyName,
          AmountPropertyName,
          _subscriptions,
          CurrencyDecimalPlaces: 2),
      PluginDescription);

  private static ICfnNotionClient CreateClient(List<Page> pages) {
    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDataSourceRawAsync(Arg.Any<QueryDataSourceRequest>(), CancellationToken.None)
        .Returns(new QueryDataSourceResponse {
            Results = pages.Cast<IQueryDataSourceResponseObject>().ToList(), HasMore = false
        });
    return client;
  }

  private static IPluginStateProvider SubstituteState(string? pageCount, string? lastEdit) {
    var stateProvider = Substitute.For<IPluginStateProvider>();
    stateProvider.GetString(Arg.Any<string>(), Arg.Any<string>(), "PageCount", CancellationToken.None)
        .Returns(pageCount);
    stateProvider.GetString(Arg.Any<string>(), Arg.Any<string>(), "LastEdit", CancellationToken.None)
        .Returns(lastEdit);
    return stateProvider;
  }

  private static double? GetUpdatedTotal(ICfnNotionClient client) => client.ReceivedCalls()
      .Single(call => call.GetMethodInfo().Name == "UpdatePageAsync")
      .GetArguments()[1]
      .ShouldBeOfType<PagesUpdateParameters>()
      .Properties[PropertyName]
      .ShouldBeOfType<NumberPropertyValue>()
      .Number;

  private static Page CreatePage(string id, string[] nameParts, double amount) => new() {
      Id = id,
      LastEditedTime = _lastEditedTime,
      Properties = new Dictionary<string, PropertyValue> {
          {
              VendorPropertyName,
              new TitlePropertyValue {
                  Title = nameParts.Select(part => (RichTextBase)new RichTextText { PlainText = part }).ToList()
              }
          },
          { AmountPropertyName, new NumberPropertyValue { Number = amount } }
      }
  };
}
