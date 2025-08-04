namespace CleanupForNotion.Core.Infrastructure.State;

public interface IPluginStateProvider {
  public Task<string?> GetString(string pluginName, string pluginDescription, string key, CancellationToken cancellationToken);

  public Task SetString(string pluginName, string pluginDescription, string key, string value, CancellationToken cancellationToken);

  public Task Remove(string pluginName, string pluginDescription, string key, CancellationToken cancellationToken);
}
