using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Plugins.Providers;

public abstract class BasicPluginProviderBase<TPlugin, TOptions>(
  ILoggerFactory loggerFactory,
  IPluginStateProvider pluginStateProvider,
  TimeProvider timeProvider) : IPluginProvider
  where TPlugin : IPlugin
  where TOptions : IBasicPluginOptions {
  public abstract string Name { get; }

  public IPlugin GetPlugin(PluginSpecification pluginSpecification) {
    var rawOptions = pluginSpecification.RawOptions;
    var databaseId = rawOptions.GetString("DatabaseId");
    var propertyName = rawOptions.GetString("PropertyName");
    var gracePeriod = rawOptions.GetOptionalTimeSpan("GracePeriod");

    var options = GetOptions(rawOptions, databaseId, propertyName, gracePeriod);
    var logger = loggerFactory.CreateLogger<TPlugin>();

    return CreatePlugin(logger, pluginStateProvider, timeProvider, options, pluginSpecification.PluginDescription);
  }

  protected abstract TOptions GetOptions(RawPluginOptions options, string databaseId, string propertyName, TimeSpan? gracePeriod);

  protected abstract IPlugin CreatePlugin(
      ILogger<TPlugin> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      TOptions options,
      string pluginDescription);
}
