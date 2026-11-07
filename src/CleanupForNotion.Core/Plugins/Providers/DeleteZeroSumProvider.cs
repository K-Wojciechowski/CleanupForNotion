using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Plugins.Providers;

public class DeleteZeroSumProvider(
    ILoggerFactory loggerFactory,
    IPluginStateProvider pluginStateProvider,
    TimeProvider timeProvider)
    : BasicPluginProviderBase<DeleteZeroSum, DeleteZeroSumOptions>(
        loggerFactory,
        pluginStateProvider,
        timeProvider) {
  public override string Name => "DeleteZeroSum";

  protected override DeleteZeroSumOptions GetOptions(
      RawPluginOptions options,
      string dataSourceId,
      string propertyName,
      TimeSpan? gracePeriod,
      int pluginIndex) {
    return new DeleteZeroSumOptions(
        dataSourceId,
        propertyName,
        gracePeriod);
  }

  protected override IPlugin CreatePlugin(
      ILogger<DeleteZeroSum> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      DeleteZeroSumOptions options,
      string pluginDescription,
      string? pluginId) {
    return new DeleteZeroSum(logger, pluginStateProvider, timeProvider, options, pluginDescription, pluginId);
  }
}
