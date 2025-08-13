using System.Threading.Channels;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Infrastructure.Loop;

public class ChannelTimerBackgroundService(
    Channel<DateTimeOffset> channel,
    IGlobalOptionsProvider globalOptionsProvider,
    ILogger<ChannelTimerBackgroundService> logger,
    TimeProvider timeProvider) : BackgroundService {
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    await Task.Delay(100, stoppingToken).ConfigureAwait(false);
    if (!globalOptionsProvider.GlobalOptions.RunFrequency.HasValue) {
      logger.LogInformation("Run frequency is not configured - timer will not run");
      return;
    }

    while (!stoppingToken.IsCancellationRequested) {
      try {
        await channel.Writer.WriteAsync(timeProvider.GetUtcNow(), stoppingToken).ConfigureAwait(false);
        var runFrequency = globalOptionsProvider.GlobalOptions.RunFrequency.Value;
        logger.LogTrace("Waiting for {Frequency} until next run", runFrequency);
        await Task.Delay(runFrequency, stoppingToken).ConfigureAwait(false);
      } catch (TaskCanceledException) {
        // don't throw, the while loop will exit
      }
    }
  }
}
