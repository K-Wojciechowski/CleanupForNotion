using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Infrastructure.Time;
using CleanupForNotion.Core.Infrastructure.Traceability;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;
using Notion.Client;

namespace CleanupForNotion.Core.Plugins;

public abstract class DeletePluginBase<TOptions> : IPlugin
    where TOptions : IBasicPluginOptions {
  public abstract string Name { get; }
  public string Description { get; }

  protected readonly TOptions Options;
  protected readonly ILogger<DeletePluginBase<TOptions>> Logger;
  protected readonly IPluginStateProvider PluginStateProvider;
  protected readonly TimeProvider TimeProvider;

  protected DeletePluginBase(
      ILogger<DeletePluginBase<TOptions>> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      TOptions options,
      string description) {
    Logger = logger;
    PluginStateProvider = pluginStateProvider;
    TimeProvider = timeProvider;
    Description = description;
    Options = options;
  }

  public abstract Task Run(ICfnNotionClient client, GlobalOptions globalOptions, CancellationToken cancellationToken);

  protected async Task DoDelete(
      ICfnNotionClient client,
      GlobalOptions globalOptions,
      List<Filter> filters,
      CancellationToken cancellationToken) {
    var filter = LastEditedFilterHelper.GetCompoundFilterWithLastEdited(TimeProvider, Options, filters);

    Logger.LogFilters(filter);
    var pageIds = await client.QueryDatabaseIdsAsync(Options.DatabaseId, filter, cancellationToken).ConfigureAwait(false);

    await DoDelete(client, globalOptions, pageIds, cancellationToken).ConfigureAwait(false);
  }

  protected async Task DoDelete(
      ICfnNotionClient client,
      GlobalOptions globalOptions,
      IReadOnlyCollection<string> pageIds,
      CancellationToken cancellationToken) {
    Logger.LogInformation("Pages to delete: {Count}", pageIds.Count);

    foreach (var pageId in pageIds) {
      Logger.LogInformation("Deleting page '{Id}'", pageId);
      if (!globalOptions.DryRun) {
        await client.DeletePageAsync(pageId, cancellationToken).ConfigureAwait(false);
      }
    }
  }
}
