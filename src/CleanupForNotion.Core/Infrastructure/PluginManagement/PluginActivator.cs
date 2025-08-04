using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Plugins;

namespace CleanupForNotion.Core.Infrastructure.PluginManagement;

public class PluginActivator : IPluginActivator {
  private readonly Dictionary<string, IPluginProvider> _providers;

  public PluginActivator(IEnumerable<IPluginProvider> providers) {
    _providers = providers.ToDictionary(p => p.Name, p => p);
  }

  public IPlugin ActivatePlugin(PluginSpecification specification) {
    var pluginProvider = _providers.GetValueOrDefault(specification.PluginName);

    if (pluginProvider == null) {
      throw new InvalidConfigurationException($"The plugin provider for '{specification.PluginName}' does not exist.");
    }

    return pluginProvider.GetPlugin(specification);
  }
}
