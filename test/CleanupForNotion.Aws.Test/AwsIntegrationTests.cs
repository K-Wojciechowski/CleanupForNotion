using System.Diagnostics;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using CleanupForNotion.Aws.DynamoDb;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
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
      (await stateProvider.GetString(new P("N1", "D1"), "K1", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V1");
      (await stateProvider.GetString(new P("N2", "D2"), "K2", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V2");
      (await stateProvider.GetString(new P("N3", "D3"), "K3", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V3");
      (await stateProvider.GetString(new P("N1", "D1"), "K4", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();

      // Write
      await stateProvider.SetString(new P("N1", "D1"), "K4", "V4", CancellationToken.None).ConfigureAwait(false);
      await stateProvider.SetString(new P("N2", "D2"), "K2", "V5", CancellationToken.None).ConfigureAwait(false);
      await stateProvider.Remove(new P("N3", "D3"), "K3", CancellationToken.None).ConfigureAwait(false);

      // First state provider sees new data
      (await stateProvider.GetString(new P("N1", "D1"), "K1", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V1");
      (await stateProvider.GetString(new P("N2", "D2"), "K2", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V5");
      (await stateProvider.GetString(new P("N3", "D3"), "K3", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
      (await stateProvider.GetString(new P("N1", "D1"), "K4", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V4");

      // Database still contains old data
      (await stateProvider2.GetString(new P("N1", "D1"), "K1", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V1");
      (await stateProvider2.GetString(new P("N2", "D2"), "K2", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V2");
      (await stateProvider2.GetString(new P("N3", "D3"), "K3", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V3");
      (await stateProvider2.GetString(new P("N1", "D1"), "K4", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();

      // Save
      await stateProvider.OnRunFinished(dryRun: false, CancellationToken.None).ConfigureAwait(false);

      // Read again
      (await stateProvider.GetString(new P("N1", "D1"), "K1", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V1");
      (await stateProvider.GetString(new P("N2", "D2"), "K2", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V5");
      (await stateProvider.GetString(new P("N3", "D3"), "K3", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
      (await stateProvider.GetString(new P("N1", "D1"), "K4", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V4");

      // Both providers should return the same
      (await stateProvider2.GetString(new P("N1", "D1"), "K1", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V1");
      (await stateProvider2.GetString(new P("N2", "D2"), "K2", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V5");
      (await stateProvider2.GetString(new P("N3", "D3"), "K3", CancellationToken.None).ConfigureAwait(false)).ShouldBeNull();
      (await stateProvider2.GetString(new P("N1", "D1"), "K4", CancellationToken.None).ConfigureAwait(false)).ShouldBe("V4");
    } finally {
      await dynamoDbClient.DeleteTableAsync(tableName, CancellationToken.None).ConfigureAwait(false);
    }
  }

  [TestMethod]
  public async Task TestDynamoDbLockManager() {
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

      // Create lock manager
      var logger = new NullLogger<DynamoDbLockManager>();
      var options = new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = tableName });
      var timeProvider = new FakeTimeProvider();
      var lockManager = new DynamoDbLockManager(dynamoDbClient, logger, options, timeProvider);

      var stopwatch = Stopwatch.StartNew();

      // Lock
      var firstLockToken = await lockManager.AcquireLock(CancellationToken.None).ConfigureAwait(false);
      stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000);

      // Unlock
      (await lockManager.ReleaseLock(firstLockToken).ConfigureAwait(false)).ShouldBeTrue();

      // Cannot unlock again
      (await lockManager.ReleaseLock(firstLockToken).ConfigureAwait(false)).ShouldBeFalse();

      // Second lock
      var secondLockToken = await lockManager.AcquireLock(CancellationToken.None).ConfigureAwait(false);

      stopwatch.Restart();

      // Cannot unlock old lock
      (await lockManager.ReleaseLock(firstLockToken).ConfigureAwait(false)).ShouldBeFalse();

      // Try another lock
      Func<Task> act = async () => await lockManager.AcquireLock(CancellationToken.None).ConfigureAwait(false);
      var exc = await act.ShouldThrowAsync<Exception>().ConfigureAwait(false);
      exc.Message.ShouldBe("Failed to acquire lock");
      stopwatch.Elapsed.ShouldBeGreaterThan(DynamoDbConstants.LockWaitTime - TimeSpan.FromSeconds(1));

      // Try again
      var thirdLockTask = lockManager.AcquireLock(CancellationToken.None);

      // But unlock this time
      await Task.Delay(5000, CancellationToken.None).ConfigureAwait(false);
      thirdLockTask.IsCompleted.ShouldBeFalse();
      thirdLockTask.IsFaulted.ShouldBeFalse();
      await lockManager.ReleaseLock(secondLockToken).ConfigureAwait(false);
      var thirdLockToken = await thirdLockTask.ConfigureAwait(false);

      // Advance time
      timeProvider.Advance(TimeSpan.FromSeconds(1));
      timeProvider.Advance(DynamoDbConstants.LockAbandonTime);

      // Should steal the lock
      stopwatch.Restart();
      var fourthLockToken = await lockManager.AcquireLock(CancellationToken.None).ConfigureAwait(false);
      stopwatch.ElapsedMilliseconds.ShouldBeLessThan(2000);


      (await lockManager.ReleaseLock(thirdLockToken).ConfigureAwait(false)).ShouldBeFalse();
      (await lockManager.ReleaseLock(fourthLockToken).ConfigureAwait(false)).ShouldBeTrue();
    } finally {
      await dynamoDbClient.DeleteTableAsync(tableName, CancellationToken.None).ConfigureAwait(false);
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

  private record P(string Name, string Description, string? Id = null) : IPluginIdentifiers;
}
