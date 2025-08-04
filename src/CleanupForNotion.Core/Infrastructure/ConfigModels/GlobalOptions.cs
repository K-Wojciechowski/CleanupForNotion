namespace CleanupForNotion.Core.Infrastructure.ConfigModels;

public record GlobalOptions(bool DryRun = false, TimeSpan? RunFrequency = null);
