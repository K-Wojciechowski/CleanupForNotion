using System.Text.Json.Serialization;

namespace CleanupForNotion.Web.NotionModels;

public class NotionWebhookEntity {
  [JsonPropertyName(("id"))]
  public required string Id { get; init; }

  [JsonPropertyName(("type"))]
  public required string Type { get; init; }
}
