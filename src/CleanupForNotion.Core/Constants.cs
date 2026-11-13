namespace CleanupForNotion.Core;

public static class Constants {
  public static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromHours(1);
  public const string WebhookSignatureHeaderName = "X-Notion-Signature";
}
