using System.Globalization;
using CleanupForNotion.Core.Infrastructure.Plugins;

namespace CleanupForNotion.Core.Infrastructure.State;

public static class PluginStateProviderExtensions {
  public static async Task<DateTimeOffset?> GetDateTime(
      this IPluginStateProvider pluginStateProvider,
      IPluginIdentifiers pluginIdentifiers,
      string key,
      CancellationToken cancellationToken) {
    var stringValue = await pluginStateProvider.GetString(pluginIdentifiers, key, cancellationToken).ConfigureAwait(false);
    if (stringValue == null) return null;
    return DateTimeOffset.ParseExact(stringValue, "o", CultureInfo.InvariantCulture);
  }

  public static async Task SetDateTime(
      this IPluginStateProvider pluginStateProvider,
      IPluginIdentifiers pluginIdentifiers,
      string key,
      DateTimeOffset value,
      CancellationToken cancellationToken) {
    var stringValue = value.ToString("o", CultureInfo.InvariantCulture);
    await pluginStateProvider.SetString(pluginIdentifiers, key, stringValue, cancellationToken).ConfigureAwait(false);
  }

  public static async Task<int?> GetInteger(
      this IPluginStateProvider pluginStateProvider,
      IPluginIdentifiers pluginIdentifiers,
      string key,
      CancellationToken cancellationToken) {
    var stringValue = await pluginStateProvider.GetString(pluginIdentifiers, key, cancellationToken).ConfigureAwait(false);
    if (stringValue == null) return null;
    return int.Parse(stringValue, CultureInfo.InvariantCulture);
  }

  public static async Task SetInteger(
      this IPluginStateProvider pluginStateProvider,
      IPluginIdentifiers pluginIdentifiers,
      string key,
      int value,
      CancellationToken cancellationToken) {
    var stringValue = value.ToString(CultureInfo.InvariantCulture);
    await pluginStateProvider.SetString(pluginIdentifiers, key, stringValue, cancellationToken).ConfigureAwait(false);
  }
}
