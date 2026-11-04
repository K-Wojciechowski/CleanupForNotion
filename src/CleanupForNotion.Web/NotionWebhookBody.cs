using System.Text.Json.Serialization;

namespace CleanupForNotion.Web;

// bare minimum required to extract data source id from data_source.content_updated
public class NotionWebhookBody {
  [JsonPropertyName("entity")]
  public required NotionWebhookEntity Entity { get; init; }
}
