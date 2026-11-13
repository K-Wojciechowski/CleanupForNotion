namespace CleanupForNotion.Core.Infrastructure.ConfigModels;

public class WebhookConfiguration {
  public required string DataSourceId { get; init; }

  public required List<string> PluginIds { get; init; }
}
