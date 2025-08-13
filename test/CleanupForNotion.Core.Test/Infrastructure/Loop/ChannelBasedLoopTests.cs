using System.Threading.Channels;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.Loop;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Infrastructure.Loop;

[TestClass]
public class ChannelBasedLoopTests {
  [TestMethod]
  public async Task ExecuteAsync_NeverTriggered_DoesNotRunCleanup() {
    // Arrange
    var channel = Channel.CreateUnbounded<DateTimeOffset>();
    var globalOptionsProvider = BuildGlobalOptionsProvider();
    var logger = new FakeLogger<ChannelBasedLoop>();
    var timeProvider = new FakeTimeProvider();

    var runner = Substitute.For<IRunner>();

    var services = new ServiceCollection()
        .AddScoped<IRunner>(_ => runner)
        .BuildServiceProvider();

    var loop = new ChannelBasedLoop(
        channel: channel,
        globalOptionsProvider: globalOptionsProvider,
        logger: logger,
        timeProvider: timeProvider,
        serviceScopeFactory: services.GetRequiredService<IServiceScopeFactory>());

    // Act
    await loop.StartAsync(CancellationToken.None).ConfigureAwait(false);
    await Task.Delay(1000, CancellationToken.None).ConfigureAwait(false);
    await loop.StopAsync(CancellationToken.None).ConfigureAwait(false);

    // Assert
    logger.Collector.GetSnapshot().ShouldBeEmpty();
    await runner.Received(0).RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task ExecuteAsync_TriggeredOnce_RunsCleanup() {
    // Arrange
    var channel = Channel.CreateUnbounded<DateTimeOffset>();
    var globalOptionsProvider = BuildGlobalOptionsProvider();
    var logger = new FakeLogger<ChannelBasedLoop>();
    var timeProvider = new FakeTimeProvider();
    var runnerCalls = new List<DateTimeOffset>();
    var runTime = TimeSpan.FromMilliseconds(2137);

    var runner = Substitute.For<IRunner>();

    var services = new ServiceCollection()
        .AddScoped<IRunner>(_ => runner)
        .BuildServiceProvider();

    var loop = new ChannelBasedLoop(
        channel: channel,
        globalOptionsProvider: globalOptionsProvider,
        logger: logger,
        timeProvider: timeProvider,
        serviceScopeFactory: services.GetRequiredService<IServiceScopeFactory>());

    // Act
    await loop.StartAsync(CancellationToken.None).ConfigureAwait(false);
    await Task.Delay(200, CancellationToken.None).ConfigureAwait(false);
    runnerCalls.ShouldBeEmpty();

    Task? stopTask = null;

    runner.RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .Returns(async _ => {
          runnerCalls.Add(timeProvider.GetUtcNow());
          timeProvider.Advance(runTime);
          stopTask = loop.StopAsync(CancellationToken.None);
          await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
        });

    var triggerTime = timeProvider.GetUtcNow().Subtract(TimeSpan.FromMilliseconds(123));
    var startTime = timeProvider.GetUtcNow();
    await channel.Writer.WriteAsync(triggerTime, CancellationToken.None).ConfigureAwait(false);
    await Task.Delay(200, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await stopTask!.ConfigureAwait(false);
    loop.ExecuteTask?.IsCompletedSuccessfully.ShouldBeTrue();
    runnerCalls.ShouldHaveSingleItem().ShouldBe(startTime);
    var logRecords = logger.Collector.GetSnapshot();
    logRecords.Count.ShouldBe(2);
    logRecords[0].Message.ShouldBe(
            $"Received trigger from {triggerTime:O} at {startTime:O} (in {(startTime - triggerTime).TotalMilliseconds} ms)");
    logRecords[^1].Message.ShouldBe(
            $"Finished trigger from {triggerTime:O} at {timeProvider.GetUtcNow():O} (in {(timeProvider.GetUtcNow() - startTime).TotalMilliseconds} ms)");
    await runner.Received(1).RunCleanup(globalOptionsProvider.GlobalOptions, Arg.Any<CancellationToken>()).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task ExecuteAsync_CleanupThrows_DoesNotCrash() {
    // Arrange
    var channel = Channel.CreateUnbounded<DateTimeOffset>();
    var globalOptionsProvider = BuildGlobalOptionsProvider();
    var logger = new FakeLogger<ChannelBasedLoop>();
    var timeProvider = new FakeTimeProvider();
    var runnerCalls = new List<DateTimeOffset>();

    var runner = Substitute.For<IRunner>();

    runner.RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .Returns(_ => {
          runnerCalls.Add(timeProvider.GetUtcNow());
          if (runnerCalls.Count == 2) {
            throw new Exception("kaboom");
          }
          return Task.CompletedTask;
        });

    var services = new ServiceCollection()
        .AddScoped<IRunner>(_ => runner)
        .BuildServiceProvider();

    var loop = new ChannelBasedLoop(
        channel: channel,
        globalOptionsProvider: globalOptionsProvider,
        logger: logger,
        timeProvider: timeProvider,
        serviceScopeFactory: services.GetRequiredService<IServiceScopeFactory>());

    // Act
    var trigger1Time = timeProvider.GetUtcNow().Subtract(TimeSpan.FromMilliseconds(123));
    var trigger2Time = timeProvider.GetUtcNow().Add(TimeSpan.FromMilliseconds(456));
    var trigger3Time = timeProvider.GetUtcNow().Add(TimeSpan.FromMilliseconds(789));
    await channel.Writer.WriteAsync(trigger1Time, CancellationToken.None).ConfigureAwait(false);
    await channel.Writer.WriteAsync(trigger2Time, CancellationToken.None).ConfigureAwait(false);
    await channel.Writer.WriteAsync(trigger3Time, CancellationToken.None).ConfigureAwait(false);

    await loop.StartAsync(CancellationToken.None).ConfigureAwait(false);
    await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);
    await loop.StopAsync(CancellationToken.None).ConfigureAwait(false);

    // Assert
    runnerCalls.Count.ShouldBe(3);
    var logRecords = logger.Collector.GetSnapshot();
    logRecords.Count.ShouldBe(6);
    logRecords[1].Message.ShouldStartWith($"Finished trigger from {trigger1Time:O}");
    logRecords[3].Message.ShouldBe($"Failed trigger from {trigger2Time:O} with exception: kaboom");
    logRecords[5].Message.ShouldStartWith($"Finished trigger from {trigger3Time:O}");
    await runner.Received(3).RunCleanup(globalOptionsProvider.GlobalOptions, Arg.Any<CancellationToken>()).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task ExecuteAsync_HundredCalls_RunsAllCallsSequentially() {
    // Arrange
    var channel = Channel.CreateUnbounded<DateTimeOffset>();
    var globalOptionsProvider = BuildGlobalOptionsProvider();
    var logger = new FakeLogger<ChannelBasedLoop>();
    var timeProvider = new FakeTimeProvider();
    const int calls = 100;

    var runner = Substitute.For<IRunner>();

    runner.RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .Returns(_ => Task.Delay(Random.Shared.Next(1, 50)));

    var services = new ServiceCollection()
        .AddScoped<IRunner>(_ => runner)
        .BuildServiceProvider();

    var loop = new ChannelBasedLoop(
        channel: channel,
        globalOptionsProvider: globalOptionsProvider,
        logger: logger,
        timeProvider: timeProvider,
        serviceScopeFactory: services.GetRequiredService<IServiceScopeFactory>());

    // Act
    for (var i = 0; i < calls; i++) {
      var triggerTime = timeProvider.GetUtcNow().Add(TimeSpan.FromMilliseconds(Random.Shared.Next(-5000, 5000)));
      await channel.Writer.WriteAsync(triggerTime, CancellationToken.None).ConfigureAwait(false);
    }

    await loop.StartAsync(CancellationToken.None).ConfigureAwait(false);
    await Task.Delay(60 * calls, CancellationToken.None).ConfigureAwait(false);
    await loop.StopAsync(CancellationToken.None).ConfigureAwait(false);

    // Assert
    var logRecords = logger.Collector.GetSnapshot();
    logRecords.Count.ShouldBe(2 * calls);
    for (int l = 0; l < 2 * calls; l += 2) {
      logRecords[l].Message.ShouldStartWith("Received trigger from");
      logRecords[l + 1].Message.ShouldStartWith("Finished trigger from");
    }
    await runner.Received(calls).RunCleanup(globalOptionsProvider.GlobalOptions, Arg.Any<CancellationToken>()).ConfigureAwait(false);
  }

  private IGlobalOptionsProvider BuildGlobalOptionsProvider() {
    var provider = Substitute.For<IGlobalOptionsProvider>();
    provider.GlobalOptions.Returns(new GlobalOptions(DryRun: true,
        RunFrequency: TimeSpan.FromMilliseconds(Random.Shared.Next(12345))));
    return provider;
  }
}
