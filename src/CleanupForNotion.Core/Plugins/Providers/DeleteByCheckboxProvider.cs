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
    : DeletePluginProviderBase<DeleteByCheckbox, DeleteByCheckboxOptions>(
        loggerFactory,
        pluginStateProvider,
        timeProvider) {
  public override string Name => "DeleteByCheckbox";

  protected override DeleteByCheckboxOptions GetOptions(
      RawPluginOptions options,
      string databaseId,
      TimeSpan? gracePeriod) {
    return new DeleteByCheckboxOptions(
        databaseId,
        options.GetString("PropertyName"),
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
