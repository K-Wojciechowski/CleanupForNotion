using System.Diagnostics;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CleanupForNotion.Aws.DynamoDb;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace CleanupForNotion.Aws.Test;

[TestClass]
public class DynamoDbLockManagerUnitTests {
  [TestMethod]
  public async Task AcquireLock_NoLockPresent_ReturnsToken() {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var dynamoDbClient = Substitute.For<IAmazonDynamoDB>();
    var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

    dynamoDbClient.PutItemAsync(Arg.Is<PutItemRequest>(r =>
                r.TableName == tableName &&
                r.ConditionExpression == DynamoDbConstants.ConditionLockNotPresent),
            Arg.Any<CancellationToken>())
        .Returns(new PutItemResponse());

    var lockManager = new DynamoDbLockManager(
        dynamoDbClient,
        NullLogger<DynamoDbLockManager>.Instance,
        new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = tableName }),
        fakeTimeProvider
    );

    // Act
    var lockToken = await lockManager.AcquireLock(CancellationToken.None).ConfigureAwait(false);

    // Assert
    lockToken.Timestamp.ShouldBe(fakeTimeProvider.GetUtcNow());
    dynamoDbClient.ReceivedCalls().First().GetArguments()[0].ShouldBeOfType<PutItemRequest>()
        .Item[DynamoDbConstants.ValueKeyName].S.ShouldBe(lockToken.ToString());
  }

  [TestMethod]
  public async Task AcquireLock_LockPresentAndThenExpires_ReturnsToken() {
    var timeTaken = await TestAcquireWithSingleConflict(fakeTimeProvider => new GetItemResponse {
        IsItemSet = true,
        Item = new Dictionary<string, AttributeValue> {
            {
                DynamoDbConstants.ValueKeyName,
                new AttributeValue(new LockToken(fakeTimeProvider.GetUtcNow(), Guid.NewGuid()).ToString())
            }
        }
    }).ConfigureAwait(false);
    timeTaken.ShouldBeGreaterThan(900);
  }

  [TestMethod]
  public async Task AcquireLock_LockPresentButDisappears_ReturnsToken() {
    var timeTaken = await TestAcquireWithSingleConflict(fakeTimeProvider => new GetItemResponse { IsItemSet = false })
        .ConfigureAwait(false);
    timeTaken.ShouldBeLessThan(100);
  }

  [TestMethod]
  public async Task AcquireLock_LockPresentButUnreadableAndThenDisappears_ReturnsToken() {
    var timeTaken = await TestAcquireWithSingleConflict(fakeTimeProvider => new GetItemResponse {
        IsItemSet = true,
        Item = new Dictionary<string, AttributeValue> {
            { "invalid", new AttributeValue(new LockToken(fakeTimeProvider.GetUtcNow(), Guid.NewGuid()).ToString()) }
        }
    }).ConfigureAwait(false);
    timeTaken.ShouldBeLessThan(100);
  }

  [TestMethod]
  public async Task AcquireLock_LockAbandoned_ReleasesIt() {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var dynamoDbClient = Substitute.For<IAmazonDynamoDB>();
    var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var abandonedLock = new LockToken(
        fakeTimeProvider.GetUtcNow().Subtract(DynamoDbConstants.LockAbandonTime).AddSeconds(-1),
        Guid.NewGuid());

    dynamoDbClient.PutItemAsync(Arg.Is<PutItemRequest>(r =>
            r.TableName == tableName &&
            r.ConditionExpression == DynamoDbConstants.ConditionLockNotPresent),
        Arg.Any<CancellationToken>()).Returns(new PutItemResponse());

    dynamoDbClient
        .When(c => c.PutItemAsync(Arg.Is<PutItemRequest>(r =>
                r.TableName == tableName &&
                r.ConditionExpression == DynamoDbConstants.ConditionLockNotPresent),
            Arg.Any<CancellationToken>()))
        .Do(Callback
            .FirstThrow(new ConditionalCheckFailedException())
        );

    dynamoDbClient
        .GetItemAsync(Arg.Is<GetItemRequest>(r => r.TableName == tableName), Arg.Any<CancellationToken>())
        .Returns(new GetItemResponse {
            IsItemSet = true,
            Item = new Dictionary<string, AttributeValue> {
                { DynamoDbConstants.ValueKeyName, new AttributeValue(abandonedLock.ToString()) }
            }
        });

    dynamoDbClient.DeleteItemAsync(Arg.Is<DeleteItemRequest>(r =>
                r.TableName == tableName &&
                r.ConditionExpression == DynamoDbConstants.ConditionLockEquals &&
                r.ExpressionAttributeValues[DynamoDbConstants.ValueKeyAttributeValueName].S ==
                abandonedLock.ToString()),
            CancellationToken.None)
        .Returns(new DeleteItemResponse());

    var lockManager = new DynamoDbLockManager(
        dynamoDbClient,
        NullLogger<DynamoDbLockManager>.Instance,
        new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = tableName }),
        fakeTimeProvider
    );

    // Act
    var stopwatch = Stopwatch.StartNew();
    var lockToken = await lockManager.AcquireLock(CancellationToken.None).ConfigureAwait(false);

    // Assert
    stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
    lockToken.Timestamp.ShouldBe(fakeTimeProvider.GetUtcNow());
    var receivedCalls = dynamoDbClient.ReceivedCalls().ToList();
    receivedCalls.Count.ShouldBe(4);
    receivedCalls[0].GetArguments()[0].ShouldBeOfType<PutItemRequest>()
        .Item[DynamoDbConstants.ValueKeyName].S.ShouldNotBe(lockToken.ToString());
    receivedCalls[1].GetArguments()[0].ShouldBeOfType<GetItemRequest>();
    receivedCalls[2].GetArguments()[0].ShouldBeOfType<DeleteItemRequest>()
        .ExpressionAttributeValues[DynamoDbConstants.ValueKeyAttributeValueName].S.ShouldBe(abandonedLock.ToString());
    receivedCalls[3].GetArguments()[0].ShouldBeOfType<PutItemRequest>()
        .Item[DynamoDbConstants.ValueKeyName].S.ShouldBe(lockToken.ToString());
  }

  [TestMethod]
  public async Task AcquireLock_LockAbandonedAndThenStolen_ThrowsAfterTimeout() {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var dynamoDbClient = Substitute.For<IAmazonDynamoDB>();
    var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var abandonedLock = new LockToken(
        fakeTimeProvider.GetUtcNow().Subtract(DynamoDbConstants.LockAbandonTime).AddSeconds(-1),
        Guid.NewGuid());
    var secondLock = new LockToken(
        fakeTimeProvider.GetUtcNow().AddSeconds(-5),
        Guid.NewGuid());

    dynamoDbClient.PutItemAsync(Arg.Is<PutItemRequest>(r =>
            r.TableName == tableName &&
            r.ConditionExpression == DynamoDbConstants.ConditionLockNotPresent),
        Arg.Any<CancellationToken>()).Throws(new ConditionalCheckFailedException());

    dynamoDbClient
        .GetItemAsync(Arg.Is<GetItemRequest>(r => r.TableName == tableName), Arg.Any<CancellationToken>())
        .Returns(
            new GetItemResponse {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue> {
                    { DynamoDbConstants.ValueKeyName, new AttributeValue(abandonedLock.ToString()) }
                },
            },
            new GetItemResponse {
                IsItemSet = true,
                Item = new Dictionary<string, AttributeValue> {
                    { DynamoDbConstants.ValueKeyName, new AttributeValue(secondLock.ToString()) }
                }
            });

    dynamoDbClient.DeleteItemAsync(Arg.Is<DeleteItemRequest>(r =>
                r.TableName == tableName &&
                r.ConditionExpression == DynamoDbConstants.ConditionLockEquals &&
                r.ExpressionAttributeValues[DynamoDbConstants.ValueKeyAttributeValueName].S ==
                abandonedLock.ToString()),
            CancellationToken.None)
        .Returns(new DeleteItemResponse());

    var lockManager = new DynamoDbLockManager(
        dynamoDbClient,
        NullLogger<DynamoDbLockManager>.Instance,
        new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = tableName }),
        fakeTimeProvider
    );

    // Act
    var stopwatch = Stopwatch.StartNew();
    Func<Task> act = async () => await lockManager.AcquireLock(CancellationToken.None).ConfigureAwait(false);

    // Assert
    var exc = await act.ShouldThrowAsync<Exception>().ConfigureAwait(false);
    stopwatch.ElapsedMilliseconds.ShouldBeGreaterThan(14000);
    exc.Message.ShouldBe("Failed to acquire lock");
    var receivedCalls = dynamoDbClient.ReceivedCalls().ToList();
    receivedCalls[0].GetArguments()[0].ShouldBeOfType<PutItemRequest>();
    receivedCalls[1].GetArguments()[0].ShouldBeOfType<GetItemRequest>();
    receivedCalls[2].GetArguments()[0].ShouldBeOfType<DeleteItemRequest>()
        .ExpressionAttributeValues[DynamoDbConstants.ValueKeyAttributeValueName].S.ShouldBe(abandonedLock.ToString());

    for (var i = 3; i < receivedCalls.Count; i++) {
      if (i % 2 == 1) {
        receivedCalls[i].GetArguments()[0].ShouldBeOfType<PutItemRequest>();
      } else {
        receivedCalls[i].GetArguments()[0].ShouldBeOfType<GetItemRequest>();
      }
    }
  }

  [TestMethod]
  public async Task AcquireLock_TokenAlreadyCancelled_Throws() {
    // Arrange
    using var cts = new CancellationTokenSource();
    await cts.CancelAsync().ConfigureAwait(false);
    var dynamoDbClient = Substitute.For<IAmazonDynamoDB>();

    var lockManager = new DynamoDbLockManager(
        dynamoDbClient,
        NullLogger<DynamoDbLockManager>.Instance,
        new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = "" }),
        new FakeTimeProvider()
    );

    // Act
    Func<Task<LockToken>> act = async () => await lockManager.AcquireLock(cts.Token).ConfigureAwait(false);

    // Assert
    var exc = await act.ShouldThrowAsync<Exception>().ConfigureAwait(false);
    exc.Message.ShouldBe("Failed to acquire lock");
  }

  [TestMethod]
  public async Task ReleaseLock_CurrentLock_ReturnsTrue() {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var dynamoDbClient = Substitute.For<IAmazonDynamoDB>();
    var lockToken = new LockToken(DateTimeOffset.UtcNow, Guid.NewGuid());

    dynamoDbClient.DeleteItemAsync(Arg.Is<DeleteItemRequest>(r =>
                r.TableName == tableName &&
                r.ConditionExpression == DynamoDbConstants.ConditionLockEquals &&
                r.ExpressionAttributeValues[DynamoDbConstants.ValueKeyAttributeValueName].S == lockToken.ToString()),
            CancellationToken.None)
        .Returns(new DeleteItemResponse());

    var lockManager = new DynamoDbLockManager(
        dynamoDbClient,
        NullLogger<DynamoDbLockManager>.Instance,
        new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = tableName }),
        new FakeTimeProvider()
    );

    // Act
    var result = await lockManager.ReleaseLock(lockToken).ConfigureAwait(false);

    // Assert
    result.ShouldBeTrue();
  }

  [TestMethod]
  public async Task ReleaseLock_NotCurrentLock_ReturnsFalse() {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var dynamoDbClient = Substitute.For<IAmazonDynamoDB>();
    var lockToken = new LockToken(DateTimeOffset.UtcNow, Guid.NewGuid());

    dynamoDbClient.DeleteItemAsync(Arg.Is<DeleteItemRequest>(r =>
                r.TableName == tableName &&
                r.ConditionExpression == DynamoDbConstants.ConditionLockEquals &&
                r.ExpressionAttributeValues[DynamoDbConstants.ValueKeyAttributeValueName].S == lockToken.ToString()),
            CancellationToken.None)
        .ThrowsAsync(new ConditionalCheckFailedException());

    var lockManager = new DynamoDbLockManager(
        dynamoDbClient,
        NullLogger<DynamoDbLockManager>.Instance,
        new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = tableName }),
        new FakeTimeProvider()
    );

    // Act
    var result = await lockManager.ReleaseLock(lockToken).ConfigureAwait(false);

    // Assert
    result.ShouldBeFalse();
  }

  [TestMethod]
  public void ReleaseLock_DatabaseThrowsException_DoesNotHandle() {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var dynamoDbClient = Substitute.For<IAmazonDynamoDB>();
    var lockToken = new LockToken(DateTimeOffset.UtcNow, Guid.NewGuid());

    dynamoDbClient.DeleteItemAsync(Arg.Is<DeleteItemRequest>(r =>
                r.TableName == tableName &&
                r.ConditionExpression == DynamoDbConstants.ConditionLockEquals &&
                r.ExpressionAttributeValues[DynamoDbConstants.ValueKeyAttributeValueName].S == lockToken.ToString()),
            CancellationToken.None)
        .ThrowsAsync(new InvalidOperationException());

    var lockManager = new DynamoDbLockManager(
        dynamoDbClient,
        NullLogger<DynamoDbLockManager>.Instance,
        new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = tableName }),
        new FakeTimeProvider()
    );

    // Act
    Func<Task> act = async () => await lockManager.ReleaseLock(lockToken).ConfigureAwait(false);

    // Assert
    act.ShouldThrow<InvalidOperationException>();
  }

  private async Task<long> TestAcquireWithSingleConflict(
      Func<FakeTimeProvider, GetItemResponse> produceGetItemResponse) {
    // Arrange
    var tableName = Guid.NewGuid().ToString();
    var dynamoDbClient = Substitute.For<IAmazonDynamoDB>();
    var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

    dynamoDbClient.PutItemAsync(Arg.Is<PutItemRequest>(r =>
            r.TableName == tableName &&
            r.ConditionExpression == DynamoDbConstants.ConditionLockNotPresent),
        Arg.Any<CancellationToken>()).Returns(new PutItemResponse());

    dynamoDbClient
        .When(c => c.PutItemAsync(Arg.Is<PutItemRequest>(r =>
                r.TableName == tableName &&
                r.ConditionExpression == DynamoDbConstants.ConditionLockNotPresent),
            Arg.Any<CancellationToken>()))
        .Do(Callback
            .FirstThrow(new ConditionalCheckFailedException())
        );

    dynamoDbClient
        .GetItemAsync(Arg.Is<GetItemRequest>(r => r.TableName == tableName), Arg.Any<CancellationToken>())
        .Returns(produceGetItemResponse(fakeTimeProvider));

    var lockManager = new DynamoDbLockManager(
        dynamoDbClient,
        NullLogger<DynamoDbLockManager>.Instance,
        new OptionsWrapper<DynamoDbOptions>(new DynamoDbOptions { TableName = tableName }),
        fakeTimeProvider
    );

    // Act
    var stopwatch = Stopwatch.StartNew();
    var lockToken = await lockManager.AcquireLock(CancellationToken.None).ConfigureAwait(false);

    // Assert
    stopwatch.Stop();
    lockToken.Timestamp.ShouldBe(fakeTimeProvider.GetUtcNow());
    var receivedCalls = dynamoDbClient.ReceivedCalls().ToList();
    receivedCalls.Count.ShouldBe(3);
    receivedCalls[0].GetArguments()[0].ShouldBeOfType<PutItemRequest>()
        .Item[DynamoDbConstants.ValueKeyName].S.ShouldNotBe(lockToken.ToString());
    receivedCalls[1].GetArguments()[0].ShouldBeOfType<GetItemRequest>();
    receivedCalls[2].GetArguments()[0].ShouldBeOfType<PutItemRequest>()
        .Item[DynamoDbConstants.ValueKeyName].S.ShouldBe(lockToken.ToString());
    return stopwatch.ElapsedMilliseconds;
  }
}
