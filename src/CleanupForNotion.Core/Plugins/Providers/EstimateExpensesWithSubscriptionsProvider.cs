using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Plugins.Providers;

public class EstimateExpensesWithSubscriptionsProvider(
    ILoggerFactory loggerFactory,
    IPluginStateProvider pluginStateProvider,
    TimeProvider timeProvider)
    : BasicPluginProviderBase<EstimateExpensesWithSubscriptions, EstimateExpensesWithSubscriptionsOptions>(
        loggerFactory,
        pluginStateProvider,
        timeProvider) {
  public override string Name => "EstimateExpensesWithSubscriptions";

  protected override EstimateExpensesWithSubscriptionsOptions GetOptions(
      RawPluginOptions options,
      string dataSourceId,
      string propertyName,
      TimeSpan? gracePeriod,
      int pluginIndex) {
    return new EstimateExpensesWithSubscriptionsOptions(
        DataSourceId: dataSourceId,
        PropertyName: propertyName,
        ExpensesDataSourceId: options.GetString("ExpensesDataSourceId"),
        VendorPropertyName: options.GetString("VendorPropertyName"),
        AmountPropertyName: options.GetString("AmountPropertyName"),
        AlternateVendorPropertyName: options.GetString("AlternateVendorPropertyName"),
        CurrencyDecimalPlaces: options.GetOptionalInteger("CurrencyDecimalPlaces") ?? 2,
        GracePeriod: gracePeriod);
  }

  protected override IPlugin CreatePlugin(
      ILogger<EstimateExpensesWithSubscriptions> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      EstimateExpensesWithSubscriptionsOptions options,
      string pluginDescription,
      string? pluginId) {
    return new EstimateExpensesWithSubscriptions(logger, pluginStateProvider, options, pluginDescription, pluginId);
  }
}
