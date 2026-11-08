using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Plugins.Options;

namespace CleanupForNotion.Core.Infrastructure.Plugins;

public interface IPlugin : IPluginIdentifiers {
  public IBasicPluginOptions Configuration { get; }

  Task Run(ICfnNotionClient client, GlobalOptions globalOptions, CancellationToken cancellationToken);
}
