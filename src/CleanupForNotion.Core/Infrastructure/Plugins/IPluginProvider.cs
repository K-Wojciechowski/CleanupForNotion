using CleanupForNotion.Core.Infrastructure.ConfigModels;

namespace CleanupForNotion.Core.Infrastructure.Plugins;

public interface IPluginProvider {
  string Name { get; }

  IPlugin GetPlugin(PluginSpecification pluginSpecification);
}
