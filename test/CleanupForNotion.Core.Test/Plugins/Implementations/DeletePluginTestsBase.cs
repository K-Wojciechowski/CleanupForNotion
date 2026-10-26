using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using Microsoft.Extensions.Logging.Testing;
using Notion.Client;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Plugins.Implementations;

public abstract class DeletePluginTestsBase {
  private const string DataSourceId = "dataSourceId";
  private static readonly string[] _pageIdsToDelete = ["a", "b", "c"];

  protected static ICfnNotionClient CreateMockNotionClient()
  {
    var client = Substitute.For<ICfnNotionClient>();

    client.QueryDataSourceRawAsync(
        Arg.Any<QueryDataSourceRequest>(), CancellationToken.None).Returns(
        new QueryDataSourceResponse {
            Results = _pageIdsToDelete.Select(id => new Page { Id = id }).Cast<IQueryDataSourceResponseObject>().ToList()
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
        .QueryDataSourceRawAsync(Arg.Any<QueryDataSourceRequest>(), CancellationToken.None)
        .ConfigureAwait(false);

    var queryCall = client.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "QueryDataSourceRawAsync");
    var queryCallArguments = queryCall.GetArguments();

    var request = queryCallArguments[0].ShouldBeOfType<QueryDataSourceRequest>();
    request.DataSourceId.ShouldBe(DataSourceId);
    request.Filter.ShouldBeEquivalentTo(expectedCompoundFilter);

    queryCallArguments[1].ShouldBe(CancellationToken.None);

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
