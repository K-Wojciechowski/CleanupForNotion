using System.Diagnostics.CodeAnalysis;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notion.Client;

namespace CleanupForNotion.Core.Infrastructure.NotionIntegration;

[ExcludeFromCodeCoverage(Justification = "Trivial wrapper for NotionClient")]
public class CfnNotionClient(ILogger<CfnNotionClient> logger, IOptions<CfnOptions> options) : ICfnNotionClient {
  private readonly NotionClient _notionClient =
      NotionClientFactory.Create(new ClientOptions { AuthToken = options.Value.AuthToken });

  public async Task<DatabaseQueryResponse> QueryDatabaseRawAsync(
      string databaseId,
      DatabasesQueryParameters queryParameters,
      CancellationToken cancellationToken)
    => await RateLimitHelper.CallWithRetryAsync(async () =>
            await _notionClient.Databases
                .QueryAsync(databaseId, queryParameters, cancellationToken)
                .ConfigureAwait(false),
        logger,
        cancellationToken).ConfigureAwait(false);

  public async Task UpdatePageAsync(string pageId, PagesUpdateParameters pagesUpdateParameters, CancellationToken cancellationToken)
    => await RateLimitHelper.CallWithRetryAsync(async () =>
            await _notionClient.Pages
                .UpdateAsync(pageId, pagesUpdateParameters, cancellationToken)
                .ConfigureAwait(false),
        logger,
        cancellationToken).ConfigureAwait(false);

  public async Task DeletePageAsync(string pageId, CancellationToken cancellationToken)
    => await UpdatePageAsync(pageId, new PagesUpdateParameters { InTrash = true }, cancellationToken).ConfigureAwait(false);
}
