using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.Semaphores;
using CleanupForNotion.Core.Infrastructure.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace CleanupForNotion.Aws.DynamoDb;

public class DynamoDbPluginStateProvider : IPluginStateProvider, INotificationListener, IDisposable {
  private readonly IAmazonDynamoDB _dynamoDbClient;
  private readonly ILogger<DynamoDbPluginStateProvider> _logger;
  private readonly INotificationSender _notificationSender;
  private readonly IOptions<DynamoDbOptions> _options;

  private readonly SemaphoreSlim _semaphore = new(1, 1);
  private readonly Dictionary<DynamoDbStateKey, string?> _pendingWrites = new();

  private readonly ResiliencePipeline _resiliencePipeline = new ResiliencePipelineBuilder()
      .AddRetry(new RetryStrategyOptions() {
          BackoffType = DelayBackoffType.Exponential,
          UseJitter = true,
          MaxRetryAttempts = 4,
          Delay = TimeSpan.FromSeconds(2),
      })
      .AddTimeout(TimeSpan.FromSeconds(10))
      .Build();

  public DynamoDbPluginStateProvider(
      IAmazonDynamoDB dynamoDbClient,
      ILogger<DynamoDbPluginStateProvider> logger,
      INotificationSender notificationSender,
      IOptions<DynamoDbOptions> options) {
    _dynamoDbClient = dynamoDbClient;
    _logger = logger;
    _notificationSender = notificationSender;
    _notificationSender.Register(this);
    _options = options;
  }

  public async Task<string?> GetString(string pluginName, string pluginDescription, string key,
      CancellationToken cancellationToken) {
    var stateKey = new DynamoDbStateKey(pluginName, pluginDescription, key);

    using var _ = await _semaphore.AcquireAsync(cancellationToken).ConfigureAwait(false);
    if (_pendingWrites.TryGetValue(stateKey, out var pendingWrite)) {
      _logger.LogTrace(
          "Returning state item with partition key '{PartitionKey}' and sort key '{SortKey}' from pending write cache with value '{Value}'",
          stateKey.PartitionKey, stateKey.SortKey, pendingWrite);
      return pendingWrite;
    }

    _logger.LogTrace("Retrieving state item with partition key '{PartitionKey}' and sort key '{SortKey}'",
        stateKey.PartitionKey, stateKey.SortKey);

    var item = await _resiliencePipeline.ExecuteAsync(async token => await _dynamoDbClient.GetItemAsync(
            new GetItemRequest(
                tableName: _options.Value.TableName,
                key: stateKey.ToDictionary(),
                consistentRead: true), token)
        .ConfigureAwait(false), cancellationToken).ConfigureAwait(false);

    if (item.Item == null) {
      _logger.LogWarning(
          "Could not find state item with partition key '{PartitionKey}' and sort key '{SortKey}', returning null",
          stateKey.PartitionKey, stateKey.SortKey);
      return null;
    }

    var value = item.Item[DynamoDbConstants.ValueKeyName].S;
    _logger.LogTrace("Retrieved state item with partition key '{PartitionKey}' and sort key '{SortKey}' with value '{Value}'",
        stateKey.PartitionKey, stateKey.SortKey, value);

    return value;
  }

  public async Task SetString(string pluginName, string pluginDescription, string key, string value,
      CancellationToken cancellationToken) {
    var stateKey = new DynamoDbStateKey(pluginName, pluginDescription, key);

    using var _ = await _semaphore.AcquireAsync(cancellationToken).ConfigureAwait(false);
    _pendingWrites[stateKey] = value;
  }

  public async Task Remove(string pluginName, string pluginDescription, string key,
      CancellationToken cancellationToken) {
    var stateKey = new DynamoDbStateKey(pluginName, pluginDescription, key);

    using var _ = await _semaphore.AcquireAsync(cancellationToken).ConfigureAwait(false);
    _pendingWrites[stateKey] = null;
  }

  public async Task OnRunFinished(bool dryRun, CancellationToken cancellationToken) {
    using var _ = await _semaphore.AcquireAsync(cancellationToken).ConfigureAwait(false);

    if (dryRun || _pendingWrites.Count == 0) {
      _pendingWrites.Clear();
      return;
    }

    var retryCount = 0;

    try {
      var writeRequests = _pendingWrites
          .Select(item => item.Value == null
              ? new WriteRequest { DeleteRequest = new DeleteRequest(item.Key.ToDictionary()) }
              : new WriteRequest { PutRequest = new PutRequest(item.Key.ToDictionary(item.Value)) })
          .ToList();

      while (writeRequests.Count > 0) {
        var count = Math.Min(writeRequests.Count, 25);
        var writeRequestsInBatch = writeRequests.GetRange(0, count);
        writeRequests.RemoveRange(0, count);

        _logger.LogInformation(
            "Writing state changes to DynamoDB: {WriteRequests}",
            JsonSerializer.Serialize(writeRequestsInBatch));

        var batchWriteRequest = new BatchWriteItemRequest {
            RequestItems =
                new Dictionary<string, List<WriteRequest>> { { _options.Value.TableName, writeRequestsInBatch } }
        };

        var batchWriteResponse = await _resiliencePipeline
            .ExecuteAsync(
                async token =>
                    await _dynamoDbClient.BatchWriteItemAsync(batchWriteRequest, token).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);

        if (batchWriteResponse.UnprocessedItems is not { Count: > 0 }) {
          continue;
        }

        if (retryCount >= 5) {
          _logger.LogError("Retry count exceeded, failed items will not be retried");
          continue;
        }

        ++retryCount;
        var unprocessedItems = batchWriteResponse.UnprocessedItems[_options.Value.TableName];
        _logger.LogWarning("Retrying unprocessed items: {UnprocessedItems}",
            JsonSerializer.Serialize(unprocessedItems));
        writeRequests.AddRange(unprocessedItems);
        await Task.Delay(500 * retryCount, cancellationToken).ConfigureAwait(false);
      }
    } finally {
      _pendingWrites.Clear();
    }
  }

  public void Dispose() {
    _notificationSender.Unregister(this);
    _semaphore.Dispose();
    GC.SuppressFinalize(this);
  }
}
