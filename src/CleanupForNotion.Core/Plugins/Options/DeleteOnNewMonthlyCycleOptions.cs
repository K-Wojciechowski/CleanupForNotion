namespace CleanupForNotion.Core.Plugins.Options;

public record DeleteOnNewMonthlyCycleOptions(
  string DataSourceId,
  string PropertyName,
  int CycleResetDay,
  bool MonthOverflowResetsOnFirstDayOfNextMonth = true,
  string? TimeZoneName = null,
  TimeSpan? GracePeriod = null) : IBasicPluginOptions;
