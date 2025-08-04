using System.Diagnostics;
using System.Net;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using Microsoft.Extensions.Logging.Testing;
using Notion.Client;
using Shouldly;

namespace CleanupForNotion.Test.Infrastructure.NotionIntegration;

[TestClass]
public class RateLimitHelperTests {
  [TestMethod]
  public async Task CallWithRetryAsync_NoRetryNeeded_ReturnsResult() {
    // Arrange
    const string expectedResult = "test";
    var logger = new FakeLogger();
    var stopwatch = Stopwatch.StartNew();

    // Act
    var result = await RateLimitHelper.CallWithRetryAsync(
        () => Task.FromResult<string>(expectedResult),
        logger,
        CancellationToken.None).ConfigureAwait(false);

    // Assert
    stopwatch.ElapsedMilliseconds.ShouldBeLessThan(150);
    result.ShouldBe(expectedResult);
    logger.Collector.Count.ShouldBe(0);
  }

  [TestMethod]
  public async Task CallWithRetryAsync_RetryNeededAndDelayProvided_ReturnsResultAfterDelay() {
    // Arrange
    const string expectedResult = "test";
    var retryAfter = TimeSpan.FromSeconds(1.25);
    var logger = new FakeLogger();

    int attempts = 0;

    Func<Task<string>> func = async () => {
      await Task.Yield();
      ++attempts;
      if (attempts <= 2) {
        throw new NotionApiRateLimitException(
            statusCode: HttpStatusCode.TooManyRequests,
            notionAPIErrorCode: null,
            message: "test",
            retryAfter: retryAfter);
      }

      return expectedResult;
    };

    var stopwatch = Stopwatch.StartNew();

    // Act
    var result = await RateLimitHelper.CallWithRetryAsync(
        func,
        logger,
        CancellationToken.None).ConfigureAwait(false);

    // Assert
    stopwatch.ElapsedMilliseconds.ShouldBeGreaterThan((int)(1.9 * (int)retryAfter.TotalMilliseconds));
    result.ShouldBe(expectedResult);
    logger.Collector.Count.ShouldBe(2);
    logger.LatestRecord.Message.ShouldBe($"Rate limit exceeded, will retry after {retryAfter.TotalSeconds} seconds");
  }

  [TestMethod]
  public async Task CallWithRetryAsync_RetryNeededAndDelayNotProvided_ReturnsResultAfterDefaultDelay() {
    // Arrange
    const string expectedResult = "test";
    var defaultRetryAfter = TimeSpan.FromSeconds(5);
    var logger = new FakeLogger();

    int attempts = 0;

    Func<Task<string>> func = async () => {
      await Task.Yield();
      ++attempts;
      if (attempts <= 1) {
        throw new NotionApiRateLimitException(
            statusCode: HttpStatusCode.TooManyRequests,
            notionAPIErrorCode: null,
            message: "test",
            retryAfter: null);
      }

      return expectedResult;
    };

    var stopwatch = Stopwatch.StartNew();

    // Act
    var result = await RateLimitHelper.CallWithRetryAsync(
        func,
        logger,
        CancellationToken.None).ConfigureAwait(false);

    // Assert
    stopwatch.ElapsedMilliseconds.ShouldBeGreaterThan((int)(0.9 * (int)defaultRetryAfter.TotalMilliseconds));
    result.ShouldBe(expectedResult);
    logger.Collector.Count.ShouldBe(1);
    logger.LatestRecord.Message.ShouldBe($"Rate limit exceeded, will retry after {defaultRetryAfter.TotalSeconds} seconds");
  }
}
