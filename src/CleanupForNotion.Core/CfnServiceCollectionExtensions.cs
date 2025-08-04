using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace CleanupForNotion.Core;

public static class CfnServiceCollectionExtensions {
  public static IServiceCollection AddCfnServices(this IServiceCollection services) {
    return services
      .AddScoped<ICfnNotionClient, CfnNotionClient>()
      .AddScoped<IPluginActivator, PluginActivator>()
      .AddScoped<IPluginSpecificationParser, PluginSpecificationParser>()
      .AddScoped<IRunner, Runner>()
      .AddSingleton<IGlobalOptionsProvider, GlobalOptionsProvider>()
      .AddSingleton<INotificationSender, NotificationSender>()
      .AddSingleton<IPluginStateProvider, JsonFilePluginStateProvider>()
      .AddSingleton<IPluginProvider, DeleteByCheckboxProvider>()
      .AddSingleton<IPluginProvider, DeleteOnNewMonthlyCycleProvider>()
      .AddSingleton<IPluginProvider, DeleteWithoutRelationshipsProvider>()
      .AddSingleton<IPluginProvider, DeleteZeroSumProvider>()
      .AddSingleton<IRunnerSemaphore, RunnerSemaphore>();
  }
}
