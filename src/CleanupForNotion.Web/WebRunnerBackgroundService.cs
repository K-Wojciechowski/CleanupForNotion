using System.Threading.Channels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.PluginManagement;

namespace CleanupForNotion.Web;

public class WebRunnerBackgroundService(
    Channel<DateTimeOffset> channel,
    IGlobalOptionsProvider globalOptionsProvider,
    ILogger<WebRunnerBackgroundService> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory)
    : CfnBackgroundService {
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    await ServiceStartupDelay(stoppingToken).ConfigureAwait(false);

    while (!stoppingToken.IsCancellationRequested) {
      var trigger = await channel.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);
      var startTime = timeProvider.GetUtcNow();
      logger.LogTrace("Received trigger from {TriggerDate} at {Date} (in {Duration} ms)", trigger, startTime,
          (startTime - trigger).TotalMilliseconds);

      using var serviceScope = serviceScopeFactory.CreateScope();
      var runner = serviceScope.ServiceProvider.GetRequiredService<IRunner>();
      try {
        await runner.RunCleanup(globalOptionsProvider.GlobalOptions, stoppingToken).ConfigureAwait(false);
      } catch (Exception exc) {
        logger.LogError(exc, "Runner failed with exception: {Error}", exc.Message);
      }

      var endTime = timeProvider.GetUtcNow();
      logger.LogTrace("Finished trigger from {TriggerDate} at {Date} (in {Duration} ms)", trigger, endTime,
          (endTime - startTime).TotalMilliseconds);
    }
  }
}
