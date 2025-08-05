using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.Time;
using CleanupForNotion.Core.Infrastructure.Traceability;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;
using Notion.Client;

namespace CleanupForNotion.Core.Plugins.Implementations;

public class EnsureStaticRelatedPage(
    ILogger<EnsureStaticRelatedPage> logger,
    TimeProvider timeProvider,
    EnsureStaticRelatedPageOptions options,
    string pluginDescription) : IPlugin {
  public string Name => "EnsureStaticRelatedPage";
  public string Description { get; } = pluginDescription;

  public async Task Run(ICfnNotionClient client, GlobalOptions globalOptions, CancellationToken cancellationToken) {
    if (options.RelatedPageId == "?") {
      await RunDebugMode(client, cancellationToken).ConfigureAwait(false);
      return;
    }

    var relationFilter = new RelationFilter(options.PropertyName, doesNotContain: options.RelatedPageId);
    var filter = LastEditedFilterHelper.GetCompoundFilterWithLastEdited(timeProvider, options, [relationFilter]);
    logger.LogFilters(filter);

    var pagesToUpdate = await client
        .QueryDatabaseAsync(options.DatabaseId, filter, cancellationToken)
        .ConfigureAwait(false);

    if (pagesToUpdate.Count == 0) {
      logger.LogInformation("No pages to update");
      return;
    }

    foreach (var page in pagesToUpdate) {
      logger.LogInformation("Updating page '{Id}'", page.Id);
      if (globalOptions.DryRun) continue;

      await client.UpdatePageAsync(page.Id,
          new PagesUpdateParameters {
              Properties = new Dictionary<string, PropertyValue> {
                  {
                      options.PropertyName,
                      new RelationPropertyValue { Relation = [new ObjectId { Id = options.RelatedPageId }] }
                  }
              }
          }, cancellationToken).ConfigureAwait(false);
    }
  }

  private async Task RunDebugMode(ICfnNotionClient client, CancellationToken cancellationToken) {
    logger.LogInformation("Running {Name} ({Description}) in debug mode", Name, Description);

    var allPagesFilter = LastEditedFilterHelper.GetLastEditedFilter(timeProvider, TimeSpan.Zero);
    var allPages = await client
        .QueryDatabaseAsync(options.DatabaseId, allPagesFilter, cancellationToken)
        .ConfigureAwait(false);

    foreach (var page in allPages) {
      var titlePropertyParts = page.Properties.Values
          .Select(p => (p as TitlePropertyValue)?.Title.Select(t => t.PlainText))
          .FirstOrDefault(p => p != null);
      var title = titlePropertyParts == null ? "(unknown)" : string.Join("", titlePropertyParts);

      page.Properties.TryGetValue(options.PropertyName, out var prop);

      if (prop is RelationPropertyValue relationPropertyValue) {
        logger.LogInformation(
            "Page '{Id}' ('{Title}') - property {Name} = '{RelatedPageIds}'",
            page.Id,
            title,
            options.PropertyName,
            string.Join(", ", relationPropertyValue.Relation.Select(i => i.Id)));
      } else if (prop is not null) {
        logger.LogInformation(
            "Page '{Id}' ('{Title}') - property {Name} has incorrect type '{Type}'",
            page.Id,
            title,
            options.PropertyName,
            prop.Type);
      } else {
        logger.LogInformation(
            "Page '{Id}' ('{Title}') - property {Name} not found",
            page.Id,
            title,
            options.PropertyName);
      }
    }
  }
}
