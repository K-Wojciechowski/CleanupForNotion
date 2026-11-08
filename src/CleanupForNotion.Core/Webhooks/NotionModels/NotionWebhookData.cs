using System.Text.Json.Serialization;

namespace CleanupForNotion.Core.Webhooks.NotionModels;

public class NotionWebhookData {
  [JsonPropertyName("parent")]
  public NotionWebhookParent Parent { get; init; }
}