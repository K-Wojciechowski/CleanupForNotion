namespace CleanupForNotion.Core.Plugins.Options;

public record DeleteZeroSumOptions(
  string DataSourceId,
  string PropertyName,
  TimeSpan? GracePeriod = null) : IBasicPluginOptions;
