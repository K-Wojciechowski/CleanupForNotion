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
    TimeProvider timeProvider,
    EstimateExpensesWithSubscriptionsOptions options,
    string pluginDescription) : IPlugin {
  private const string PageCountKey = "PageCount";
  private const string LastEditKey = "LastEdit";
  private static readonly DateTimeOffset _placeholderLastEdit = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

  public string Name => "EnsureStaticRelatedPage";
  public string Description { get; } = pluginDescription;

  public async Task Run(ICfnNotionClient client, GlobalOptions globalOptions, CancellationToken cancellationToken) {
    var pages = await client.QueryDataSourceAsync(options.DataSourceId, filter: null, cancellationToken)
        .ConfigureAwait(false);

    var currentLastEdit = pages.Count == 0
        ? _placeholderLastEdit
        : new DateTimeOffset(pages.Max(p => p.LastEditedTime), TimeSpan.Zero);

    var previousPageCount = await pluginStateProvider.GetInteger(Name, Description, PageCountKey, cancellationToken)
        .ConfigureAwait(false);

    var previousLastEdit = await pluginStateProvider.GetDateTime(Name, Description, LastEditKey, cancellationToken)
        .ConfigureAwait(false);

    if (previousPageCount.HasValue && previousLastEdit.HasValue && previousPageCount.Value == pages.Count &&
        previousLastEdit.Value == currentLastEdit) {
      logger.LogDebug("Not estimating expenses - no updates since last run");
      return;
    }

    logger.LogDebug("Estimating total from {Count} expenses, last edit {LastEdit}", pages.Count, currentLastEdit);

    var buckets = options.Subscriptions
        .Select(s => new Bucket(
            s.Names.Select(n => n.ToLower()).ToHashSet(),
            s.ExpectedTotal / (decimal)(Math.Pow(10, options.CurrencyDecimalPlaces))))
        .Append(new Bucket(null, 0))
        .ToArray();

    foreach (var page in pages) {
      var nameProperty = page.Properties.GetValueOrNull(options.NamePropertyName);
      var amountProperty = page.Properties.GetValueOrNull(options.AmountPropertyName);

      var nameParts = nameProperty switch {
          TitlePropertyValue tpv => tpv.Title,
          RichTextPropertyValue rtpv => rtpv.RichText,
          _ => throw new Exception($"Page {page.Id} does not have a readable name")
      };

      var name = string.Join(null, nameParts.Select(p => p.PlainText)).ToLower().Trim();

      var doubleAmount = amountProperty is NumberPropertyValue npv
          ? (npv.Number ?? 0)
          : throw new Exception($"Page {page} ({name}) does not have a readable amount");

      var amount = Math.Round(((decimal)doubleAmount), options.CurrencyDecimalPlaces);

      foreach (var bucket in buckets) {
        if (bucket.Names?.Contains(name) == false) {
          continue;
        }

        logger.LogTrace("Expense {Name} with amount {Amount} associated with bucket {Bucket}", name, amount, bucket.Id);
        bucket.Actual += amount;
        break;
      }
    }

    var estimatedTotal = buckets.Sum(b => b.Value);
    logger.LogDebug("Estimated total for all buckets: {Total}", estimatedTotal);

    await client.UpdatePageAsync(
        options.OutputPageId,
        new PagesUpdateParameters {
            Properties = new Dictionary<string, PropertyValue> {
                { options.PropertyName, new NumberPropertyValue { Number = (double)estimatedTotal } }
            }
        },
        cancellationToken).ConfigureAwait(false);

    await pluginStateProvider.SetDateTime(Name, Description, LastEditKey, currentLastEdit, cancellationToken)
        .ConfigureAwait(false);
    await pluginStateProvider.SetInteger(Name, Description, PageCountKey, pages.Count, cancellationToken)
        .ConfigureAwait(false);
  }

  private class Bucket(HashSet<string>? names, decimal expected) {
    public HashSet<string>? Names { get; } = names;
    public decimal Actual { get; set; }
    public decimal Value => Math.Max(expected, Actual);

    public string Id { get; } = names?.First() ?? "(null)";
  }
}
