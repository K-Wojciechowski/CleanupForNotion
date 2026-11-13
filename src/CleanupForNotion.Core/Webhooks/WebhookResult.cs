using System.Net;

namespace CleanupForNotion.Core.Webhooks;

public class WebhookResult {
  public HttpStatusCode StatusCode { get; }
  public IReadOnlyCollection<string> PluginIds { get; }

  private WebhookResult(HttpStatusCode statusCode, IReadOnlyCollection<string> pluginIds) {
    StatusCode = statusCode;
    PluginIds = pluginIds;
  }

  public static WebhookResult Ok(IReadOnlyCollection<string> pluginIds) => new(HttpStatusCode.OK, pluginIds);

  public static WebhookResult Ok() => new(HttpStatusCode.OK, []);

  public static WebhookResult Failure(HttpStatusCode statusCode) => new(statusCode, []);
}
