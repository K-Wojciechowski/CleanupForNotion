using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Notion.Client;

namespace CleanupForNotion.Core.Plugins;

public abstract class DeletePluginBase<TOptions> : IPlugin
    where TOptions : IDeletePluginOptions {
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
    var lastEditedBefore = TimeProvider.GetUtcNow() - Options.GracePeriodWithFallback;
    var filter = new CompoundFilter(and: [
        new TimestampLastEditedTimeFilter(onOrBefore: lastEditedBefore.DateTime),
        .. filters
    ]);
    // Not a fan of Newtonsoft.Json, but Notion.Client is using that, so we must use it too to get correct output
    Logger.LogInformation("Searching for pages to delete with filters: {Filters}", JsonConvert.SerializeObject(filter));
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
