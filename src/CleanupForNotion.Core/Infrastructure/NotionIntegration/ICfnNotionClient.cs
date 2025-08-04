using Notion.Client;

namespace CleanupForNotion.Core.Infrastructure.NotionIntegration;

public interface ICfnNotionClient {
  Task<DatabaseQueryResponse> QueryDatabaseRawAsync(
      string databaseId,
      DatabasesQueryParameters queryParameters,
      CancellationToken cancellationToken);

  Task DeletePageAsync(string pageId, CancellationToken cancellationToken);
}
