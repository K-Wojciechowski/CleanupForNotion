using CleanupForNotion.Core.Infrastructure.ConfigModels;
using Microsoft.Extensions.Options;

namespace CleanupForNotion.Core.Infrastructure.PluginManagement;

public class GlobalOptionsProvider(IOptions<CfnOptions> options) : IGlobalOptionsProvider {
  public GlobalOptions GlobalOptions { get; } = new(
    DryRun: options.Value.DryRun,
    RunFrequency: options.Value.RunFrequency == null ? null : TimeSpan.Parse(options.Value.RunFrequency));
}
