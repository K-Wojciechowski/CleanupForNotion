using System.Text;
using System.Text.Json;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.Semaphores;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CleanupForNotion.Core.Infrastructure.State;

public class JsonFilePluginStateProvider : IPluginStateProvider, INotificationListener, IDisposable {
  private readonly ILogger<JsonFilePluginStateProvider> _logger;
  private readonly INotificationSender _notificationSender;

  private readonly SemaphoreSlim _semaphore = new(1, 1);
  private readonly string? _filePath;
  private Dictionary<string, string> _data;
  private bool _initialized;
  private bool _dirty;

  public JsonFilePluginStateProvider(
      ILogger<JsonFilePluginStateProvider> logger,
      INotificationSender notificationSender,
      IOptions<CfnOptions> options) {
    _logger = logger;
    _notificationSender = notificationSender;
    _notificationSender.Register(this);
    _filePath = options.Value.StateFilePath;

    _data = [];
  }

  #region Data access

  public async Task<string?> GetString(string pluginName, string pluginDescription, string key, CancellationToken cancellationToken) {
    using var _ = await GetAccess(cancellationToken).ConfigureAwait(false);
    var encodedKey = GetEncodedKey(pluginName, pluginDescription, key);
    return _data.GetValueOrDefault(encodedKey);
  }

  public async Task SetString(string pluginName, string pluginDescription, string key, string value, CancellationToken cancellationToken) {
    using var _ = await GetAccess(cancellationToken).ConfigureAwait(false);
    var encodedKey = GetEncodedKey(pluginName, pluginDescription, key);
    _data[encodedKey] = value;
    _dirty = true;
  }

  public async Task Remove(string pluginName, string pluginDescription, string key, CancellationToken cancellationToken) {
    using var _ = await GetAccess(cancellationToken).ConfigureAwait(false);
    var encodedKey = GetEncodedKey(pluginName, pluginDescription, key);
    _data.Remove(encodedKey);
    _dirty = true;
  }

  #endregion Data access

  #region Notification listener

  public void Dispose() {
    Save();
    _notificationSender.Unregister(this);
    _semaphore.Dispose();
    GC.SuppressFinalize(this);
  }

  public async Task OnRunFinished(bool dryRun, CancellationToken cancellationToken) {
    if (dryRun) return;
    await SaveAsync(cancellationToken).ConfigureAwait(false);
  }

  #endregion Notification listener

  #region File handling

  private async Task<SemaphoreHolder> GetAccess(CancellationToken cancellationToken) {
    var semaphoreHolder = await _semaphore.AcquireAsync(cancellationToken).ConfigureAwait(false);
    if (_initialized || _filePath == null) return semaphoreHolder;

    try {
      var json = File.ReadAllText(_filePath);
      _data = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ??
              throw new Exception("Null state deserialized");
    } catch (Exception exc) {
      _logger.LogError(exc, "Failed to load state file, will use blank state");
      _data = [];
    }

    _initialized = true;
    return semaphoreHolder;
  }

  private void Save() {
    using var _ = _semaphore.Acquire();
    if (!_dirty || _filePath == null) return;

    var json = JsonSerializer.Serialize(_data);
    File.WriteAllText(_filePath, json);
    _dirty = false;
  }

  private async Task SaveAsync(CancellationToken cancellationToken) {
    using var _ = await _semaphore.AcquireAsync(cancellationToken).ConfigureAwait(false);
    if (!_dirty || _filePath == null) return;

    var json = JsonSerializer.Serialize(_data, JsonSerializerOptions.Default);
    await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
    _dirty = false;
  }

  #endregion

  #region Helpers

  private static string GetEncodedKey(string pluginName, string pluginDescription, string key) {
    var sb = new StringBuilder(2 + pluginName.Length + pluginDescription.Length);
    sb.Append(pluginName);
    sb.Append('\x1c');
    sb.Append(pluginDescription);
    sb.Append('\x1d');
    sb.Append(key);
    return sb.ToString();
  }

  #endregion Helpers
}
