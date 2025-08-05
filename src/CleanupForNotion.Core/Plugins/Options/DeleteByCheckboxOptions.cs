namespace CleanupForNotion.Core.Plugins.Options;

public record DeleteByCheckboxOptions(
  string DatabaseId,
  string PropertyName,
  bool DeleteIfChecked = true,
  TimeSpan? GracePeriod = null) : IBasicPluginOptions;
