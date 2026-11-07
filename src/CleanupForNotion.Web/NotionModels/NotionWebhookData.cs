using System.Text.Json.Serialization;

namespace CleanupForNotion.Web.NotionModels;

public class NotionWebhookData {
  [JsonPropertyName("parent")]
  public NotionWebhookParent Parent { get; init; }
}