using CleanupForNotion.Core.Infrastructure.ConfigModels;

namespace CleanupForNotion.Core.Infrastructure.PluginManagement;

public class PluginSpecificationParser : IPluginSpecificationParser {
  public PluginSpecification ParseSpecification(Dictionary<string, object> rawSpecification, int pluginIndex) => new(
      PluginIndex: pluginIndex,
      PluginName: Get(rawSpecification, "PluginName"),
      PluginDescription: Get(rawSpecification, "PluginDescription"),
      GroupId: rawSpecification.GetValueOrDefault("GroupId") as string,
      RawOptions: new RawPluginOptions(rawSpecification));

  private static string Get(Dictionary<string, object> rawSpecification, string key) =>
    rawSpecification.GetValueOrDefault(key) as string ?? throw new InvalidConfigurationException($"{key} is required");
}
