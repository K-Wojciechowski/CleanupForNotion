namespace CleanupForNotion.Core.Webhooks;

public interface IWebhookRequest {
  long? ContentLength { get; }
  string? SignatureHeader { get; }
  Task<byte[]> ReadBodyAsync(CancellationToken cancellationToken);
}
