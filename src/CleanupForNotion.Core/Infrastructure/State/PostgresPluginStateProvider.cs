using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CleanupForNotion.Core.Infrastructure.State;

public class PostgresPluginStateProvider : IPluginStateProvider, INotificationListener, IDisposable {
  private readonly INotificationSender _notificationSender;

  private readonly SemaphoreSlim _semaphore = new(1, 1);
  private readonly string _connectionString;
  private NpgsqlDataSource? _dataSource;
  private NpgsqlConnection? _connection;
  private NpgsqlTransaction? _transaction;

  private const string TableCreateSql = """
                                        CREATE TABLE IF NOT EXISTS cfn_state (
                                          pluginName VARCHAR(255) NOT NULL,
                                          pluginDescription VARCHAR(255) NOT NULL,
                                          key VARCHAR(255) NOT NULL,
                                          value TEXT NULL,
                                          PRIMARY KEY (pluginName, pluginDescription, key)
                                        );
                                        """;

  public PostgresPluginStateProvider(
      INotificationSender notificationSender,
      IOptions<CfnOptions> options) {
    _notificationSender = notificationSender;
    _notificationSender.Register(this);
    _connectionString = options.Value.StateAccessDetails ??
                        throw new InvalidConfigurationException("Connection string is null");
  }

  #region Data access

  public async Task<string?> GetString(IPluginIdentifiers pluginIdentifiers, string key,
      CancellationToken cancellationToken) {
    var connection = await GetConnection(cancellationToken).ConfigureAwait(false);
    var cmd = connection.CreateCommand();

    await using (cmd.ConfigureAwait(false)) {
      cmd.CommandText =
          "SELECT value FROM cfn_state WHERE pluginName = @pluginName AND pluginDescription = @pluginDescription AND key = @key";
      cmd.Parameters.AddWithValue("pluginName", pluginIdentifiers.Name);
      cmd.Parameters.AddWithValue("pluginDescription", pluginIdentifiers.Description);
      cmd.Parameters.AddWithValue("key", key);
      await cmd.PrepareAsync(cancellationToken).ConfigureAwait(false);
      return (await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) as string;
    }
  }

  public async Task SetString(IPluginIdentifiers pluginIdentifiers, string key, string value,
      CancellationToken cancellationToken) {
    var connection = await GetConnection(cancellationToken).ConfigureAwait(false);
    var cmd = connection.CreateCommand();

    await using (cmd.ConfigureAwait(false)) {
      cmd.CommandText = """
                        INSERT INTO cfn_state (pluginName, pluginDescription, key, value)
                        VALUES (@pluginName, @pluginDescription, @key, @value)
                        ON CONFLICT (pluginName, pluginDescription, key)
                        DO UPDATE SET value = @value
                        """;
      cmd.Parameters.AddWithValue("pluginName", pluginIdentifiers.Name);
      cmd.Parameters.AddWithValue("pluginDescription", pluginIdentifiers.Description);
      cmd.Parameters.AddWithValue("key", key);
      cmd.Parameters.AddWithValue("value", value);
      await cmd.PrepareAsync(cancellationToken).ConfigureAwait(false);
      await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
  }

  public async Task Remove(IPluginIdentifiers pluginIdentifiers, string key, CancellationToken cancellationToken) {
    var connection = await GetConnection(cancellationToken).ConfigureAwait(false);
    var cmd = connection.CreateCommand();

    await using (cmd.ConfigureAwait(false)) {
      cmd.CommandText =
          "DELETE FROM cfn_state WHERE pluginName = @pluginName AND pluginDescription = @pluginDescription AND key = @key";
      cmd.Parameters.AddWithValue("pluginName", pluginIdentifiers.Name);
      cmd.Parameters.AddWithValue("pluginDescription", pluginIdentifiers.Description);
      cmd.Parameters.AddWithValue("key", key);
      await cmd.PrepareAsync(cancellationToken).ConfigureAwait(false);
      await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
  }

  private async Task<NpgsqlConnection> GetConnection(CancellationToken cancellationToken) {
    await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
    try {
      if (_dataSource is null) {
        _dataSource = NpgsqlDataSource.Create(_connectionString);
        var command = _dataSource.CreateCommand(TableCreateSql);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
      }

      if (_connection is null) {
        _connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        _transaction = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
      }

      return _connection;
    } finally {
      _semaphore.Release();
    }
  }

  #endregion Data access

  #region Notification listener

  public void Dispose() {
    _notificationSender.Unregister(this);
    _transaction?.Rollback();
    _transaction?.Dispose();
    _connection?.Dispose();
    _dataSource?.Dispose();
    _semaphore.Dispose();
    GC.SuppressFinalize(this);
  }

  public async Task OnRunFinished(bool dryRun, CancellationToken cancellationToken) {
    if (_transaction is null || _connection is null) {
      return;
    }

    await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
    try {
      if (dryRun) {
        await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
      } else {
        await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
      }

      await _transaction.DisposeAsync().ConfigureAwait(false);
      _transaction = null;

      await _connection.DisposeAsync().ConfigureAwait(false);
      _connection = null;
    } finally {
      _semaphore.Release();
    }
  }

  #endregion Notification listener
}
