using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupForNotion.Core.Infrastructure.State.Factories;

internal class JsonFilePluginStateProviderFactory(
    ILoggerFactory loggerFactory,
    INotificationSender notificationSender,
    IOptions<CfnOptions> options) : IPluginStateProviderFactory {
  public string Name => "File";

  public bool CanCreate() => string.Equals(options.Value.StateEngine, Name, StringComparison.OrdinalIgnoreCase);

  public IPluginStateProvider Create()
    => new JsonFilePluginStateProvider(
        loggerFactory.CreateLogger<JsonFilePluginStateProvider>(),
        notificationSender,
        options);
}
