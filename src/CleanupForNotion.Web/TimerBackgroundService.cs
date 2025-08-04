using System.Threading.Channels;
using CleanupForNotion.Core.Infrastructure.PluginManagement;

namespace CleanupForNotion.Web;

public class TimerBackgroundService(
    Channel<DateTimeOffset> channel,
    IGlobalOptionsProvider globalOptionsProvider,
    ILogger<TimerBackgroundService> logger,
    TimeProvider timeProvider) : CfnBackgroundService {
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    await ServiceStartupDelay(stoppingToken).ConfigureAwait(false);
    while (!stoppingToken.IsCancellationRequested) {
      if (!globalOptionsProvider.GlobalOptions.RunFrequency.HasValue) {
        return;
      }

      await channel.Writer.WriteAsync(timeProvider.GetUtcNow(), stoppingToken).ConfigureAwait(false);
      var runFrequency = globalOptionsProvider.GlobalOptions.RunFrequency.Value;
      logger.LogTrace("Waiting for {Frequency} until next run", runFrequency);
      await Task.Delay(runFrequency, stoppingToken).ConfigureAwait(false);
    }
  }
}
