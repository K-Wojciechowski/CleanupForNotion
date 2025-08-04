using CleanupForNotion.Core.Infrastructure.ConfigModels;

namespace CleanupForNotion.Core.Infrastructure.PluginManagement;

public interface IPluginSpecificationParser {
  PluginSpecification ParseSpecification(Dictionary<string, object> rawSpecification);
}
