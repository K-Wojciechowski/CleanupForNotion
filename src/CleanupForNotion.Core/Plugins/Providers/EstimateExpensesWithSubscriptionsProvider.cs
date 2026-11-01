using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Plugins.Providers;

public class EstimateExpensesWithSubscriptionsProvider(
    IConfiguration configuration,
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
    var subscriptions = configuration
        .GetSection($"CleanupForNotion:Plugins:{pluginIndex}:Subscriptions")
        .Get<ExpenseSubscription[]>();

    return new EstimateExpensesWithSubscriptionsOptions(
        DataSourceId: dataSourceId,
        PropertyName: propertyName,
        OutputPageId: options.GetString("OutputPageId"),
        NamePropertyName: options.GetString("NamePropertyName"),
        AmountPropertyName: options.GetString("AmountPropertyName"),
        Subscriptions: subscriptions ?? throw new InvalidConfigurationException($"Missing subscription configuration for plugin {pluginIndex}"),
        CurrencyDecimalPlaces: options.GetInteger("CurrencyDecimalPlaces"),
        GracePeriod: gracePeriod);
  }

  protected override EstimateExpensesWithSubscriptions CreatePlugin(
      ILogger<EstimateExpensesWithSubscriptions> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      EstimateExpensesWithSubscriptionsOptions options,
      string pluginDescription) {
    return new EstimateExpensesWithSubscriptions(logger, pluginStateProvider, options, pluginDescription);
  }
}
