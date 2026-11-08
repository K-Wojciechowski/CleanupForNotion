using CleanupForNotion.Core.Infrastructure.Plugins;

namespace CleanupForNotion.Core.Infrastructure.State;

public interface IPluginStateProvider {
  public Task<string?> GetString(IPluginIdentifiers pluginIdentifiers, string key, CancellationToken cancellationToken);

  public Task SetString(IPluginIdentifiers pluginIdentifiers, string key, string value, CancellationToken cancellationToken);

  public Task Remove(IPluginIdentifiers pluginIdentifiers, string key, CancellationToken cancellationToken);
}
