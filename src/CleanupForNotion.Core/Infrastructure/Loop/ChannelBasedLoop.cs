using System.Threading.Channels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Infrastructure.Loop;

public class ChannelBasedLoop(
    Channel<DateTimeOffset> channel,
    IGlobalOptionsProvider globalOptionsProvider,
    ILogger<ChannelBasedLoop> logger,
    TimeProvider timeProvider,
    IServiceScopeFactory serviceScopeFactory)
    : BackgroundService {
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    await Task.Delay(100, stoppingToken).ConfigureAwait(false);

    while (!stoppingToken.IsCancellationRequested) {
      var trigger = await channel.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);

      var startTime = timeProvider.GetUtcNow();
      logger.LogTrace("Received trigger from {TriggerDate:O} at {Date:O} (in {Duration} ms)", trigger, startTime,
          (startTime - trigger).TotalMilliseconds);

      using var serviceScope = serviceScopeFactory.CreateScope();
      var runner = serviceScope.ServiceProvider.GetRequiredService<IRunner>();
      try {
        await runner.RunCleanup(globalOptionsProvider.GlobalOptions, stoppingToken).ConfigureAwait(false);
      } catch (Exception exc) {
        logger.LogError(exc, "Failed trigger from {TriggerTime:O} with exception: {Error}", trigger, exc.Message);
        continue;
      }

      var endTime = timeProvider.GetUtcNow();
      logger.LogTrace("Finished trigger from {TriggerTime:O} at {Date:O} (in {Duration} ms)", trigger, endTime,
          (endTime - startTime).TotalMilliseconds);
    }
  }
}
