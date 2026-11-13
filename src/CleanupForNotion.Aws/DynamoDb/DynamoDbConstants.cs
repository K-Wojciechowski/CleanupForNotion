using Amazon.DynamoDBv2.Model;

namespace CleanupForNotion.Aws.DynamoDb;

internal static class DynamoDbConstants {
  public const string PartitionKeyName = "Plugin";
  public const string SortKeyName = "ConfigKey";
  public const string ValueKeyName = "Value";
  public const string ConditionLockNotPresent = "attribute_not_exists (Plugin)";
  public const string ConditionLockEquals = "#val = :val";
  public const string ValueKeyAttributeKeyName = "#val";
  public const string ValueKeyAttributeValueName = ":val";
  public static readonly AttributeValue LockKeyValue = new("$lock$");
  public static readonly TimeSpan LockWaitTime = TimeSpan.FromSeconds(15);
  public static readonly TimeSpan LockAbandonTime = TimeSpan.FromMinutes(5);
}
