using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Aws;

public class LambdaHostedServiceWrapper(
    IGlobalOptionsProvider globalOptionsProvider,
    IHostApplicationLifetime hostApplicationLifetime,
    ILogger<LambdaHostedServiceWrapper> logger,
    IServiceScopeFactory serviceScopeFactory)
    : BackgroundService {
  protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
    await Task.Yield();

    using (var serviceScope = serviceScopeFactory.CreateScope()) {
      var runner = serviceScope.ServiceProvider.GetRequiredService<IRunner>();
      try {
        await runner.RunCleanup(globalOptionsProvider.GlobalOptions, stoppingToken).ConfigureAwait(false);
      } catch (Exception exc) {
        logger.LogError(exc, "Runner failed with exception: {Error}", exc.Message);
      }
    }

    hostApplicationLifetime.StopApplication();
  }
}
