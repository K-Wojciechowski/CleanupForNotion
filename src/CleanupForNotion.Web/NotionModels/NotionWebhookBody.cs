using System.Text.Json.Serialization;

namespace CleanupForNotion.Web.NotionModels;

// bare minimum required to extract data source id
public class NotionWebhookBody {
  [JsonPropertyName("type")]
  public required string Type { get; init; }

  [JsonPropertyName("entity")]
  public required NotionWebhookEntity Entity { get; init; }

  [JsonPropertyName("data")]
  public required NotionWebhookData Data { get; init; }
}
