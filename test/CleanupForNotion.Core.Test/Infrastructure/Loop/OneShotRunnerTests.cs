using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.Loop;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Infrastructure.Loop;

[TestClass]
public class OneShotLoopTests {
  [TestMethod]
  public async Task ExecuteAsync_Called_RunsOneCleanupAndStopsApplication() {
    // Arrange
    var globalOptionsProvider = Substitute.For<IGlobalOptionsProvider>();
    globalOptionsProvider.GlobalOptions.Returns(new GlobalOptions(RunFrequency: TimeSpan.FromMilliseconds(100)));
    var logger = NullLogger<OneShotLoop>.Instance;
    var runner = Substitute.For<IRunner>();

    runner.RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .Returns(Task.CompletedTask);

    var services = new ServiceCollection()
        .AddScoped<IRunner>(_ => runner)
        .BuildServiceProvider();

    var loop = new OneShotLoop(
        globalOptionsProvider,
        logger,
        services.GetRequiredService<IServiceScopeFactory>());

    // Act
    using var cancellationTokenSource = new CancellationTokenSource();
    await loop.ExecuteAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    await cancellationTokenSource.CancelAsync().ConfigureAwait(false);

    // Assert
    await runner.ReceivedWithAnyArgs(1).RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);
  }

  [TestMethod]
  public async Task ExecuteAsync_RunnerThrows_LogsAndStopsApplication() {
    // Arrange
    var globalOptionsProvider = Substitute.For<IGlobalOptionsProvider>();
    globalOptionsProvider.GlobalOptions.Returns(new GlobalOptions(RunFrequency: TimeSpan.FromMilliseconds(100)));
    var logger = new FakeLogger<OneShotLoop>();
    var runner = Substitute.For<IRunner>();

    runner.RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .Returns(async _ => {
          await Task.Delay(100).ConfigureAwait(false);
          throw new Exception("kaboom");
        });

    var services = new ServiceCollection()
        .AddScoped(_ => runner)
        .BuildServiceProvider();

    var lambdaHostedServiceWrapper = new OneShotLoop(
        globalOptionsProvider,
        logger,
        services.GetRequiredService<IServiceScopeFactory>());

    // Act
    using var cancellationTokenSource = new CancellationTokenSource();
    var runTask = lambdaHostedServiceWrapper.ExecuteAsync(cancellationTokenSource.Token);
    await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
    await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
    Func<Task> act = async () => await runTask.ConfigureAwait(false);

    // Assert
    (await act.ShouldThrowAsync<Exception>().ConfigureAwait(false)).Message.ShouldBe("kaboom");
    await runner.ReceivedWithAnyArgs(1).RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);
    logger.LatestRecord.Message.ShouldContain("kaboom");
  }
}
