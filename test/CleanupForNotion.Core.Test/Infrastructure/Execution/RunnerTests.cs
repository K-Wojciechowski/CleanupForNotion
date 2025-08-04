using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using CleanupForNotion.Core.Infrastructure.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Infrastructure.Execution;

[TestClass]
public sealed class RunnerTests {
  private readonly IPluginActivator _pluginActivator;
  private readonly IPluginSpecificationParser _pluginSpecificationParser;

  public RunnerTests() {
    _pluginSpecificationParser = Substitute.For<IPluginSpecificationParser>();
    _pluginSpecificationParser.ParseSpecification(null!).ReturnsForAnyArgs(callInfo => {
      var options = callInfo.Arg<Dictionary<string, object>>();
      var name = (string)options["Name"];
      return new PluginSpecification(name, name + "desc", new RawPluginOptions(options));
    });

    _pluginActivator = Substitute.For<IPluginActivator>();
    _pluginActivator.ActivatePlugin(null!).ReturnsForAnyArgs(callInfo => new FakePlugin(
        callInfo.Arg<PluginSpecification>()));
  }

  [TestMethod]
  public async Task RunCleanup_NullPlugins_ThrowsException()
    => await RunCleanup_NoPlugins_ThrowsException(null).ConfigureAwait(false);

  [TestMethod]
  public async Task RunCleanup_NullConfig_ThrowsException()
    => await RunCleanup_NoPlugins_ThrowsException(null, nullConfig: true).ConfigureAwait(false);

  [TestMethod]
  public async Task RunCleanup_EmptyPlugins_ThrowsException()
    => await RunCleanup_NoPlugins_ThrowsException([]).ConfigureAwait(false);

  [TestMethod]
  public async Task RunCleanup_ValidPlugins_RunsInOrder() {
    // Arrange
    var logger = new FakeLogger<Runner>();

    var notificationSender = Substitute.For<INotificationSender>();

    var rawPlugins = new List<Dictionary<string, object>> {
        new() { { "Name", "Plugin1" } }, new() { { "Name", "Plugin2" } }
    };

    var runner = new Runner(
        notionClient: null!,
        logger: logger,
        notificationSender: notificationSender,
        options: TestHelpers.GetCfnOptions(rawPlugins),
        pluginActivator: _pluginActivator,
        pluginSpecificationParser: _pluginSpecificationParser,
        runnerSemaphore: new RunnerSemaphore()
    );

    await runner.RunCleanup(new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    var logMessages = logger.Collector.GetSnapshot();
    logMessages.ShouldAllBe(l => l.Level == LogLevel.Information);

    logMessages[0].Message.ShouldBe("Running plugin Plugin1 (Plugin1desc)");
    logMessages[1].Message.ShouldMatch(@"Finished plugin Plugin1 in \d+(\.\d+)? ms");
    logMessages[2].Message.ShouldBe("Running plugin Plugin2 (Plugin2desc)");
    logMessages[3].Message.ShouldMatch(@"Finished plugin Plugin2 in \d+(\.\d+)? ms");

    await notificationSender.ReceivedWithAnyArgs().NotifyRunFinished(default, default).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task RunCleanup_SemaphoreTakenButReleasedWithinTenSeconds_RunsCleanup() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();

    var rawPlugins = new List<Dictionary<string, object>> { new() { { "Name", "Plugin1" } } };

    var semaphore = new RunnerSemaphore();

    var runner = new Runner(
        notionClient: null!,
        logger: NullLogger<Runner>.Instance,
        notificationSender: notificationSender,
        options: TestHelpers.GetCfnOptions(rawPlugins),
        pluginActivator: _pluginActivator,
        pluginSpecificationParser: _pluginSpecificationParser,
        runnerSemaphore: semaphore
    );

    var semaphoreHolder = await semaphore.AcquireAsync(TimeSpan.FromSeconds(1), CancellationToken.None).ConfigureAwait(false);

    var runTask = runner.RunCleanup(new GlobalOptions(), CancellationToken.None);

    await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
    semaphoreHolder.Dispose();
    await runTask.ConfigureAwait(false);

    await notificationSender.ReceivedWithAnyArgs().NotifyRunFinished(default, default).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task RunCleanup_SemaphoreTakenAndNotReleased_ThrowsException() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();

    var rawPlugins = new List<Dictionary<string, object>> { new() { { "Name", "Plugin1" } } };

    var semaphore = new RunnerSemaphore();

    var runner = new Runner(
        notionClient: null!,
        logger: NullLogger<Runner>.Instance,
        notificationSender: notificationSender,
        options: TestHelpers.GetCfnOptions(rawPlugins),
        pluginActivator: _pluginActivator,
        pluginSpecificationParser: _pluginSpecificationParser,
        runnerSemaphore: semaphore
    );

    using var _ = await semaphore.AcquireAsync(TimeSpan.FromSeconds(1), CancellationToken.None).ConfigureAwait(false);

    Func<Task> act = async () => await runner.RunCleanup(new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    (await act.ShouldThrowAsync<TimeoutException>().ConfigureAwait(false))
        .Message.ShouldBe("A cleanup is already running.");

    await notificationSender.DidNotReceiveWithAnyArgs().NotifyRunFinished(default, default).ConfigureAwait(false);
  }

  private async Task RunCleanup_NoPlugins_ThrowsException(List<Dictionary<string, object>>? plugins, bool nullConfig = false) {
    // Arrange
    var logger = new FakeLogger<Runner>();
    var notificationSender = Substitute.For<INotificationSender>();
    var runner = new Runner(
        notionClient: null!,
        logger: logger,
        notificationSender: notificationSender,
        options: nullConfig ? new OptionsWrapper<CfnOptions>(null!) : TestHelpers.GetCfnOptions(plugins),
        pluginActivator: null!,
        pluginSpecificationParser: null!,
        runnerSemaphore: new RunnerSemaphore()
    );

    const string emptyPluginsMessage = "Plugin configuration is missing, check your appsettings.json file";

    // Act
    Func<Task> act = async () =>
        await runner.RunCleanup(globalOptions: new GlobalOptions(), CancellationToken.None).ConfigureAwait(false);

    // Assert
    (await act.ShouldThrowAsync<InvalidConfigurationException>().ConfigureAwait(false)).Message.ShouldBe(
        emptyPluginsMessage);
    logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Critical);
    logger.Collector.LatestRecord.Message.ShouldBe(emptyPluginsMessage);
    await notificationSender.DidNotReceiveWithAnyArgs().NotifyRunFinished(default, default).ConfigureAwait(false);
  }

  private class FakePlugin(PluginSpecification pluginSpecification) : IPlugin {
    public string Name { get; } = pluginSpecification.PluginName;
    public string Description { get; } = pluginSpecification.PluginDescription;

    public Task Run(ICfnNotionClient client, GlobalOptions globalOptions, CancellationToken cancellationToken) {
      return Task.CompletedTask;
    }

    public override string ToString() => Name;
  }
}
