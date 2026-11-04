using System.Text.Json.Serialization;

namespace CleanupForNotion.Web;

public class NotionWebhookParent {
  [JsonPropertyName(("id"))]
  public required string Id { get; init; }

  [JsonPropertyName(("type"))]
  public required string Type { get; init; }
  
  [JsonPropertyName("data_source_id")]
  public string? DataSourceId { get; init; }
}