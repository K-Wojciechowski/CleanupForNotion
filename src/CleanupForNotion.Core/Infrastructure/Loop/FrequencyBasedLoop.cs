using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Infrastructure.Loop;

public class FrequencyBasedLoop(
    IGlobalOptionsProvider globalOptionsProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<FrequencyBasedLoop> logger,
    IServiceScopeFactory serviceScopeFactory)
    : BackgroundService {
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    await Task.Yield();

    while (!stoppingToken.IsCancellationRequested) {
      using (var serviceScope = serviceScopeFactory.CreateScope()) {
        var runner = serviceScope.ServiceProvider.GetRequiredService<IRunner>();
        try {
          await runner.RunCleanup(globalOptionsProvider.GlobalOptions, stoppingToken).ConfigureAwait(false);
        } catch (Exception exc) {
          logger.LogError(exc, "Runner failed with exception: {Error}", exc.Message);
          if (!globalOptionsProvider.GlobalOptions.RunFrequency.HasValue) {
            hostApplicationLifetime.StopApplication();
            throw;
          }
        }
      }

      if (globalOptionsProvider.GlobalOptions.RunFrequency.HasValue) {
        var runFrequency = globalOptionsProvider.GlobalOptions.RunFrequency.Value;
        logger.LogTrace("Waiting for {Frequency} until next run", runFrequency);
        await Task.Delay(runFrequency, stoppingToken).ConfigureAwait(false);
      } else {
        hostApplicationLifetime.StopApplication();
        break;
      }
    }
  }
}
