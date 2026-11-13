using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupForNotion.Aws.DynamoDb;

public class DynamoDbLockManager(
    IAmazonDynamoDB dynamoDbClient,
    ILogger<DynamoDbLockManager> logger,
    IOptions<DynamoDbOptions> options,
    TimeProvider timeProvider) {
  private readonly string _tableName = options.Value.TableName;

  private LockToken GenerateToken() {
    return new LockToken(timeProvider.GetUtcNow(), Guid.NewGuid());
  }

  public async Task<LockToken> AcquireLock(CancellationToken cancellationToken) {
    using var timerCts = new CancellationTokenSource();
    timerCts.CancelAfter(DynamoDbConstants.LockWaitTime);
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timerCts.Token);
    try {
      while (!cts.Token.IsCancellationRequested) {
        var acquiredLock = await TryAcquire(cts.Token).ConfigureAwait(false);
        if (acquiredLock is not null) {
          logger.LogDebug("Acquired lock: {LockToken}", acquiredLock);
          return acquiredLock;
        }

        var currentLock = await ReadCurrentLock(timerCts.Token).ConfigureAwait(false);
        if (currentLock == null) {
          logger.LogWarning("Lock could not be read, it has probably expired - retrying to acquire");
          continue;
        }

        var timeSinceAcquired = timeProvider.GetUtcNow() - currentLock.Timestamp;
        if (timeSinceAcquired > DynamoDbConstants.LockAbandonTime) {
          logger.LogWarning("Existing lock {LockToken} is abandoned, will try to release", currentLock);
          await ReleaseLock(currentLock).ConfigureAwait(false);
          continue;
        }

        logger.LogDebug("Waiting for lock to become available (existing lock: {LockToken})", currentLock);
        await Task.Delay(1000, timerCts.Token).ConfigureAwait(false);
      }
    } catch (OperationCanceledException exc) {
      throw new Exception("Failed to acquire lock", exc);
    }

    throw new Exception("Failed to acquire lock");
  }

  public async Task<bool> ReleaseLock(LockToken lockToken) {
    try {
      await dynamoDbClient.DeleteItemAsync(
          new DeleteItemRequest {
              TableName = _tableName,
              Key = LockKey,
              ConditionExpression = DynamoDbConstants.ConditionLockEquals,
              ExpressionAttributeNames =
                  new Dictionary<string, string>() {
                      { DynamoDbConstants.ValueKeyAttributeKeyName, DynamoDbConstants.ValueKeyName },
                  },
              ExpressionAttributeValues = new Dictionary<string, AttributeValue>() {
                  { DynamoDbConstants.ValueKeyAttributeValueName, new AttributeValue(lockToken.ToString()) },
              }
          }, CancellationToken.None).ConfigureAwait(false);
      return true;
    } catch (ConditionalCheckFailedException) {
      logger.LogError("Failed to release lock {LockToken} - it might have been deleted or replaced", lockToken);
      return false;
    }
  }

  private async Task<LockToken?> TryAcquire(CancellationToken cancellationToken) {
    var lockToken = GenerateToken();
    var request = new PutItemRequest {
        TableName = _tableName,
        Item = LockValue(lockToken.ToString()),
        ConditionExpression = DynamoDbConstants.ConditionLockNotPresent
    };

    try {
      logger.LogTrace("Attempting to acquire lock: {LockToken}", lockToken);
      await dynamoDbClient.PutItemAsync(request, cancellationToken).ConfigureAwait(false);
      return lockToken;
    } catch (ConditionalCheckFailedException) {
      logger.LogWarning("Lock not acquired");
      return null;
    }
  }

  private async Task<LockToken?> ReadCurrentLock(CancellationToken cancellationToken) {
    var request = new GetItemRequest { TableName = _tableName, Key = LockKey, ConsistentRead = true };

    var existingItem = await dynamoDbClient.GetItemAsync(
        request,
        cancellationToken).ConfigureAwait(false);

    if (existingItem.IsItemSet &&
        existingItem.Item.TryGetValue(DynamoDbConstants.ValueKeyName, out var lockTokenAttribute)) {
      return LockToken.FromString(lockTokenAttribute.S);
    }

    return null;
  }

  private static Dictionary<string, AttributeValue> LockKey => new() {
      { DynamoDbConstants.PartitionKeyName, DynamoDbConstants.LockKeyValue },
      { DynamoDbConstants.SortKeyName, DynamoDbConstants.LockKeyValue }
  };

  private static Dictionary<string, AttributeValue> LockValue(string tokenString) => new() {
      { DynamoDbConstants.PartitionKeyName, DynamoDbConstants.LockKeyValue },
      { DynamoDbConstants.SortKeyName, DynamoDbConstants.LockKeyValue },
      { DynamoDbConstants.ValueKeyName, new AttributeValue(tokenString) }
  };
}
