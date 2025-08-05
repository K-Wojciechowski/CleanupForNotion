using CleanupForNotion.Core.Plugins.Options;
using Notion.Client;

namespace CleanupForNotion.Core.Infrastructure.Time;

public static class LastEditedFilterHelper {
  public static TimestampLastEditedTimeFilter GetLastEditedFilter(TimeProvider timeProvider, TimeSpan gracePeriod) {
    var lastEditedBefore = timeProvider.GetUtcNow() - gracePeriod;
    return new TimestampLastEditedTimeFilter(onOrBefore: lastEditedBefore.DateTime);
  }

  public static TimestampLastEditedTimeFilter GetLastEditedFilter(
      TimeProvider timeProvider,
      IBasicPluginOptions basicPluginOptions) =>
      GetLastEditedFilter(timeProvider, basicPluginOptions.GracePeriodWithFallback);

  public static CompoundFilter GetCompoundFilterWithLastEdited(
      TimeProvider timeProvider,
      TimeSpan gracePeriod,
      IEnumerable<Filter> filters) =>
      new(and: [
          GetLastEditedFilter(timeProvider, gracePeriod),
          .. filters
      ]);

  public static CompoundFilter GetCompoundFilterWithLastEdited(
      TimeProvider timeProvider,
      IBasicPluginOptions basicPluginOptions,
      IEnumerable<Filter> filters) =>
      GetCompoundFilterWithLastEdited(timeProvider, basicPluginOptions.GracePeriodWithFallback, filters);
}
