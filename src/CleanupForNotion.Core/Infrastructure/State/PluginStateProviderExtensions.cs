using System.Globalization;

namespace CleanupForNotion.Core.Infrastructure.State;

public static class PluginStateProviderExtensions {
  public static async Task<DateTimeOffset?> GetDateTime(
      this IPluginStateProvider pluginStateProvider,
      string pluginName,
      string pluginDescription,
      string key,
      CancellationToken cancellationToken) {
    var stringValue = await pluginStateProvider.GetString(pluginName, pluginDescription, key, cancellationToken).ConfigureAwait(false);
    if (stringValue == null) return null;
    return DateTimeOffset.ParseExact(stringValue, "o", CultureInfo.InvariantCulture);
  }

  public static async Task SetDateTime(
      this IPluginStateProvider pluginStateProvider,
      string pluginName,
      string pluginDescription,
      string key,
      DateTimeOffset value,
      CancellationToken cancellationToken) {
    var stringValue = value.ToString("o", CultureInfo.InvariantCulture);
    await pluginStateProvider.SetString(pluginName, pluginDescription, key, stringValue, cancellationToken).ConfigureAwait(false);
  }
}
