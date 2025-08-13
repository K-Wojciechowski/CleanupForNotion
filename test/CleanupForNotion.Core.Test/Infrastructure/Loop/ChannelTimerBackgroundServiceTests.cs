using System.Threading.Channels;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Loop;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.Loop;

[TestClass]
public class ChannelTimerBackgroundServiceTests {
  [TestMethod]
  public async Task ExecuteAsync_RunFrequencyNotConfigured_ReturnsImmediately() {
    // Arrange
    var channel = Channel.CreateUnbounded<DateTimeOffset>();
    var timeProvider = TimeProvider.System;
    var logger = new FakeLogger<ChannelTimerBackgroundService>();
    var service = new ChannelTimerBackgroundService(
        channel: channel,
        globalOptionsProvider: BuildGlobalOptionsProvider(runFrequency: null),
        logger: logger,
        timeProvider: timeProvider);

    var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(2));

    // Act
    await service.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    await Task.Delay(500, cancellationTokenSource.Token).ConfigureAwait(false);

    // Assert
    service.ExecuteTask?.IsCompleted.ShouldBeTrue();
    logger.LatestRecord.Message.ShouldBe("Run frequency is not configured - timer will not run");
  }

  [TestMethod]
  public async Task ExecuteAsync_RunFrequencyConfigured_RunsForeverAndWritesToChannel() {
    // Arrange
    var channel = Channel.CreateUnbounded<DateTimeOffset>();
    var timeProvider = TimeProvider.System;
    var logger = new FakeLogger<ChannelTimerBackgroundService>();
    var runFrequency = TimeSpan.FromMilliseconds(400);
    var service = new ChannelTimerBackgroundService(
        channel: channel,
        globalOptionsProvider: BuildGlobalOptionsProvider(runFrequency),
        logger: logger,
        timeProvider: timeProvider);

    var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(4));

    // Act
    var startTime = timeProvider.GetUtcNow();
    await service.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    await Task.Delay(2000, cancellationTokenSource.Token).ConfigureAwait(false);
    await service.StopAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    await Task.Delay(500, cancellationTokenSource.Token).ConfigureAwait(false);

    // Assert
    service.ExecuteTask?.IsCompleted.ShouldBeTrue();
    channel.Writer.Complete();
    var triggers = channel.Reader
        .ReadAllAsync(CancellationToken.None)
        .ToBlockingEnumerable(CancellationToken.None)
        .ToList();
    triggers.Count.ShouldBeInRange(4, 6);
    // cannot expect too much precision with high frequencies
    triggers[0].ShouldBeInRange(startTime.AddMilliseconds(50), startTime.AddMilliseconds(600));
    var timeBetweenTriggers = triggers[2] - triggers[1];
    timeBetweenTriggers.TotalMilliseconds.ShouldBeInRange(200, 600);

    logger.LatestRecord.Message.ShouldBe($"Waiting for {runFrequency} until next run");
  }

  private IGlobalOptionsProvider BuildGlobalOptionsProvider(TimeSpan? runFrequency) {
    var provider = Substitute.For<IGlobalOptionsProvider>();
    provider.GlobalOptions.Returns(new GlobalOptions(DryRun: true, RunFrequency: runFrequency));
    return provider;
  }
}
