using Notion.Client;

namespace CleanupForNotion.Core.Infrastructure.NotionIntegration;

public static class CfnNotionClientExtensions {
  public static async Task<List<Page>> QueryDatabaseAsync(
      this ICfnNotionClient cfnNotionClient,
      string databaseId,
      Filter filter,
      CancellationToken cancellationToken) {
    var queryParameters = new DatabasesQueryParameters { Filter = filter };
    var queryResults = await cfnNotionClient.QueryDatabaseRawAsync(databaseId, queryParameters, cancellationToken)
        .ConfigureAwait(false);
    var pages = new List<IWikiDatabase>(queryResults.Results);

    while (queryResults.HasMore) {
      var nextQueryParameters = new DatabasesQueryParameters { Filter = filter, StartCursor = queryResults.NextCursor };
      queryResults = await cfnNotionClient.QueryDatabaseRawAsync(databaseId, nextQueryParameters, cancellationToken)
          .ConfigureAwait(false);
      pages.AddRange(queryResults.Results);
    }

    return pages.Cast<Page>().ToList();
  }

  public static async Task<List<string>> QueryDatabaseIdsAsync(
      this ICfnNotionClient cfnNotionClient,
      string databaseId,
      Filter filter,
      CancellationToken cancellationToken) =>
      (await QueryDatabaseAsync(cfnNotionClient, databaseId, filter, cancellationToken).ConfigureAwait(false))
      .Select(p => p.Id)
      .ToList();
}
