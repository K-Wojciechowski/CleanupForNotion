using Amazon.DynamoDBv2.Model;
using CleanupForNotion.Core.Infrastructure.Plugins;

namespace CleanupForNotion.Aws.DynamoDb;

internal record DynamoDbStateKey(string PluginName, string PluginDescription, string ConfigKey) {
  public string PartitionKey => PluginName + "::" + PluginDescription;
  public string SortKey => ConfigKey;

  public DynamoDbStateKey(IPluginIdentifiers pluginIdentifiers, string configKey)
      : this(pluginIdentifiers.Name, pluginIdentifiers.Description, configKey) {
  }

  public Dictionary<string, AttributeValue> ToDictionary() =>
      new() {
          { DynamoDbConstants.PartitionKeyName, new AttributeValue(s: PartitionKey) },
          { DynamoDbConstants.SortKeyName, new AttributeValue(s: SortKey) }
      };

  public Dictionary<string, AttributeValue> ToDictionary(string value) =>
      new() {
          { DynamoDbConstants.PartitionKeyName, new AttributeValue(s: PartitionKey) },
          { DynamoDbConstants.SortKeyName, new AttributeValue(s: SortKey) },
          { DynamoDbConstants.ValueKeyName, new AttributeValue(s: value) }
      };
}
