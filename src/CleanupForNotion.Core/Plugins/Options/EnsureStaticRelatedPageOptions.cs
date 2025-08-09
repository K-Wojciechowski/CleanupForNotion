namespace CleanupForNotion.Core.Plugins.Options;

public record EnsureStaticRelatedPageOptions(
  string DatabaseId,
  string PropertyName,
  string RelatedPageId,
  TimeSpan? GracePeriod = null) : IBasicPluginOptions;
