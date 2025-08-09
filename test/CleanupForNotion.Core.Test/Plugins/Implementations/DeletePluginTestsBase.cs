using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using Microsoft.Extensions.Logging.Testing;
using Notion.Client;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Plugins.Implementations;

public abstract class DeletePluginTestsBase {
  private const string DatabaseId = "databaseId";
  private static readonly string[] _pageIdsToDelete = ["a", "b", "c"];

  protected static ICfnNotionClient CreateMockNotionClient()
  {
    var client = Substitute.For<ICfnNotionClient>();

    client.QueryDatabaseRawAsync(
        DatabaseId,
        Arg.Any<DatabasesQueryParameters>(), CancellationToken.None).Returns(
        new DatabaseQueryResponse {
            Results = _pageIdsToDelete.Select(id => new Page { Id = id }).Cast<IWikiDatabase>().ToList()
        });

    client.DeletePageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsForAnyArgs(Task.CompletedTask);

    return client;
  }

  protected static async Task AssertPagesDeleted<TPlugin>(
      FakeLogger<TPlugin> logger,
      ICfnNotionClient client,
      CompoundFilter expectedCompoundFilter,
      bool dryRun
  ) {
    // Assert
    var logRecords = logger.Collector.GetSnapshot();
    logRecords.ShouldContain(r => r.Message.StartsWith("Searching for pages to change with filters: {\""));
    logRecords.ShouldContain(r => r.Message == "Pages to delete: 3");

    await client.ReceivedWithAnyArgs()
        .QueryDatabaseRawAsync(Arg.Any<string>(), Arg.Any<DatabasesQueryParameters>(), CancellationToken.None)
        .ConfigureAwait(false);

    var queryCall = client.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "QueryDatabaseRawAsync");
    var queryCallArguments = queryCall.GetArguments();
    queryCallArguments[0].ShouldBe(DatabaseId);
    queryCallArguments[1].ShouldBeOfType<DatabasesQueryParameters>().Filter
        .ShouldBeEquivalentTo(expectedCompoundFilter);
    queryCallArguments[2].ShouldBe(CancellationToken.None);

    if (dryRun) {
      await client.DidNotReceiveWithAnyArgs().DeletePageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
          .ConfigureAwait(false);
    }

    foreach (var pageId in _pageIdsToDelete) {
      logRecords.ShouldContain(r => r.Message == $"Deleting page '{pageId}'");
      if (!dryRun) {
        await client.Received().DeletePageAsync(pageId, CancellationToken.None).ConfigureAwait(false);
      }
    }
  }
}
