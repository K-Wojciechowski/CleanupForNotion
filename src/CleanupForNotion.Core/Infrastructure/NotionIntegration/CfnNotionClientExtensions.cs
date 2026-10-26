using Notion.Client;

namespace CleanupForNotion.Core.Infrastructure.NotionIntegration;

public static class CfnNotionClientExtensions {
  public static async Task<List<Page>> QueryDataSourceAsync(
      this ICfnNotionClient cfnNotionClient,
      string dataSourceId,
      Filter filter,
      CancellationToken cancellationToken) {
    var request = new QueryDataSourceRequest { DataSourceId = dataSourceId, Filter = filter };
    var queryResults = await cfnNotionClient.QueryDataSourceRawAsync(request, cancellationToken)
        .ConfigureAwait(false);
    var pages = new List<IQueryDataSourceResponseObject>(queryResults.Results);

    while (queryResults.HasMore) {
      var nextRequest = new QueryDataSourceRequest { DataSourceId = dataSourceId, Filter = filter, StartCursor = queryResults.NextCursor };
      queryResults = await cfnNotionClient.QueryDataSourceRawAsync(nextRequest, cancellationToken)
          .ConfigureAwait(false);
      pages.AddRange(queryResults.Results);
    }

    return pages.Cast<Page>().ToList();
  }

  public static async Task<List<string>> QueryDataSourceIdsAsync(
      this ICfnNotionClient cfnNotionClient,
      string dataSourceId,
      Filter filter,
      CancellationToken cancellationToken) =>
      (await cfnNotionClient.QueryDataSourceAsync(dataSourceId, filter, cancellationToken).ConfigureAwait(false))
      .Select(p => p.Id)
      .ToList();
}
