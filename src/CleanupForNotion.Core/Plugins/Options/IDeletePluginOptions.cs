namespace CleanupForNotion.Core.Plugins.Options;

public interface IDeletePluginOptions {
  string DatabaseId { get; }

  string PropertyName { get; }

  TimeSpan? GracePeriod { get; }

  public TimeSpan GracePeriodWithFallback => GracePeriod ?? Constants.DefaultGracePeriod;
}
