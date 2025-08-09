using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CleanupForNotion.Aws.DynamoDb;
using CleanupForNotion.Core.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace CleanupForNotion.Aws.Test;

[TestClass]
public class DynamoDbPluginStateProviderUnitTests {
  private const string TableName = "CfnState";

  private static readonly IOptions<DynamoDbOptions> _options =
      new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = TableName });

  [TestMethod]
  public void Constructor_Called_RegistersWithNotificationSender() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();

    // Act
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    // Assert
    notificationSender.Received().Register(provider);
  }

  [TestMethod]
  public void Dispose_Called_UnregistersFromNotificationSenderAndDisposesSemaphore() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    // Act
    provider.Dispose();

    // Assert
    notificationSender.Received().Unregister(provider);
  }

  [TestMethod]
  public async Task GetString_CalledWithPresentKey_GetsValueFromDynamoDb() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var value = Guid.NewGuid().ToString();
    var partitionKey = pluginName + "::" + pluginDescription;
    client.GetItemAsync(
            Arg.Is<GetItemRequest>(r =>
                r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
                r.Key[DynamoDbConstants.SortKeyName].S == key),
            Arg.Any<CancellationToken>())
        .Returns(new GetItemResponse {
            Item = new Dictionary<string, AttributeValue> {
                { DynamoDbConstants.PartitionKeyName, new AttributeValue(partitionKey) },
                { DynamoDbConstants.SortKeyName, new AttributeValue(key) },
                { DynamoDbConstants.ValueKeyName, new AttributeValue(value) }
            }
        });

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    // Act
    var result = await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None)
        .ConfigureAwait(false);
    var result2 = await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None)
        .ConfigureAwait(false);

    // Assert
    result.ShouldBe(value);
    result2.ShouldBe(value);
    await client.Received(2).GetItemAsync(
        Arg.Is<GetItemRequest>(r =>
            r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
            r.Key[DynamoDbConstants.SortKeyName].S == key),
        Arg.Any<CancellationToken>()).ConfigureAwait(false);

    var logRecords = logger.Collector.GetSnapshot();
    logRecords[0].Message.ShouldBe($"Retrieving state item with partition key '{partitionKey}' and sort key '{key}'");
    logRecords[1].Message.ShouldBe(
        $"Retrieved state item with partition key '{partitionKey}' and sort key '{key}' with value '{value}'");
    logRecords[2].Message.ShouldBe(logRecords[0].Message);
    logRecords[3].Message.ShouldBe(logRecords[1].Message);
  }

  [TestMethod]
  public async Task GetString_CalledWithMissingKey_ReturnsNull() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var partitionKey = pluginName + "::" + pluginDescription;
    client.GetItemAsync(
            Arg.Is<GetItemRequest>(r =>
                r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
                r.Key[DynamoDbConstants.SortKeyName].S == key),
            Arg.Any<CancellationToken>())
        .Returns(new GetItemResponse { Item = null });

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    // Act
    var result = await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None)
        .ConfigureAwait(false);
    var result2 = await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None)
        .ConfigureAwait(false);

    // Assert
    result.ShouldBeNull();
    result2.ShouldBeNull();
    await client.Received(2).GetItemAsync(
        Arg.Is<GetItemRequest>(r =>
            r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
            r.Key[DynamoDbConstants.SortKeyName].S == key),
        Arg.Any<CancellationToken>()).ConfigureAwait(false);

    var logRecords = logger.Collector.GetSnapshot();
    logRecords[0].Message.ShouldBe($"Retrieving state item with partition key '{partitionKey}' and sort key '{key}'");
    logRecords[1].Message.ShouldBe(
        $"Could not find state item with partition key '{partitionKey}' and sort key '{key}', returning null");
    logRecords[2].Message.ShouldBe(logRecords[0].Message);
    logRecords[3].Message.ShouldBe(logRecords[1].Message);
  }

  [TestMethod]
  public async Task GetString_DynamoDbThrows_RetriesAutomatically() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var value = Guid.NewGuid().ToString();
    var partitionKey = pluginName + "::" + pluginDescription;
    client.GetItemAsync(
            Arg.Is<GetItemRequest>(r =>
                r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
                r.Key[DynamoDbConstants.SortKeyName].S == key),
            Arg.Any<CancellationToken>())
        .Returns(
            _ => throw new Exception("try again"),
            _ => throw new Exception("try once more"),
            _ => new GetItemResponse {
                Item = new Dictionary<string, AttributeValue> {
                    { DynamoDbConstants.PartitionKeyName, new AttributeValue(partitionKey) },
                    { DynamoDbConstants.SortKeyName, new AttributeValue(key) },
                    { DynamoDbConstants.ValueKeyName, new AttributeValue(value) }
                }
            });

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    // Act
    var result = await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None)
        .ConfigureAwait(false);

    // Assert
    result.ShouldBe(value);
    await client.Received(3).GetItemAsync(
        Arg.Is<GetItemRequest>(r =>
            r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
            r.Key[DynamoDbConstants.SortKeyName].S == key),
        Arg.Any<CancellationToken>()).ConfigureAwait(false);

    var logRecords = logger.Collector.GetSnapshot();
    logRecords[0].Message.ShouldBe($"Retrieving state item with partition key '{partitionKey}' and sort key '{key}'");
    logRecords[1].Message.ShouldBe(
        $"Retrieved state item with partition key '{partitionKey}' and sort key '{key}' with value '{value}'");
  }

  [TestMethod]
  public async Task GetString_DynamoDbThrowsForever_ThrowsException() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var partitionKey = pluginName + "::" + pluginDescription;
    var exception = new Exception("nope");
    client.GetItemAsync(
            Arg.Is<GetItemRequest>(r =>
                r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
                r.Key[DynamoDbConstants.SortKeyName].S == key),
            Arg.Any<CancellationToken>())
        .Throws(exception);

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    // Act
    Func<Task> act = async () => await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None)
        .ConfigureAwait(false);

    // Assert
    (await act.ShouldThrowAsync<Exception>().ConfigureAwait(false)).ShouldBe(exception);
    await client.Received(5).GetItemAsync(
        Arg.Is<GetItemRequest>(r =>
            r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
            r.Key[DynamoDbConstants.SortKeyName].S == key),
        Arg.Any<CancellationToken>()).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task SetString_Called_DoesNotWriteImmediately() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var value = Guid.NewGuid().ToString();

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    // Act
    await provider.SetString(pluginName, pluginDescription, key, value, CancellationToken.None).ConfigureAwait(false);

    // Assert
    client.ReceivedCalls().ShouldBeEmpty();
  }

  [TestMethod]
  public async Task Remove_Called_DoesNotWriteImmediately() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    // Act
    await provider.Remove(pluginName, pluginDescription, key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    client.ReceivedCalls().ShouldBeEmpty();
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public async Task GetString_AfterSetString_RetrievesFromCache(bool initialReadIsNull) {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var value = initialReadIsNull ? null : Guid.NewGuid().ToString();
    var value2 = Guid.NewGuid().ToString();
    var partitionKey = pluginName + "::" + pluginDescription;
    client.GetItemAsync(
            Arg.Is<GetItemRequest>(r =>
                r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
                r.Key[DynamoDbConstants.SortKeyName].S == key),
            Arg.Any<CancellationToken>())
        .Returns(new GetItemResponse {
            Item = initialReadIsNull
                ? null
                : new Dictionary<string, AttributeValue> {
                    { DynamoDbConstants.PartitionKeyName, new AttributeValue(partitionKey) },
                    { DynamoDbConstants.SortKeyName, new AttributeValue(key) },
                    { DynamoDbConstants.ValueKeyName, new AttributeValue(value) }
                }
        });

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    // Act
    var result = await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None)
        .ConfigureAwait(false);
    await provider.SetString(pluginName, pluginDescription, key, value2, CancellationToken.None).ConfigureAwait(false);
    var result2 = await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None)
        .ConfigureAwait(false);

    // Assert
    result.ShouldBe(value);
    result2.ShouldBe(value2);
    await client.Received(1).GetItemAsync(
        Arg.Is<GetItemRequest>(r =>
            r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
            r.Key[DynamoDbConstants.SortKeyName].S == key),
        Arg.Any<CancellationToken>()).ConfigureAwait(false);

    var logRecords = logger.Collector.GetSnapshot();
    logRecords[0].Message.ShouldBe($"Retrieving state item with partition key '{partitionKey}' and sort key '{key}'");
    logRecords[1].Message.ShouldNotStartWith("Returning");
    logRecords[2].Message.ShouldBe(
        $"Returning state item with partition key '{partitionKey}' and sort key '{key}' from pending write cache with value '{value2}'");
  }

  [TestMethod]
  public async Task GetString_AfterRemove_ReturnsNull() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var value = Guid.NewGuid().ToString();
    var partitionKey = pluginName + "::" + pluginDescription;
    client.GetItemAsync(
            Arg.Is<GetItemRequest>(r =>
                r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
                r.Key[DynamoDbConstants.SortKeyName].S == key),
            Arg.Any<CancellationToken>())
        .Returns(new GetItemResponse {
            Item = new Dictionary<string, AttributeValue> {
                { DynamoDbConstants.PartitionKeyName, new AttributeValue(partitionKey) },
                { DynamoDbConstants.SortKeyName, new AttributeValue(key) },
                { DynamoDbConstants.ValueKeyName, new AttributeValue(value) }
            }
        });

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    // Act
    var result = await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None)
        .ConfigureAwait(false);
    await provider.Remove(pluginName, pluginDescription, key, CancellationToken.None).ConfigureAwait(false);
    var result2 = await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None)
        .ConfigureAwait(false);

    // Assert
    result.ShouldBe(value);
    result2.ShouldBeNull();
    await client.Received(1).GetItemAsync(
        Arg.Is<GetItemRequest>(r =>
            r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
            r.Key[DynamoDbConstants.SortKeyName].S == key),
        Arg.Any<CancellationToken>()).ConfigureAwait(false);

    var logRecords = logger.Collector.GetSnapshot();
    logRecords[0].Message.ShouldBe($"Retrieving state item with partition key '{partitionKey}' and sort key '{key}'");
    logRecords[1].Message.ShouldBe(
        $"Retrieved state item with partition key '{partitionKey}' and sort key '{key}' with value '{value}'");
    logRecords[2].Message.ShouldBe(
        $"Returning state item with partition key '{partitionKey}' and sort key '{key}' from pending write cache with value '(null)'");
  }

  [TestMethod]
  public async Task OnRunFinished_DryRun_DoesNothing() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var value = Guid.NewGuid().ToString();

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    await provider.SetString(pluginName, pluginDescription, key, value, CancellationToken.None).ConfigureAwait(false);

    // Act
    await provider.OnRunFinished(dryRun: true, CancellationToken.None).ConfigureAwait(false);

    // Assert
    client.ReceivedCalls().ShouldBeEmpty();
  }

  [TestMethod]
  public async Task OnRunFinished_NoChanges_DoesNothing() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var value = Guid.NewGuid().ToString();
    var partitionKey = pluginName + "::" + pluginDescription;
    client.GetItemAsync(
            Arg.Is<GetItemRequest>(r =>
                r.TableName == TableName && r.Key[DynamoDbConstants.PartitionKeyName].S == partitionKey &&
                r.Key[DynamoDbConstants.SortKeyName].S == key),
            Arg.Any<CancellationToken>())
        .Returns(new GetItemResponse {
            Item = new Dictionary<string, AttributeValue> {
                { DynamoDbConstants.PartitionKeyName, new AttributeValue(partitionKey) },
                { DynamoDbConstants.SortKeyName, new AttributeValue(key) },
                { DynamoDbConstants.ValueKeyName, new AttributeValue(value) }
            }
        });

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    await provider.GetString(pluginName, pluginDescription, key, CancellationToken.None).ConfigureAwait(false);

    // Act
    await provider.OnRunFinished(dryRun: false, CancellationToken.None).ConfigureAwait(false);

    // Assert
    client.ReceivedCalls().ShouldHaveSingleItem()
        .GetMethodInfo().Name.ShouldBe("GetItemAsync");
  }

  [TestMethod]
  public async Task OnRunFinished_OneSetStringChange_WritesToDynamoDb() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var value = Guid.NewGuid().ToString();
    var partitionKey = pluginName + "::" + pluginDescription;

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    client.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .Returns(new BatchWriteItemResponse { UnprocessedItems = null });

    await provider.SetString(pluginName, pluginDescription, key, value, CancellationToken.None).ConfigureAwait(false);

    // Act
    await provider.OnRunFinished(dryRun: false, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await client.Received(1).BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    var request = client.ReceivedCalls().First().GetArguments()[0].ShouldBeOfType<BatchWriteItemRequest>();
    request.RequestItems.Keys.ShouldHaveSingleItem(TableName);
    var writeRequest = request.RequestItems[TableName].ShouldHaveSingleItem();
    writeRequest.DeleteRequest.ShouldBeNull();
    var putRequest = writeRequest.PutRequest.ShouldNotBeNull();
    putRequest.Item.Count.ShouldBe(3);
    putRequest.Item[DynamoDbConstants.PartitionKeyName].S.ShouldBe(partitionKey);
    putRequest.Item[DynamoDbConstants.SortKeyName].S.ShouldBe(key);
    putRequest.Item[DynamoDbConstants.ValueKeyName].S.ShouldBe(value);
  }

  [TestMethod]
  public async Task OnRunFinished_OneRemoveChange_WritesToDynamoDb() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();
    var partitionKey = pluginName + "::" + pluginDescription;

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    client.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .Returns(new BatchWriteItemResponse { UnprocessedItems = null });

    await provider.Remove(pluginName, pluginDescription, key, CancellationToken.None).ConfigureAwait(false);

    // Act
    await provider.OnRunFinished(dryRun: false, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await client.Received(1).BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    var request = client.ReceivedCalls().First().GetArguments()[0].ShouldBeOfType<BatchWriteItemRequest>();
    request.RequestItems.Keys.ShouldHaveSingleItem(TableName);
    var writeRequest = request.RequestItems[TableName].ShouldHaveSingleItem();
    writeRequest.PutRequest.ShouldBeNull();
    var deleteRequest = writeRequest.DeleteRequest.ShouldNotBeNull();
    deleteRequest.Key.Count.ShouldBe(2);
    deleteRequest.Key[DynamoDbConstants.PartitionKeyName].S.ShouldBe(partitionKey);
    deleteRequest.Key[DynamoDbConstants.SortKeyName].S.ShouldBe(key);
  }

  [TestMethod]
  public async Task OnRunFinished_ManyChanges_WritesToDynamoDbInBatchesOf25Items() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    var expectedPutRequests = new List<Dictionary<string, string>>();
    var expectedDeleteRequests = new List<Dictionary<string, string>>();

    client.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .Returns(new BatchWriteItemResponse { UnprocessedItems = null });

    for (int i = 0; i < 80; i++) {
      var pluginName = Guid.NewGuid().ToString();
      var pluginDescription = Guid.NewGuid().ToString();
      var key = Guid.NewGuid().ToString();
      var partitionKey = pluginName + "::" + pluginDescription;
      var value = Guid.NewGuid().ToString();
      if (i % 3 == 2) {
        expectedDeleteRequests.Add(new Dictionary<string, string> {
            { DynamoDbConstants.PartitionKeyName, partitionKey }, { DynamoDbConstants.SortKeyName, key }
        });
        await provider.Remove(pluginName, pluginDescription, key, CancellationToken.None).ConfigureAwait(false);
      } else {
        expectedPutRequests.Add(new Dictionary<string, string> {
            { DynamoDbConstants.PartitionKeyName, partitionKey },
            { DynamoDbConstants.SortKeyName, key },
            { DynamoDbConstants.ValueKeyName, value }
        });
        await provider.SetString(pluginName, pluginDescription, key, value, CancellationToken.None)
            .ConfigureAwait(false);
      }
    }

    // Act
    await provider.OnRunFinished(dryRun: false, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await client.Received(4).BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    var receivedPutRequests = new List<Dictionary<string, string>>();
    var receivedDeleteRequests = new List<Dictionary<string, string>>();

    foreach (var call in client.ReceivedCalls()) {
      var request = call.GetArguments()[0].ShouldBeOfType<BatchWriteItemRequest>();
      request.RequestItems.Keys.ShouldHaveSingleItem(TableName);
      request.RequestItems[TableName].Count.ShouldBeLessThanOrEqualTo(25);
      foreach (var writeRequest in request.RequestItems[TableName]) {
        if (writeRequest.PutRequest != null) {
          receivedPutRequests.Add(writeRequest.PutRequest.Item.ToDictionary(kv => kv.Key, kv => kv.Value.S));
        } else if (writeRequest.DeleteRequest != null) {
          receivedDeleteRequests.Add(writeRequest.DeleteRequest.Key.ToDictionary(kv => kv.Key, kv => kv.Value.S));
        } else {
          Assert.Fail("WriteRequest does not have content");
        }
      }
    }

    receivedPutRequests.ShouldBeEquivalentTo(expectedPutRequests);
    receivedDeleteRequests.ShouldBeEquivalentTo(expectedDeleteRequests);
  }

  [TestMethod]
  public async Task OnRunFinished_WriteFails_Retries() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key1 = Guid.NewGuid().ToString();
    var key2 = Guid.NewGuid().ToString();
    var value = Guid.NewGuid().ToString();

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    var retriedWriteRequest = new WriteRequest(new PutRequest(new Dictionary<string, AttributeValue>() {
        { "RETRY", new AttributeValue("RETRY") }
    }));

    client.BatchWriteItemAsync(Arg.Is<BatchWriteItemRequest>(r => r.RequestItems[TableName].Count == 2),
            Arg.Any<CancellationToken>())
        .Returns(new BatchWriteItemResponse {
            UnprocessedItems = new Dictionary<string, List<WriteRequest>> { { TableName, [retriedWriteRequest] } }
        });

    client.BatchWriteItemAsync(Arg.Is<BatchWriteItemRequest>(r => r.RequestItems[TableName].Count == 1),
            Arg.Any<CancellationToken>())
        .Returns(new BatchWriteItemResponse { UnprocessedItems = null });

    await provider.SetString(pluginName, pluginDescription, key1, value, CancellationToken.None).ConfigureAwait(false);
    await provider.Remove(pluginName, pluginDescription, key2, CancellationToken.None).ConfigureAwait(false);

    // Act
    await provider.OnRunFinished(dryRun: false, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await client.Received(2).BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    var firstRequest = client.ReceivedCalls().First().GetArguments()[0].ShouldBeOfType<BatchWriteItemRequest>();
    firstRequest.RequestItems.Keys.ShouldHaveSingleItem(TableName);
    firstRequest.RequestItems[TableName].Count.ShouldBe(2);

    var secondRequest = client.ReceivedCalls().Last().GetArguments()[0].ShouldBeOfType<BatchWriteItemRequest>();
    secondRequest.RequestItems.Keys.ShouldHaveSingleItem(TableName);
    secondRequest.RequestItems[TableName].ShouldHaveSingleItem().ShouldBeEquivalentTo(retriedWriteRequest);
  }

  [TestMethod]
  public async Task OnRunFinished_WriteFails_DoesNotRetryForever() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    client.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(),
            Arg.Any<CancellationToken>())
        .Returns(r => new BatchWriteItemResponse { UnprocessedItems = r.Arg<BatchWriteItemRequest>().RequestItems });

    await provider.Remove(pluginName, pluginDescription, key, CancellationToken.None).ConfigureAwait(false);

    // Act
    using var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(10));
    await provider.OnRunFinished(dryRun: false, cancellationTokenSource.Token).ConfigureAwait(false);

    // Assert
    await client.Received(6).BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    logger.LatestRecord.Message.ShouldBe("Retry count exceeded, failed items will not be retried");
  }

  [TestMethod]
  public async Task OnRunFinished_DynamoDbThrows_AutomaticallyRetries() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);

    client.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .Returns(
            _ => throw new Exception("try again"),
            _ => new BatchWriteItemResponse { UnprocessedItems = null });

    await provider.Remove(pluginName, pluginDescription, key, CancellationToken.None).ConfigureAwait(false);

    // Act
    await provider.OnRunFinished(dryRun: false, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await client.Received(2).BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);
  }

  [TestMethod]
  public async Task OnRunFinished_DynamoDbThrowsForever_ThrowsException() {
    // Arrange
    var client = Substitute.For<IAmazonDynamoDB>();
    var pluginName = Guid.NewGuid().ToString();
    var pluginDescription = Guid.NewGuid().ToString();
    var key = Guid.NewGuid().ToString();

    var logger = new FakeLogger<DynamoDbPluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new DynamoDbPluginStateProvider(client, logger, notificationSender, _options);
    var exception = new Exception("nope");

    client.BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .Throws(exception);

    // Act
    await provider.Remove(pluginName, pluginDescription, key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    Func<Task> act = async () => await provider.OnRunFinished(dryRun: false, CancellationToken.None).ConfigureAwait(false);

    (await act.ShouldThrowAsync<Exception>().ConfigureAwait(false)).ShouldBe(exception);
    await client.Received(5).BatchWriteItemAsync(Arg.Any<BatchWriteItemRequest>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);
  }
}
