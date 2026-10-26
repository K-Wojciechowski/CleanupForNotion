using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Plugins.Providers;

public class DeleteWithoutRelationshipsProvider(
    ILoggerFactory loggerFactory,
    IPluginStateProvider pluginStateProvider,
    TimeProvider timeProvider)
    : BasicPluginProviderBase<DeleteWithoutRelationships, DeleteWithoutRelationshipsOptions>(
        loggerFactory,
        pluginStateProvider,
        timeProvider) {
  public override string Name => "DeleteWithoutRelationships";

  protected override DeleteWithoutRelationshipsOptions GetOptions(
      RawPluginOptions options,
      string dataSourceId,
      string propertyName,
      TimeSpan? gracePeriod) {
    return new DeleteWithoutRelationshipsOptions(
        dataSourceId,
        propertyName,
        gracePeriod);
  }

  protected override DeleteWithoutRelationships CreatePlugin(
      ILogger<DeleteWithoutRelationships> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      DeleteWithoutRelationshipsOptions options,
      string pluginDescription) {
    return new DeleteWithoutRelationships(logger, pluginStateProvider, timeProvider, options, pluginDescription);
  }
}
