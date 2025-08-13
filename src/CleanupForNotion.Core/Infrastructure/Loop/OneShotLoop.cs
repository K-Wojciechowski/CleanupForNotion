using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Infrastructure.Loop;

public class OneShotLoop(
    IGlobalOptionsProvider globalOptionsProvider,
    ILogger<OneShotLoop> logger,
    IServiceScopeFactory serviceScopeFactory) {
  public async Task ExecuteAsync(CancellationToken stoppingToken) {
    await Task.Yield();

    using var serviceScope = serviceScopeFactory.CreateScope();

    var runner = serviceScope.ServiceProvider.GetRequiredService<IRunner>();
    try {
      await runner.RunCleanup(globalOptionsProvider.GlobalOptions, stoppingToken).ConfigureAwait(false);
    } catch (Exception exc) {
      logger.LogError(exc, "Runner failed with exception: {Error}", exc.Message);
      throw;
    }
  }
}
