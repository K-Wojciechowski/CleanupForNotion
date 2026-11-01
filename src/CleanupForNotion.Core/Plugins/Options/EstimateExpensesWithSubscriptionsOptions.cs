namespace CleanupForNotion.Core.Plugins.Options;

public record EstimateExpensesWithSubscriptionsOptions(
    string DataSourceId,
    string PropertyName,
    string OutputPageId,
    string VendorPropertyName,
    string AmountPropertyName,
    ExpenseSubscription[] Subscriptions,
    int CurrencyDecimalPlaces,
    TimeSpan? GracePeriod = null) : IBasicPluginOptions;
