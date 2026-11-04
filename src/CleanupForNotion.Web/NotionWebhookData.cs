using System.Text.Json.Serialization;

namespace CleanupForNotion.Web;

public class NotionWebhookData {
  [JsonPropertyName("parent")]
  public NotionWebhookParent Parent { get; init; }
}