using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Plugins.Providers;

public class DeleteOnNewMonthlyCycleProvider(
    ILoggerFactory loggerFactory,
    IPluginStateProvider pluginStateProvider,
    TimeProvider timeProvider)
    : BasicPluginProviderBase<DeleteOnNewMonthlyCycle, DeleteOnNewMonthlyCycleOptions>(
        loggerFactory,
        pluginStateProvider,
        timeProvider) {
  public override string Name => "DeleteOnNewMonthlyCycle";

  protected override DeleteOnNewMonthlyCycleOptions GetOptions(
      RawPluginOptions options,
      string databaseId,
      string propertyName,
      TimeSpan? gracePeriod) {
    return new DeleteOnNewMonthlyCycleOptions(
        DatabaseId: databaseId,
        PropertyName: propertyName,
        CycleResetDay: options.GetInteger("CycleResetDay"),
        MonthOverflowResetsOnFirstDayOfNextMonth: options.GetOptionalBoolean("MonthOverflowResetsOnFirstDayOfNextMonth") ?? true,
        TimeZoneName: options.GetOptionalString("TimeZoneName"),
        GracePeriod: gracePeriod);
  }

  protected override DeleteOnNewMonthlyCycle CreatePlugin(
      ILogger<DeleteOnNewMonthlyCycle> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      DeleteOnNewMonthlyCycleOptions options,
      string pluginDescription) {
    return new DeleteOnNewMonthlyCycle(logger, pluginStateProvider, timeProvider, options, pluginDescription);
  }
}
