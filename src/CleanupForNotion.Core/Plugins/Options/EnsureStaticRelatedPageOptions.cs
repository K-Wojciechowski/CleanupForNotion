namespace CleanupForNotion.Core.Plugins.Options;

public record EnsureStaticRelatedPageOptions(
  string DataSourceId,
  string PropertyName,
  string RelatedPageId,
  TimeSpan? GracePeriod = null) : IBasicPluginOptions;
