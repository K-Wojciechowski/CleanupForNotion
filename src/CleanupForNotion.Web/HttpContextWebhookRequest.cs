using CleanupForNotion.Core;
using CleanupForNotion.Core.Webhooks;

namespace CleanupForNotion.Web;

internal class HttpContextWebhookRequest(HttpContext httpContext) : IWebhookRequest {
  public long? ContentLength => httpContext.Request.ContentLength;

  public string? SignatureHeader =>
      httpContext.Request.Headers.TryGetValue(Constants.WebhookSignatureHeaderName, out var signatureHeaders)
          ? signatureHeaders.ToString()
          : null;

  public async Task<byte[]> ReadBodyAsync(CancellationToken cancellationToken) {
    if (ContentLength is null) {
      throw new InvalidOperationException("ContentLength is null");
    }

    var bodyBytes = new byte[ContentLength.Value];
    _ = await httpContext.Request.Body.ReadAsync(bodyBytes, cancellationToken).ConfigureAwait(false);

    return bodyBytes;
  }
}
