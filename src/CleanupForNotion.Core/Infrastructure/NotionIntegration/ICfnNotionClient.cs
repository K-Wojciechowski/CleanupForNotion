using Notion.Client;

namespace CleanupForNotion.Core.Infrastructure.NotionIntegration;

public interface ICfnNotionClient {
  Task<QueryDataSourceResponse> QueryDataSourceRawAsync(
      QueryDataSourceRequest request,
      CancellationToken cancellationToken);

  Task UpdatePageAsync(string pageId, PagesUpdateParameters pagesUpdateParameters, CancellationToken cancellationToken);

  Task DeletePageAsync(string pageId, CancellationToken cancellationToken);
}
