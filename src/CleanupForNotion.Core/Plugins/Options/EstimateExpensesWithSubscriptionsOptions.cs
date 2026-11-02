namespace CleanupForNotion.Core.Plugins.Options;

public record EstimateExpensesWithSubscriptionsOptions(
    string DataSourceId,
    string PropertyName,
    string ExpensesDataSourceId,
    string VendorPropertyName,
    string AmountPropertyName,
    string AlternateVendorPropertyName,
    int CurrencyDecimalPlaces,
    TimeSpan? GracePeriod = null) : IBasicPluginOptions;
