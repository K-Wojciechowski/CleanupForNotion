using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace CleanupForNotion.Aws.Test;

[TestClass]
public class LambdaHostedServiceWrapperTests {
  [TestMethod]
  public async Task ExecuteAsync_Called_RunsOneCleanupAndStopsApplication() {
    // Arrange
    var globalOptionsProvider = Substitute.For<IGlobalOptionsProvider>();
    globalOptionsProvider.GlobalOptions.Returns(new GlobalOptions(RunFrequency: TimeSpan.FromMilliseconds(100)));
    var hostApplicationLifetime = Substitute.For<IHostApplicationLifetime>();
    var logger = NullLogger<LambdaHostedServiceWrapper>.Instance;
    var runner = Substitute.For<IRunner>();

    runner.RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .Returns(Task.CompletedTask);

    var services = new ServiceCollection()
        .AddScoped<IRunner>(_ => runner)
        .BuildServiceProvider();

    var lambdaHostedServiceWrapper = new LambdaHostedServiceWrapper(
        globalOptionsProvider,
        hostApplicationLifetime,
        logger,
        services.GetRequiredService<IServiceScopeFactory>());

    // Act
    using var cancellationTokenSource = new CancellationTokenSource();
    await lambdaHostedServiceWrapper.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
    await cancellationTokenSource.CancelAsync().ConfigureAwait(false);

    // Assert
    await runner.ReceivedWithAnyArgs(1).RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
    hostApplicationLifetime.Received().StopApplication();
  }

  [TestMethod]
  public async Task ExecuteAsync_RunnerThrows_LogsAndStopsApplication() {
    // Arrange
    var globalOptionsProvider = Substitute.For<IGlobalOptionsProvider>();
    globalOptionsProvider.GlobalOptions.Returns(new GlobalOptions(RunFrequency: TimeSpan.FromMilliseconds(100)));
    var hostApplicationLifetime = Substitute.For<IHostApplicationLifetime>();
    var logger = new FakeLogger<LambdaHostedServiceWrapper>();
    var runner = Substitute.For<IRunner>();

    runner.RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>())
        .Throws(new Exception("kaboom"));

    var services = new ServiceCollection()
        .AddScoped(_ => runner)
        .BuildServiceProvider();

    var lambdaHostedServiceWrapper = new LambdaHostedServiceWrapper(
        globalOptionsProvider,
        hostApplicationLifetime,
        logger,
        services.GetRequiredService<IServiceScopeFactory>());

    // Act
    using var cancellationTokenSource = new CancellationTokenSource();
    await lambdaHostedServiceWrapper.StartAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    await Task.Delay(TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
    await cancellationTokenSource.CancelAsync().ConfigureAwait(false);

    // Assert
    await runner.ReceivedWithAnyArgs(1).RunCleanup(Arg.Any<GlobalOptions>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
    logger.LatestRecord.Message.ShouldContain("kaboom");
    hostApplicationLifetime.Received().StopApplication();
  }
}
