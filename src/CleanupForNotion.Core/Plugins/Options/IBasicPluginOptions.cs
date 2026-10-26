namespace CleanupForNotion.Core.Plugins.Options;

public interface IBasicPluginOptions {
  string DataSourceId { get; }

  string PropertyName { get; }

  TimeSpan? GracePeriod { get; }

  public TimeSpan GracePeriodWithFallback => GracePeriod ?? Constants.DefaultGracePeriod;
}
