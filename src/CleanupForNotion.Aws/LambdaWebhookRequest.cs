using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using CleanupForNotion.Core;
using CleanupForNotion.Core.Webhooks;

namespace CleanupForNotion.Aws;

internal class LambdaWebhookRequest(APIGatewayHttpApiV2ProxyRequest request) : IWebhookRequest {
  private readonly byte[] _bodyBytes = request.IsBase64Encoded
      ? Convert.FromBase64String(request.Body)
      : Encoding.UTF8.GetBytes(request.Body);

  public long? ContentLength => _bodyBytes.Length;

  public string? SignatureHeader =>
      request.Headers.TryGetValue(Constants.WebhookSignatureHeaderName.ToLowerInvariant(), out var signatureHeader)
          ? signatureHeader
          : null;

  public Task<byte[]> ReadBodyAsync(CancellationToken cancellationToken) => Task.FromResult(_bodyBytes);
}
