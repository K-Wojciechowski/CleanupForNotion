namespace CleanupForNotion.Core.Plugins.Options;

public record DeleteZeroSumOptions(
  string DatabaseId,
  string PropertyName,
  TimeSpan? GracePeriod = null) : IDeletePluginOptions;
