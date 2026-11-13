namespace CleanupForNotion.Core.Webhooks;

public interface IWebhookProcessor {
  Task<WebhookResult> ProcessWebhookAsync(IWebhookRequest request, CancellationToken cancellationToken);
}
