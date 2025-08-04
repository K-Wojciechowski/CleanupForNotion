using Microsoft.Extensions.Logging;
using Notion.Client;

namespace CleanupForNotion.Core.Infrastructure.NotionIntegration;

public static class RateLimitHelper {
  public static async Task<T> CallWithRetryAsync<T>(Func<Task<T>> func, ILogger logger, CancellationToken cancellationToken) {
    while (true) {
      try {
        return await func().ConfigureAwait(false);
      } catch (NotionApiRateLimitException rateLimitException) {
        var retryAfter = rateLimitException.RetryAfter ?? TimeSpan.FromSeconds(5);
        logger.LogWarning(rateLimitException, "Rate limit exceeded, will retry after {Delay} seconds",
            retryAfter.TotalSeconds);
        await Task.Delay(retryAfter, cancellationToken).ConfigureAwait(false);
      }
    }
  }
}
