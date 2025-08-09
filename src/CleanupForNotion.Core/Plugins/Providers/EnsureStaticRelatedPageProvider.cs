using CleanupForNotion.Core.Infrastructure.ConfigModels;
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
      string databaseId,
      string propertyName,
      TimeSpan? gracePeriod) {
    return new EnsureStaticRelatedPageOptions(
        databaseId,
        propertyName,
        options.GetString("RelatedPageId"),
        gracePeriod);
  }

  protected override EnsureStaticRelatedPage CreatePlugin(
      ILogger<EnsureStaticRelatedPage> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      EnsureStaticRelatedPageOptions options,
      string pluginDescription) {
    return new EnsureStaticRelatedPage(logger, timeProvider, options, pluginDescription);
  }
}
