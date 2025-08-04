using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;
using Notion.Client;

namespace CleanupForNotion.Core.Plugins.Implementations;

public class DeleteByCheckbox(
  ILogger<DeletePluginBase<DeleteByCheckboxOptions>> logger,
  IPluginStateProvider pluginStateProvider,
  TimeProvider timeProvider,
  DeleteByCheckboxOptions options,
  string pluginDescription)
  : DeletePluginBase<DeleteByCheckboxOptions>(logger, pluginStateProvider, timeProvider, options, pluginDescription) {

  public override string Name => "DeleteByCheckbox";

  public override async Task Run(
    ICfnNotionClient client,
    GlobalOptions globalOptions,
    CancellationToken cancellationToken) {
    await DoDelete(
        client,
        globalOptions,
        [new CheckboxFilter(Options.PropertyName, Options.DeleteIfChecked)],
        cancellationToken).ConfigureAwait(false);
  }
}
