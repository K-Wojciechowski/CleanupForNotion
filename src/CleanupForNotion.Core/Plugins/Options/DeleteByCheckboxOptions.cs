namespace CleanupForNotion.Core.Plugins.Options;

public record DeleteByCheckboxOptions(
  string DataSourceId,
  string PropertyName,
  bool DeleteIfChecked = true,
  TimeSpan? GracePeriod = null) : IBasicPluginOptions;
