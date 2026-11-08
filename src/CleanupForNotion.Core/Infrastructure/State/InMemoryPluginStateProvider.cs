using System.Text;
using CleanupForNotion.Core.Infrastructure.Plugins;

namespace CleanupForNotion.Core.Infrastructure.State;

public class InMemoryPluginStateProvider : IPluginStateProvider {
  private readonly Dictionary<string, string> _data = [];

  public Task<string?> GetString(IPluginIdentifiers pluginIdentifiers, string key, CancellationToken cancellationToken) {
    var encodedKey = GetEncodedKey(pluginIdentifiers, key);
    return Task.FromResult(_data.GetValueOrDefault(encodedKey));
  }

  public Task SetString(IPluginIdentifiers pluginIdentifiers, string key, string value, CancellationToken cancellationToken) {
    var encodedKey = GetEncodedKey(pluginIdentifiers, key);
    _data[encodedKey] = value;
    return Task.CompletedTask;
  }

  public Task Remove(IPluginIdentifiers pluginIdentifiers, string key, CancellationToken cancellationToken) {
    var encodedKey = GetEncodedKey(pluginIdentifiers, key);
    _data.Remove(encodedKey);
    return Task.CompletedTask;
  }

  private static string GetEncodedKey(IPluginIdentifiers pluginIdentifiers, string key) {
    var pluginName = pluginIdentifiers.Name;
    var pluginDescription = pluginIdentifiers.Description;
    var sb = new StringBuilder(2 + pluginName.Length + pluginDescription.Length);
    sb.Append(pluginName);
    sb.Append('\x1c');
    sb.Append(pluginDescription);
    sb.Append('\x1d');
    sb.Append(key);
    return sb.ToString();
  }
}
