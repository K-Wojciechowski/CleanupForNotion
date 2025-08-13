using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.Loop;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.Loop;

[TestClass]
public class FrequencyBasedLoopTests {
  [TestMethod]
  public async Task ExecuteAsync_RunFrequencyNotConfigured_RunsOnceAndStopsApplication() {
    // Arrange
    var globalOptionsProvider = BuildGlobalOptionsProvider(null);
    var hostApplicationLifetime = Substitute.For<IHostApplicationLifetime>();
    var logger = new FakeLogger<FrequencyBasedLoop>();
    var runner = Substitute.For<IRunner>();
    var services = new ServiceCollection()
        .AddScoped<IRunner>(_ => runner)
        .BuildServiceProvider();

    runner.RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>()).Returns(Task.Delay(200, CancellationToken.None));

    var loop = new FrequencyBasedLoop(
        globalOptionsProvider: globalOptionsProvider,
        hostApplicationLifetime: hostApplicationLifetime,
        logger: logger,
        serviceScopeFactory: services.GetRequiredService<IServiceScopeFactory>());

    // Act
    var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.CancelAfter(1000);
    await loop.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    await Task.Delay(500, CancellationToken.None).ConfigureAwait(false);

    // Assert
    loop.ExecuteTask?.IsCompletedSuccessfully.ShouldBeTrue();
    await runner.Received(1).RunCleanup(globalOptionsProvider.GlobalOptions, Arg.Any<CancellationToken>())
        .ConfigureAwait(false);
    hostApplicationLifetime.Received(1).StopApplication();
  }

  [TestMethod]
  public async Task ExecuteAsync_RunFrequencyConfigured_RunsMultipleTimesUntilCancelled() {
    // Arrange
    var runFrequency = TimeSpan.FromMilliseconds(200);
    var globalOptionsProvider = BuildGlobalOptionsProvider(runFrequency);
    var hostApplicationLifetime = Substitute.For<IHostApplicationLifetime>();
    var logger = new FakeLogger<FrequencyBasedLoop>();
    var runner = Substitute.For<IRunner>();
    var services = new ServiceCollection()
        .AddScoped<IRunner>(_ => runner)
        .BuildServiceProvider();

    var loop = new FrequencyBasedLoop(
        globalOptionsProvider: globalOptionsProvider,
        hostApplicationLifetime: hostApplicationLifetime,
        logger: logger,
        serviceScopeFactory: services.GetRequiredService<IServiceScopeFactory>());

    // Act
    using var cancellationTokenSource = new CancellationTokenSource();
    await loop.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    await Task.Delay(750, CancellationToken.None).ConfigureAwait(false);
    await loop.StopAsync(cancellationTokenSource.Token).ConfigureAwait(false);

    // Assert
    await runner.Received().RunCleanup(globalOptionsProvider.GlobalOptions, Arg.Any<CancellationToken>()).ConfigureAwait(false);
    var callCount = runner.ReceivedCalls().Count();
    callCount.ShouldBeInRange(3, 4);
    hostApplicationLifetime.DidNotReceiveWithAnyArgs().StopApplication();
    logger.LatestRecord.Message.ShouldBe($"Waiting for {runFrequency} until next run");
  }

  [TestMethod]
  public async Task ExecuteAsync_RunnerThrowsWithoutRunFrequency_RethrowsAndStopsApplication() {
    // Arrange
    var globalOptionsProvider = BuildGlobalOptionsProvider(null);
    var hostApplicationLifetime = Substitute.For<IHostApplicationLifetime>();
    var logger = new FakeLogger<FrequencyBasedLoop>();
    var runner = Substitute.For<IRunner>();
    runner.RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .Returns(_ => throw new Exception("kaboom"));

    var services = new ServiceCollection()
        .AddScoped<IRunner>(_ => runner)
        .BuildServiceProvider();

    var loop = new FrequencyBasedLoop(
        globalOptionsProvider: globalOptionsProvider,
        hostApplicationLifetime: hostApplicationLifetime,
        logger: logger,
        serviceScopeFactory: services.GetRequiredService<IServiceScopeFactory>());

    // Act & Assert
    var cancellationTokenSource = new CancellationTokenSource();
    await loop.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    var exception = await Should.ThrowAsync<Exception>(
        async () => await loop.ExecuteTask.ShouldNotBeNull().ConfigureAwait(false)).ConfigureAwait(false);
    exception.Message.ShouldBe("kaboom");

    logger.LatestRecord.Message.ShouldBe("Runner failed with exception: kaboom");
    hostApplicationLifetime.Received().StopApplication();
  }

  [TestMethod]
  public async Task ExecuteAsync_RunnerThrowsWithRunFrequency_LogsAndContinuesRunning() {
    // Arrange
    var runFrequency = TimeSpan.FromMilliseconds(200);
    var globalOptionsProvider = BuildGlobalOptionsProvider(runFrequency);
    var hostApplicationLifetime = Substitute.For<IHostApplicationLifetime>();
    var logger = new FakeLogger<FrequencyBasedLoop>();
    var runner = Substitute.For<IRunner>();
    var runCount = 0;
    runner.RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .Returns(_ => {
          runCount++;
          if (runCount == 2) {
            throw new Exception("kaboom");
          }

          return Task.CompletedTask;
        });

    var services = new ServiceCollection()
        .AddScoped<IRunner>(_ => runner)
        .BuildServiceProvider();

    var loop = new FrequencyBasedLoop(
        globalOptionsProvider: globalOptionsProvider,
        hostApplicationLifetime: hostApplicationLifetime,
        logger: logger,
        serviceScopeFactory: services.GetRequiredService<IServiceScopeFactory>());

    // Act
    using var cancellationTokenSource = new CancellationTokenSource();
    await loop.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    await Task.Delay(750, CancellationToken.None).ConfigureAwait(false);
    await loop.StopAsync(cancellationTokenSource.Token).ConfigureAwait(false);

    // Assert
    await runner.ReceivedWithAnyArgs().RunCleanup(default!, default).ConfigureAwait(false);
    var callCount = runner.ReceivedCalls().Count();
    callCount.ShouldBeInRange(3, 4);
    hostApplicationLifetime.DidNotReceiveWithAnyArgs().StopApplication();
    logger.Collector.GetSnapshot().Select(x => x.Message).ShouldContain("Runner failed with exception: kaboom");
  }

  private static IGlobalOptionsProvider BuildGlobalOptionsProvider(TimeSpan? runFrequency) {
    var provider = Substitute.For<IGlobalOptionsProvider>();
    provider.GlobalOptions.Returns(new GlobalOptions(DryRun: true, RunFrequency: runFrequency));
    return provider;
  }
}
