using System.Diagnostics.CodeAnalysis;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;
using Notion.Client;

namespace CleanupForNotion.Core.Plugins.Implementations;

public class EstimateExpensesWithSubscriptions(
    ILogger<EstimateExpensesWithSubscriptions> logger,
    IPluginStateProvider pluginStateProvider,
    EstimateExpensesWithSubscriptionsOptions options,
    string pluginDescription,
    string? pluginId) : IPlugin {
  private const string PageCountKey = "PageCount";
  private const string LastEditKey = "LastEdit";
  private static readonly DateTimeOffset _placeholderLastEdit = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

  public string? Id { get; } = pluginId;
  public string Name => "EstimateExpensesWithSubscriptions";
  public string Description { get; } = pluginDescription;
  public IBasicPluginOptions Configuration => options;

  public async Task Run(ICfnNotionClient client, GlobalOptions globalOptions, CancellationToken cancellationToken) {
    var expensePages = await client.QueryDataSourceAsync(options.ExpensesDataSourceId, filter: null, cancellationToken)
        .ConfigureAwait(false);

    var currentLastEdit = expensePages.Count == 0
        ? _placeholderLastEdit
        : new DateTimeOffset(expensePages.Max(p => p.LastEditedTime), TimeSpan.Zero);

    var previousPageCount = await pluginStateProvider.GetInteger(this, PageCountKey, cancellationToken)
        .ConfigureAwait(false);

    var previousLastEdit = await pluginStateProvider.GetDateTime(this, LastEditKey, cancellationToken)
        .ConfigureAwait(false);

    if (previousPageCount.HasValue && previousLastEdit.HasValue && previousPageCount.Value == expensePages.Count &&
        previousLastEdit.Value == currentLastEdit) {
      logger.LogDebug("Not estimating expenses - no updates since last run");
      return;
    }

    logger.LogDebug("Estimating total from {Count} expenses, last edit {LastEdit}", expensePages.Count,
        currentLastEdit);

    var subscriptionPages = await client.QueryDataSourceAsync(options.DataSourceId, filter: null, cancellationToken)
        .ConfigureAwait(false);

    var buckets = subscriptionPages
        .Select(s => new Bucket(
            id: s.Id,
            names: ExtractSubscriptionNames(s),
            expected: ExtractAmount(s)
        ))
        .OrderByDescending(b => b.Expected)
        .ToArray();


    foreach (var page in expensePages) {
      var amount = ExtractAmount(page);
      var vendor = ExtractVendor(page);

      foreach (var bucket in buckets) {
        if (!bucket.Names.Contains(vendor) && bucket.Expected != 0) {
          continue;
        }

        logger.LogTrace("Expense {Name} with amount {Amount} associated with bucket {Bucket}",
            vendor, amount, bucket);
        bucket.Actual += amount;
        break;
      }
    }

    LogBuckets(buckets);

    foreach (var bucket in buckets) {
      await client.UpdatePageAsync(
          bucket.Id,
          new PagesUpdateParameters {
              Properties = new Dictionary<string, PropertyValue> {
                  { options.PropertyName, new NumberPropertyValue { Number = (double)bucket.Value } }
              }
          },
          cancellationToken).ConfigureAwait(false);
    }

    await pluginStateProvider.SetDateTime(this, LastEditKey, currentLastEdit, cancellationToken)
        .ConfigureAwait(false);
    await pluginStateProvider.SetInteger(this, PageCountKey, expensePages.Count, cancellationToken)
        .ConfigureAwait(false);
  }

  private decimal ExtractAmount(Page page) {
    page.Properties.TryGetValue(options.AmountPropertyName, out var amountProperty);

    var doubleAmount = amountProperty is NumberPropertyValue npv
        ? (npv.Number ?? 0)
        : throw new InvalidDataException($"Page {page} does not have a readable amount");

    return Math.Round((decimal)doubleAmount, options.CurrencyDecimalPlaces);
  }

  private string ExtractVendor(Page page) => ExtractVendor(page, options.VendorPropertyName);

  private static string ExtractVendor(Page page, string propertyName) {
    page.Properties.TryGetValue(propertyName, out var vendorProperty);

    var vendorParts = vendorProperty switch {
        TitlePropertyValue tpv => tpv.Title,
        RichTextPropertyValue rtpv => rtpv.RichText,
        _ => throw new InvalidDataException($"Page {page.Id} does not have a readable vendor")
    };

    return string.Join(null, vendorParts.Select(p => p.PlainText)).Trim().ToLower();
  }

  private HashSet<string> ExtractSubscriptionNames(Page page) {
    var mainVendor = ExtractVendor(page, options.VendorPropertyName);
    var alternateVendor = ExtractVendor(page, options.AlternateVendorPropertyName);

    return string.IsNullOrEmpty(alternateVendor)
        ? [mainVendor]
        : [.. alternateVendor.Split(";").Select(v => v.Trim()), mainVendor];
  }

  [ExcludeFromCodeCoverage(Justification = "Trivial trace logging")]
  private void LogBuckets(Bucket[] buckets) {
    if (!logger.IsEnabled(LogLevel.Trace)) return;
    foreach (var b in buckets) {
      logger.LogTrace("Bucket {Bucket} value: {Value}", b, b.Value);
    }
  }

  private class Bucket(string id, HashSet<string> names, decimal expected) {
    public string Id { get; } = id;

    public HashSet<string> Names { get; } = names;

    public decimal Expected { get; } = expected;

    public decimal Actual { get; set; }

    public decimal Value => Math.Max(Expected, Actual);

    private string _description = $"{id} ({names.FirstOrDefault()})";

    public override string ToString() => _description;
  }
}
