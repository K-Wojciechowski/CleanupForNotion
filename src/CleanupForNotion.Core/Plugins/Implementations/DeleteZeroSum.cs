using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Infrastructure.Time;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;
using Notion.Client;

namespace CleanupForNotion.Core.Plugins.Implementations;

public class DeleteZeroSum(
    ILogger<DeletePluginBase<DeleteZeroSumOptions>> logger,
    IPluginStateProvider pluginStateProvider,
    TimeProvider timeProvider,
    DeleteZeroSumOptions options,
    string pluginDescription)
    : DeletePluginBase<DeleteZeroSumOptions>(logger, pluginStateProvider, timeProvider, options, pluginDescription) {
  public override string Name => "DeleteZeroSum";

  public override async Task Run(ICfnNotionClient client, GlobalOptions globalOptions,
      CancellationToken cancellationToken) {
    var filter = LastEditedFilterHelper.GetLastEditedFilter(TimeProvider, Options);

    var pages = await client.QueryDatabaseAsync(Options.DatabaseId, filter, cancellationToken).ConfigureAwait(false);
    var buckets = new Buckets();

    foreach (var page in pages) {
      var propertyName = Options.PropertyName;
      var pageId = page.Id;

      if (!page.Properties.TryGetValue(propertyName, out var rawValue)) {
        Logger.LogError("Property '{Property}' not found in page '{Page}', skipping", propertyName, pageId);
        continue;
      }

      if (rawValue is NumberPropertyValue numberValue) {
        var nullableValue = numberValue.Number;
        if (!nullableValue.HasValue) {
          Logger.LogTrace("Property '{Property}' in page '{Page}' has a null numeric value, skipping", propertyName, pageId);
          continue;
        }

        buckets.Add(nullableValue.Value, page);
      } else if (rawValue is FormulaPropertyValue formulaValue) {
        var nullableValue = formulaValue.Formula.Number;
        if (!nullableValue.HasValue) {
          Logger.LogTrace("Property '{Property}' in page '{Page}' has a null formula value, skipping", propertyName, pageId);
          continue;
        }

        buckets.Add(nullableValue.Value, page);
      } else {
        Logger.LogError("Property '{Property}' in page '{Page}' does not have a numeric or formula value (found '{Type}'), skipping",
            propertyName, pageId, rawValue.Type);
      }
    }

    var bucketsWithPairs = buckets.Values.Where(b => b.Negative.Count > 0 && b.Positive.Count > 0);

    if (buckets.Zero.Count > 0) {
      Logger.LogInformation("Found {Count} pairs with a value of zero, removing", buckets.Zero.Count);
      await DoDelete(client, globalOptions, buckets.Zero.Select(p => p.Id).ToArray(), cancellationToken).ConfigureAwait(false);
    }

    foreach (var bucket in bucketsWithPairs) {
      var countToRemove = Math.Min(bucket.Negative.Count, bucket.Positive.Count);
      Logger.LogInformation("Found {Count} pairs in bucket '{Value}', removing", countToRemove, bucket.AbsoluteValue);
      var pagesToRemove = Take(bucket.Positive, countToRemove)
          .Concat(Take(bucket.Negative, countToRemove))
          .Select(p => p.Id)
          .ToArray();
      await DoDelete(client, globalOptions, pagesToRemove, cancellationToken).ConfigureAwait(false);
    }
  }

  private IEnumerable<Page> Take(List<Page> pages, int count)
    => pages.Count <= count ? pages : pages.OrderBy(p => p.CreatedTime).Take(count);

  private class Bucket(double absoluteValue) {
    public double AbsoluteValue { get; } = absoluteValue;

    public List<Page> Positive { get; } = [];
    public List<Page> Negative { get; } = [];
  }

  private class Buckets {
    private readonly Dictionary<double, Bucket> _buckets = [];

    public List<Page> Zero { get; } = [];

    public IEnumerable<Bucket> Values => _buckets.Values;

    public void Add(double value, Page page) {
      double absoluteValue = Math.Abs(value);
      if (!_buckets.TryGetValue(absoluteValue, out var bucket)) {
        bucket = new Bucket(absoluteValue);
        _buckets[absoluteValue] = bucket;
      }

      switch (value) {
        case 0:
          Zero.Add(page);
          break;
        case < 0:
          bucket.Negative.Add(page);
          break;
        default:
          bucket.Positive.Add(page);
          break;
      }
    }
  }
}
