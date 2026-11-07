using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Core.Plugins.Providers;

public class EnsureStaticRelatedPageProvider(
    ILoggerFactory loggerFactory,
    IPluginStateProvider pluginStateProvider,
    TimeProvider timeProvider)
    : BasicPluginProviderBase<EnsureStaticRelatedPage, EnsureStaticRelatedPageOptions>(
        loggerFactory,
        pluginStateProvider,
        timeProvider) {
  public override string Name => "EnsureStaticRelatedPage";

  protected override EnsureStaticRelatedPageOptions GetOptions(
      RawPluginOptions options,
      string dataSourceId,
      string propertyName,
      TimeSpan? gracePeriod,
      int pluginIndex) {
    return new EnsureStaticRelatedPageOptions(
        dataSourceId,
        propertyName,
        options.GetString("RelatedPageId"),
        gracePeriod);
  }

  protected override IPlugin CreatePlugin(
      ILogger<EnsureStaticRelatedPage> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      EnsureStaticRelatedPageOptions options,
      string pluginDescription,
      string? pluginId) {
    return new EnsureStaticRelatedPage(logger, timeProvider, options, pluginDescription, pluginId);
  }
}
