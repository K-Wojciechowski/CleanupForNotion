using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Infrastructure.State.Factories;
using CleanupForNotion.Core.Plugins.Providers;
using CleanupForNotion.Core.Webhooks;
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
        .AddSingleton<IPluginStateProviderFactory, InMemoryPluginStateProviderFactory>()
        .AddSingleton<IPluginStateProviderFactory, JsonFilePluginStateProviderFactory>()
        .AddSingleton<IPluginStateProviderFactory, PostgresPluginStateProviderFactory>()
        .AddSingleton<IPluginProvider, DeleteByCheckboxProvider>()
        .AddSingleton<IPluginProvider, DeleteOnNewMonthlyCycleProvider>()
        .AddSingleton<IPluginProvider, DeleteWithoutRelationshipsProvider>()
        .AddSingleton<IPluginProvider, DeleteZeroSumProvider>()
        .AddSingleton<IPluginProvider, EnsureStaticRelatedPageProvider>()
        .AddSingleton<IPluginProvider, EstimateExpensesWithSubscriptionsProvider>()
        .AddSingleton<IRunnerSemaphore, RunnerSemaphore>()
        .AddSingleton<IWebhookProcessor, WebhookProcessor>()
        .AddSingleton<IPluginStateProvider>(s => {
          var factory = s.GetServices<IPluginStateProviderFactory>().SingleOrDefault(f => f.CanCreate());
          return factory?.Create() ?? throw new InvalidConfigurationException("Unrecognised StateEngine configured");
        });
  }
}
