using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;
using Notion.Client;

namespace CleanupForNotion.Core.Plugins.Implementations;

public class DeleteWithoutRelationships(
  ILogger<DeletePluginBase<DeleteWithoutRelationshipsOptions>> logger,
  IPluginStateProvider pluginStateProvider,
  TimeProvider timeProvider,
  DeleteWithoutRelationshipsOptions options,
  string pluginDescription)
  : DeletePluginBase<DeleteWithoutRelationshipsOptions>(logger, pluginStateProvider, timeProvider, options, pluginDescription) {

  public override string Name => "DeleteWithoutRelationships";

  public override async Task Run(
    ICfnNotionClient client,
    GlobalOptions globalOptions,
    CancellationToken cancellationToken) {
    await DoDelete(
        client,
        globalOptions,
        [new RelationFilter(Options.PropertyName, isEmpty: true)],
        cancellationToken).ConfigureAwait(false);
  }
}
