using CleanupForNotion.Core.Infrastructure.ConfigModels;
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
      string databaseId,
      string propertyName,
      TimeSpan? gracePeriod) {
    return new DeleteZeroSumOptions(
        databaseId,
        propertyName,
        gracePeriod);
  }

  protected override DeleteZeroSum CreatePlugin(
      ILogger<DeleteZeroSum> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      DeleteZeroSumOptions options,
      string pluginDescription) {
    return new DeleteZeroSum(logger, pluginStateProvider, timeProvider, options, pluginDescription);
  }
}
