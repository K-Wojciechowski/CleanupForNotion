using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;

namespace CleanupForNotion.Core.Infrastructure.Plugins;

public interface IPlugin {
  public string Name { get; }

  public string Description { get; }

  Task Run(ICfnNotionClient client, GlobalOptions globalOptions, CancellationToken cancellationToken);
}
