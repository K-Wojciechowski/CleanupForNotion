using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Notion.Client;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace CleanupForNotion.Test.Plugins.Implementations;

[TestClass]
public class DeleteZeroSumTests {
  private const string DatabaseId = "DatabaseId";
  private const string PropertyName = "PropertyName";
  private const string PluginDescription = "PluginDescription";

  [TestMethod]
  public void Name_Get_ReturnsDeleteZeroSum() {
    new DeleteZeroSum(null!, null!, null!, null!, null!).Name.ShouldBe("DeleteZeroSum");
  }

  [TestMethod]
  public void Description_Get_ReturnsDescription() {
    var description = Guid.NewGuid().ToString();
    new DeleteZeroSum(null!, null!, null!, null!, description).Description.ShouldBe(description);
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public async Task Run_Called_DeletesEntriesThatSumToZero(bool dryRun) {
    // Arrange
    var pages = new List<Page> {
        new() { Id = "kx", Properties = CreateProperties(new CheckboxPropertyValue { Checkbox = true }, "OtherName") },
        new() { Id = "kc", Properties = CreateProperties(new CheckboxPropertyValue { Checkbox = true }) },
        new() { Id = "knn", Properties = CreateNumberProperties(null) },
        new() { Id = "knf", Properties = CreateFormulaNumberProperties(null) },
        new() { Id = "d0a", Properties = CreateNumberProperties(0) },
        new() { Id = "d0b", Properties = CreateFormulaNumberProperties(0) },
        new() { Id = "d0c", Properties = CreateNumberProperties(-0) },
        new() { Id = "d2a", Properties = CreateNumberProperties(2) },
        new() { Id = "d2b", Properties = CreateNumberProperties(2) },
        new() { Id = "d2y", Properties = CreateNumberProperties(-2) },
        new() { Id = "d2z", Properties = CreateNumberProperties(-2) },
        new() { Id = "k2a", Properties = CreateNumberProperties(2) },
        new() { Id = "k4a", Properties = CreateNumberProperties(4) },
        new() { Id = "k4b", Properties = CreateNumberProperties(4) },
        new() { Id = "k8b", Properties = CreateNumberProperties(-8) },
        new() { Id = "d5z", Properties = CreateNumberProperties(-0.5) },
        new() { Id = "d5a", Properties = CreateNumberProperties(0.5) }
    };
    var pageIdsToDelete = pages.Select(p => p.Id).Where(id => id.StartsWith('d')).ToList();

    List<string> expectedLogMessages = [
        $"Property '{PropertyName}' not found in page 'kx', skipping",
        $"Property '{PropertyName}' in page 'kc' does not have a numeric or formula value (found '{PropertyValueType.Checkbox}'), skipping",
        $"Property '{PropertyName}' in page 'knn' has a null numeric value, skipping",
        $"Property '{PropertyName}' in page 'knf' has a null formula value, skipping",
        "Found 3 pairs with a value of zero, removing",
        "Found 2 pairs in bucket '2', removing",
        "Found 1 pairs in bucket '0.5', removing",
        .. pageIdsToDelete.Select(id => $"Deleting page '{id}'")
    ];

    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDatabaseRawAsync(DatabaseId,
            Arg.Is((DatabasesQueryParameters d) => d.Filter.GetType() == typeof(TimestampLastEditedTimeFilter)),
            CancellationToken.None)
        .Returns(new DatabaseQueryResponse { Results = pages.Cast<IWikiDatabase>().ToList() });
    client.DeletePageAsync(Arg.Is((string s) => s.StartsWith('d')), CancellationToken.None)
        .Returns(Task.CompletedTask);
    client.DeletePageAsync(Arg.Is((string s) => !s.StartsWith('d')), CancellationToken.None)
        .Throws(new Exception("this should never happen"));

    var logger = new FakeLogger<DeleteZeroSum>();
    var timeProvider = new FakeTimeProvider();
    var options = new DeleteZeroSumOptions(DatabaseId: DatabaseId, PropertyName: PropertyName);

    var plugin = new DeleteZeroSum(logger, Substitute.For<IPluginStateProvider>(), timeProvider, options,
        PluginDescription);

    // Act
    await plugin.Run(client, new GlobalOptions { DryRun = dryRun }, CancellationToken.None).ConfigureAwait(false);

    // Assert
    var logRecords = logger.Collector.GetSnapshot();
    var logMessages = logRecords.Select(r => r.Message).ToList();
    expectedLogMessages.ShouldBeSubsetOf(logMessages);

    if (dryRun) {
      await client
          .DidNotReceiveWithAnyArgs()
          .DeletePageAsync(Arg.Any<string>(), CancellationToken.None)
          .ConfigureAwait(false);
      return;
    }

    foreach (var pageIdToDelete in pageIdsToDelete) {
      await client.Received().DeletePageAsync(pageIdToDelete, CancellationToken.None).ConfigureAwait(false);
    }

    // This ensures no other pages were deleted
    await client
        .ReceivedWithAnyArgs(pageIdsToDelete.Count)
        .DeletePageAsync(Arg.Any<string>(), CancellationToken.None)
        .ConfigureAwait(false);
  }

  private static Dictionary<string, PropertyValue> CreateProperties(PropertyValue propertyValue,
      string name = PropertyName) =>
      new() { { name, propertyValue } };

  private static Dictionary<string, PropertyValue> CreateNumberProperties(double? value) =>
      CreateProperties(new NumberPropertyValue { Number = value });

  private static Dictionary<string, PropertyValue> CreateFormulaNumberProperties(double? value) =>
      CreateProperties(new FormulaPropertyValue { Formula = new FormulaValue { Number = value } });
}
