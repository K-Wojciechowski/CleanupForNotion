using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using CleanupForNotion.Aws.DynamoDb;
using CleanupForNotion.Core.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CleanupForNotion.Aws.Test;

[TestClass]
public class AwsIntegrationTests {
  [ClassInitialize]
  public static async Task ClassInitialize(TestContext testContext) {
    if (Environment.GetEnvironmentVariable("CI") == "true") {
      Assert.Inconclusive("Integration tests cannot run in CI");
    }

    try {
      var client = new AmazonSecurityTokenServiceClient();
      await client.GetCallerIdentityAsync(new GetCallerIdentityRequest(), testContext.CancellationTokenSource.Token).ConfigureAwait(false);
    } catch (Exception exc) {
      Assert.Inconclusive($"AWS credentials not configured ({exc})");
    }
  }

  [TestMethod]
  public async Task TestDynamoDbPluginStateProvider() {
    var dynamoDbClient = new AmazonDynamoDBClient();
    var tableName = $"cfntest{Guid.NewGuid()}";

    // Create table
    try {
      await dynamoDbClient.CreateTableAsync(new CreateTableRequest {
              TableName = tableName,
              KeySchema = [
                  new KeySchemaElement(DynamoDbConstants.PartitionKeyName, KeyType.HASH),
                  new KeySchemaElement(DynamoDbConstants.SortKeyName, KeyType.RANGE)
              ],
              AttributeDefinitions = [
                  new AttributeDefinition(DynamoDbConstants.PartitionKeyName, ScalarAttributeType.S),
                  new AttributeDefinition(DynamoDbConstants.SortKeyName, ScalarAttributeType.S)
              ],
              BillingMode = BillingMode.PAY_PER_REQUEST
          }
      ).ConfigureAwait(false);

      // Wait for table to be created
      do {
        try {
          var table = await dynamoDbClient.DescribeTableAsync(tableName).ConfigureAwait(false);
          if (table.Table.TableStatus == TableStatus.ACTIVE) break;
        } catch (ResourceNotFoundException) { }

        await Task.Delay(1000).ConfigureAwait(false);
      } while (true);

      // Create some test rows
      await CreateDynamoDbRow(dynamoDbClient, tableName, "N1", "D1", "K1", "V1").ConfigureAwait(false);
      await CreateDynamoDbRow(dynamoDbClient, tableName, "N2", "D2", "K2", "V2").ConfigureAwait(false);
      await CreateDynamoDbRow(dynamoDbClient, tableName, "N3", "D3", "K3", "V3").ConfigureAwait(false);

      // Create state provider
      var logger = new NullLogger<DynamoDbPluginStateProvider>();
      var notificationSender = new NotificationSender();
      var options = new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = tableName });
      var stateProvider = new DynamoDbPluginStateProvider(dynamoDbClient, logger, notificationSender, options);

      // Create second state provider
      var stateProvider2 = new DynamoDbPluginStateProvider(dynamoDbClient, logger, notificationSender, options);

      // Read
      (await stateProvider.GetString("N1", "D1", "K1", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V1");
      (await stateProvider.GetString("N2", "D2", "K2", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V2");
      (await stateProvider.GetString("N3", "D3", "K3", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V3");
      (await stateProvider.GetString("N1", "D1", "K4", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();

      // Write
      await stateProvider.SetString("N1", "D1", "K4", "V4", CancellationToken.None).ConfigureAwait(false);
      await stateProvider.SetString("N2", "D2", "K2", "V5", CancellationToken.None).ConfigureAwait(false);
      await stateProvider.Remove("N3", "D3", "K3", CancellationToken.None).ConfigureAwait(false);

      // First state provider sees new data
      (await stateProvider.GetString("N1", "D1", "K1", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V1");
      (await stateProvider.GetString("N2", "D2", "K2", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V5");
      (await stateProvider.GetString("N3", "D3", "K3", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
      (await stateProvider.GetString("N1", "D1", "K4", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V4");

      // Database still contains old data
      (await stateProvider2.GetString("N1", "D1", "K1", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V1");
      (await stateProvider2.GetString("N2", "D2", "K2", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V2");
      (await stateProvider2.GetString("N3", "D3", "K3", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V3");
      (await stateProvider2.GetString("N1", "D1", "K4", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();

      // Save
      await stateProvider.OnRunFinished(dryRun: false, CancellationToken.None).ConfigureAwait(false);

      // Read again
      (await stateProvider.GetString("N1", "D1", "K1", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V1");
      (await stateProvider.GetString("N2", "D2", "K2", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V5");
      (await stateProvider.GetString("N3", "D3", "K3", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
      (await stateProvider.GetString("N1", "D1", "K4", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V4");

      // Both providers should return the same
      (await stateProvider2.GetString("N1", "D1", "K1", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V1");
      (await stateProvider2.GetString("N2", "D2", "K2", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V5");
      (await stateProvider2.GetString("N3", "D3", "K3", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
      (await stateProvider2.GetString("N1", "D1", "K4", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V4");
    } finally {
      await dynamoDbClient.DeleteTableAsync(tableName).ConfigureAwait(false);
    }
  }

  [TestMethod]
  public async Task TestS3Configuration()
    => await S3TestBase.TestS3Configuration(new AmazonS3Client()).ConfigureAwait(false);

  private static async Task CreateDynamoDbRow(
      AmazonDynamoDBClient dynamoDbClient,
      string tableName,
      string pluginName,
      string pluginDescription,
      string sortKey,
      string value) {
    await dynamoDbClient.PutItemAsync(tableName,
        new Dictionary<string, AttributeValue>() {
            { DynamoDbConstants.PartitionKeyName, new AttributeValue(pluginName + "::" + pluginDescription) },
            { DynamoDbConstants.SortKeyName, new AttributeValue(sortKey) },
            { DynamoDbConstants.ValueKeyName, new AttributeValue(value) }
        }).ConfigureAwait(false);
  }
}
