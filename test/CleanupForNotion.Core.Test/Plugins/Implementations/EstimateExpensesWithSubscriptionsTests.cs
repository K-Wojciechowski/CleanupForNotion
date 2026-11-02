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
  private const string ExpensesDataSourceId = "expensesDataSourceId";
  private const string PropertyName = "estimatedTotal";
  private const string VendorPropertyName = "vendor";
  private const string AmountPropertyName = "amount";
  private const string AlternateVendorPropertyName = "alternateVendor";
  private const string PluginDescription = "pluginDescription";
  private static readonly DateTime _lastEditedTime = new(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
  private static readonly DateTimeOffset _lastEdited = new(_lastEditedTime);

  private static readonly string _lastEditedString =
      _lastEdited.ToString("o", CultureInfo.InvariantCulture);

  [TestMethod]
  public void Name_Get_ReturnsEstimateExpensesWithSubscriptions() {
    CreatePlugin(Substitute.For<IPluginStateProvider>()).Name.ShouldBe("EstimateExpensesWithSubscriptions");
  }

  [TestMethod]
  public async Task Run_ExpensesExist_UpdatesTotalAndSavesQueryState() {
    var expensePages = new List<Page> {
        CreatePage("expense1", " Sub1 ", 5),
        CreatePage("expense2", "  SUB2 ", 8.15),
        CreatePage("expense3", "2 sub", 9.05),
        CreatePage("not sub", "2sub", 0.14),
        CreatePage("other", "unconfigured", 1.234)
    };
    var subscriptionPages = new List<Page> {
        CreatePage("subscription1", "Sub1", 3),
        CreatePage("subscription2", "Sub2", 15, "2 Sub"),
        CreatePage("unassigned", "Unassigned", 0)
    };
    var client = CreateClient(subscriptionPages, expensePages);
    var stateProvider = Substitute.For<IPluginStateProvider>();
    stateProvider.GetString(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), CancellationToken.None)
        .Returns((string?)null);
    var plugin = CreatePlugin(stateProvider);

    await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    GetUpdatedTotal(client, "subscription1").ShouldBe(5);
    GetUpdatedTotal(client, "subscription2").ShouldBe(17.2);
    GetUpdatedTotal(client, "unassigned").ShouldBe(1.37);

    await stateProvider.Received().SetString(
            plugin.Name, PluginDescription, "LastEdit", _lastEditedString, CancellationToken.None)
        .ConfigureAwait(false);
    await stateProvider.Received().SetString(
            plugin.Name, PluginDescription, "PageCount", expensePages.Count.ToString(CultureInfo.InvariantCulture),
            CancellationToken.None)
        .ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_QueryStateUnchanged_DoesNotUpdateOutputOrState() {
    var client = CreateClient([], [CreatePage("expense", "sub1", 8.15)]);
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
    var client = CreateClient(
        [CreatePage("subscription", "Sub1", 18)],
        [CreatePage("expense", "Sub1", 1.23)]);

    await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    GetUpdatedTotal(client, "subscription").ShouldBe(18);
    await stateProvider.Received().SetString(
        plugin.Name, PluginDescription, "PageCount", "1", CancellationToken.None).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_LastEditChanged_RecalculatesAndSavesState() {
    const string previousLastEdited = "2025-01-02T03:04:04.0000000+00:00";
    var stateProvider = SubstituteState("1", previousLastEdited);
    var plugin = CreatePlugin(stateProvider);
    var client = CreateClient(
        [
            CreatePage("subscription", "Sub1", 3),
            CreatePage("blank", "blank", null),
        ],
        [
            new Page {
                Id = "expense",
                LastEditedTime = _lastEditedTime,
                Properties = new Dictionary<string, PropertyValue> {
                    {
                        VendorPropertyName,
                        new RichTextPropertyValue { RichText = [new RichTextText { PlainText = "sub1 " }] }
                    },
                    { AmountPropertyName, new NumberPropertyValue { Number = 8.15 } }
                }
            }
        ]);

    await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    GetUpdatedTotal(client, "subscription").ShouldBe(8.15);
    GetUpdatedTotal(client, "blank").ShouldBe(0);
    await stateProvider.Received().SetString(
        plugin.Name, PluginDescription, "LastEdit", _lastEditedString, CancellationToken.None).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_NoExpenses_UpdatesToExpectedSubscriptionMinimumsAndSavesPlaceholderState() {
    var stateProvider = SubstituteState(null, null);
    var plugin = CreatePlugin(stateProvider);
    var client = CreateClient(
        [CreatePage("subscription1", "Sub1", 3), CreatePage("subscription2", "Sub2", 15, "2 Sub")],
        []);

    await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    GetUpdatedTotal(client, "subscription1").ShouldBe(3);
    GetUpdatedTotal(client, "subscription2").ShouldBe(15);
    await stateProvider.Received().SetString(
            plugin.Name, PluginDescription, "LastEdit", "2000-01-01T00:00:00.0000000+00:00", CancellationToken.None)
        .ConfigureAwait(false);
    await stateProvider.Received().SetString(
        plugin.Name, PluginDescription, "PageCount", "0", CancellationToken.None).ConfigureAwait(false);
  }

  [TestMethod]
  [DataRow(true, DisplayName = "subscription bad")]
  [DataRow(false, DisplayName = "expense bad")]
  public async Task Run_AmountNotNumber_Throws(bool subscriptionBad) {
    var stateProvider = SubstituteState(null, null);
    var plugin = CreatePlugin(stateProvider);
    var goodPage = CreatePage("good", "Sub1", 3);

    var badPage = new Page {
        Id = "bad",
        LastEditedTime = _lastEditedTime,
        Properties = new Dictionary<string, PropertyValue> {
            { VendorPropertyName, new TitlePropertyValue { Title = [new RichTextText { PlainText = "sub1" }] } },
            { AmountPropertyName, new UrlPropertyValue { Url = "https://example.com/" } }
        }
    };

    var client = CreateClient(
        [subscriptionBad ? badPage : goodPage],
        [subscriptionBad ? goodPage : badPage]);

    Func<Task> act = async () =>
        await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    await act.ShouldThrowAsync<InvalidDataException>("Page bad does not have a readable amount").ConfigureAwait(false);
  }

  [TestMethod]
  [DataRow(true, DisplayName = "subscription bad")]
  [DataRow(false, DisplayName = "expense bad")]
  public async Task Run_VendorNotRichTextOrTitle_Throws(bool subscriptionBad) {
    var stateProvider = SubstituteState(null, null);
    var plugin = CreatePlugin(stateProvider);
    var goodPage = CreatePage("good", "Sub1", 3);

    var badPage = new Page {
        Id = "bad",
        LastEditedTime = _lastEditedTime,
        Properties = new Dictionary<string, PropertyValue> {
            { VendorPropertyName, new UrlPropertyValue { Url = "https://example.com/" } },
            { AmountPropertyName, new NumberPropertyValue { Number = 21.37 } }
        }
    };

    var client = CreateClient(
        [subscriptionBad ? badPage : goodPage],
        [subscriptionBad ? goodPage : badPage]);

    Func<Task> act = async () =>
        await plugin.Run(client, new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    await act.ShouldThrowAsync<InvalidDataException>("Page bad does not have a readable vendor").ConfigureAwait(false);
  }

  private static EstimateExpensesWithSubscriptions CreatePlugin(IPluginStateProvider stateProvider) => new(
      new FakeLogger<EstimateExpensesWithSubscriptions>(),
      stateProvider,
      new EstimateExpensesWithSubscriptionsOptions(
          DataSourceId: DataSourceId,
          PropertyName: PropertyName,
          ExpensesDataSourceId: ExpensesDataSourceId,
          VendorPropertyName: VendorPropertyName,
          AmountPropertyName: AmountPropertyName,
          AlternateVendorPropertyName: AlternateVendorPropertyName,
          CurrencyDecimalPlaces: 2),
      PluginDescription);

  private static ICfnNotionClient CreateClient(List<Page> subscriptionPages, List<Page> expensePages) {
    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDataSourceRawAsync(
            Arg.Is<QueryDataSourceRequest>(request => request.DataSourceId == DataSourceId),
            CancellationToken.None)
        .Returns(new QueryDataSourceResponse {
            Results = subscriptionPages.Cast<IQueryDataSourceResponseObject>().ToList(), HasMore = false
        });
    client.QueryDataSourceRawAsync(
            Arg.Is<QueryDataSourceRequest>(request => request.DataSourceId == ExpensesDataSourceId),
            CancellationToken.None)
        .Returns(new QueryDataSourceResponse {
            Results = expensePages.Cast<IQueryDataSourceResponseObject>().ToList(), HasMore = false
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

  private static double? GetUpdatedTotal(ICfnNotionClient client, string pageId) => client.ReceivedCalls()
      .Where(call => call.GetMethodInfo().Name == "UpdatePageAsync")
      .Select(call => call.GetArguments())
      .Single(arguments => (string)arguments[0]! == pageId)[1]
      .ShouldBeOfType<PagesUpdateParameters>()
      .Properties[PropertyName]
      .ShouldBeOfType<NumberPropertyValue>()
      .Number;

  private static Page CreatePage(string id, string vendor, double? amount, string alternateVendor = "") => new() {
      Id = id,
      LastEditedTime = _lastEditedTime,
      Properties = new Dictionary<string, PropertyValue> {
          { VendorPropertyName, new TitlePropertyValue { Title = [new RichTextText { PlainText = vendor }] } },
          { AmountPropertyName, new NumberPropertyValue { Number = amount } }, {
              AlternateVendorPropertyName,
              new TitlePropertyValue { Title = [new RichTextText { PlainText = alternateVendor }] }
          }
      }
  };
}
