using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Plugins;

namespace CleanupForNotion.Core.Infrastructure.PluginManagement;

public interface IPluginActivator {
  IPlugin ActivatePlugin(PluginSpecification specification);
}
