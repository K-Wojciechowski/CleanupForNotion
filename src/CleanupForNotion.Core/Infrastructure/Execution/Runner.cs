using System.Diagnostics;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using CleanupForNotion.Core.Infrastructure.Semaphores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupForNotion.Core.Infrastructure.Execution;

public class Runner(
  ICfnNotionClient notionClient,
  ILogger<Runner> logger,
  INotificationSender notificationSender,
  IOptions<CfnOptions> options,
  IPluginActivator pluginActivator,
  IPluginSpecificationParser pluginSpecificationParser,
  IRunnerSemaphore runnerSemaphore
) : IRunner {
  private const string EmptyPluginsMessage = "Plugin configuration is missing, check your appsettings.json file";
  private const string AlreadyRunningMessage = "A cleanup is already running.";
  private static readonly TimeSpan _semaphoreWaitTime = TimeSpan.FromSeconds(10);

  public async Task RunCleanup(GlobalOptions globalOptions, CancellationToken cancellationToken) {
    // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
    var rawPlugins = options.Value?.Plugins;
    if (rawPlugins == null || rawPlugins.Count == 0) {
      logger.LogCritical(EmptyPluginsMessage);
      throw new InvalidConfigurationException(EmptyPluginsMessage);
    }

    SemaphoreHolder semaphore;
    try {
      semaphore = await runnerSemaphore.AcquireAsync(_semaphoreWaitTime, cancellationToken).ConfigureAwait(false);
    } catch (TimeoutException exc) {
      logger.LogCritical(AlreadyRunningMessage);
      throw new TimeoutException(AlreadyRunningMessage, exc);
    }

    using var _ = semaphore;


    var plugins = rawPlugins
      .Select(pluginSpecificationParser.ParseSpecification)
      .Select(pluginActivator.ActivatePlugin)
      .ToArray();

    foreach (var plugin in plugins) {
      logger.LogInformation("Running plugin {Plugin} ({Description})", plugin, plugin.Description);
      var stopwatch = Stopwatch.StartNew();
      await plugin.Run(notionClient, globalOptions, cancellationToken).ConfigureAwait(false);
      logger.LogInformation("Finished plugin {Plugin} in {Duration} ms", plugin, stopwatch.ElapsedMilliseconds);
    }

    await notificationSender.NotifyRunFinished(globalOptions.DryRun, cancellationToken).ConfigureAwait(false);
  }
}
