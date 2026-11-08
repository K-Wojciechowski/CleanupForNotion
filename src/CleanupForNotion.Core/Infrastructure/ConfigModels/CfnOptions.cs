namespace CleanupForNotion.Core.Infrastructure.ConfigModels;

public class CfnOptions {
  public required string AuthToken { get; init; }

  public bool DryRun { get; init; } = false;

  public string? RunFrequency { get; init; }

  public string? StateEngine { get; init; }

  public string? StateAccessDetails { get; init; }

  public string? StateFilePath { get; init; }

  public required List<Dictionary<string, object>> Plugins { get; init; }

  public string? WebhookSecret { get; init; }

  public List<WebhookConfiguration>? Webhooks { get; init; }
}
