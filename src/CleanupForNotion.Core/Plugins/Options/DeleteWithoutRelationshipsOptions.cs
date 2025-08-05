namespace CleanupForNotion.Core.Plugins.Options;

public record DeleteWithoutRelationshipsOptions(
  string DatabaseId,
  string PropertyName,
  TimeSpan? GracePeriod = null) : IBasicPluginOptions;
