using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Plugins.Providers;

public class DeleteByCheckboxProvider(
    ILoggerFactory loggerFactory,
    IPluginStateProvider pluginStateProvider,
    TimeProvider timeProvider)
    : BasicPluginProviderBase<DeleteByCheckbox, DeleteByCheckboxOptions>(
        loggerFactory,
        pluginStateProvider,
        timeProvider) {
  public override string Name => "DeleteByCheckbox";

  protected override DeleteByCheckboxOptions GetOptions(
      RawPluginOptions options,
      string dataSourceId,
      string propertyName,
      TimeSpan? gracePeriod) {
    return new DeleteByCheckboxOptions(
        dataSourceId,
        propertyName,
        options.GetOptionalBoolean("DeleteIfChecked") ?? true,
        gracePeriod);
  }

  protected override DeleteByCheckbox CreatePlugin(
      ILogger<DeleteByCheckbox> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      DeleteByCheckboxOptions options,
      string pluginDescription) {
    return new DeleteByCheckbox(logger, pluginStateProvider, timeProvider, options, pluginDescription);
  }
}
