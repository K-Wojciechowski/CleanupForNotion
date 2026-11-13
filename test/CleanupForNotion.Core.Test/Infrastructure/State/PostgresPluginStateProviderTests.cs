using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Options;
using Npgsql;
using NSubstitute;
using Shouldly;
using Testcontainers.PostgreSql;

namespace CleanupForNotion.Core.Test.Infrastructure.State;

[TestClass]
[DoNotParallelize]
public class PostgresPluginStateProviderTests {
  private static PostgreSqlContainer? _container;
  private static IOptions<CfnOptions> _options = null!;

  [ClassInitialize]
  public static void Initialize(TestContext context) {
    if (OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("CI") == "true") {
      Assert.Inconclusive("Postgres tests cannot run in CI on Windows");
    }

    try {
      _container = new PostgreSqlBuilder("postgres:18-alpine").Build();
      _container.StartAsync(context.CancellationToken).Wait(context.CancellationToken);
    } catch (DockerUnavailableException exc) {
      Assert.Inconclusive($"Docker is unavailable, cannot run Postgres tests. {exc}");

    }

    var cfnOptions = new CfnOptions {
        StateAccessDetails = _container.GetConnectionString(), AuthToken = "", Plugins = []
    };
    _options = new OptionsWrapper<CfnOptions>(cfnOptions);
  }

  [ClassCleanup]
  public static void Cleanup(TestContext context) {
    if (_container == null) return;
    Task.Run(async () => await _container.DisposeAsync().ConfigureAwait(false), context.CancellationToken).Wait(context.CancellationToken);
  }

  [TestMethod]
  public async Task Constructor_NullConnectionString_Throws() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();

    var cfnOptions = new CfnOptions {
        StateAccessDetails = null, AuthToken = "", Plugins = []
    };
    var options = new OptionsWrapper<CfnOptions>(cfnOptions);

    // Act
    Action act = () => { new PostgresPluginStateProvider(notificationSender, options); };

    // Assert
    act.ShouldThrow<InvalidConfigurationException>();
  }

  [TestMethod]
  public async Task GetString_MissingValue_ReturnsNull() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    using var provider = new PostgresPluginStateProvider(notificationSender, _options);
    var id = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var key = Guid.NewGuid().ToString();

    // Act
    var result = await provider.GetString(id, key, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    result.ShouldBeNull();
  }

  [TestMethod]
  public async Task GetString_AfterSetString_ReturnsValue() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    using var provider = new PostgresPluginStateProvider(notificationSender, _options);
    var id = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var key = Guid.NewGuid().ToString();
    var resultBefore = await provider.GetString(id, key, TestContext.CancellationToken)
        .ConfigureAwait(false);
    resultBefore.ShouldBeNull();
    var value = Guid.NewGuid().ToString();

    // Act
    await provider.SetString(id, key, value, TestContext.CancellationToken)
        .ConfigureAwait(false);
    var result = await provider.GetString(id, key, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    result.ShouldBe(value);
  }


  [TestMethod]
  public async Task SetString_OverwriteInSameTransaction_ReturnsNewValue() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    using var provider = new PostgresPluginStateProvider(notificationSender, _options);
    var id1 = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var id2 = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var key1 = Guid.NewGuid().ToString();
    var key2 = Guid.NewGuid().ToString();
    (await provider.GetString(id1, key1, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    (await provider.GetString(id2, key2, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    var value1 = Guid.NewGuid().ToString();
    var value2 = Guid.NewGuid().ToString();
    var value3 = Guid.NewGuid().ToString();

    // Act
    await provider.SetString(id1, key1, value1, TestContext.CancellationToken).ConfigureAwait(false);
    await provider.SetString(id2, key2, value2, TestContext.CancellationToken).ConfigureAwait(false);
    (await provider.GetString(id1, key1, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value1);
    (await provider.GetString(id2, key2, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value2);
    await provider.SetString(id1, key1, value3, TestContext.CancellationToken).ConfigureAwait(false);

    // Assert
    (await provider.GetString(id1, key1, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value3);
    (await provider.GetString(id2, key2, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value2);
  }

  [TestMethod]
  public async Task SetString_OverwriteInNewTransaction_ReturnsNewValue() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    using var provider = new PostgresPluginStateProvider(notificationSender, _options);
    var id1 = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var id2 = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var key1 = Guid.NewGuid().ToString();
    var key2 = Guid.NewGuid().ToString();
    (await provider.GetString(id1, key1, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    (await provider.GetString(id2, key2, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    var value1 = Guid.NewGuid().ToString();
    var value2 = Guid.NewGuid().ToString();
    var value3 = Guid.NewGuid().ToString();

    // Act
    await provider.SetString(id1, key1, value1, TestContext.CancellationToken).ConfigureAwait(false);
    await provider.SetString(id2, key2, value2, TestContext.CancellationToken).ConfigureAwait(false);
    (await provider.GetString(id1, key1, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value1);
    (await provider.GetString(id2, key2, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value2);
    await provider.OnRunFinished(dryRun: false, TestContext.CancellationToken).ConfigureAwait(false);

    await provider.SetString(id1, key1, value3, TestContext.CancellationToken).ConfigureAwait(false);

    // Assert
    (await provider.GetString(id1, key1, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value3);
    (await provider.GetString(id2, key2, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value2);
  }

  [TestMethod]
  public async Task Remove_RemovesExistingValue() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    using var provider = new PostgresPluginStateProvider(notificationSender, _options);
    var id = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var key1 = Guid.NewGuid().ToString();
    var key2 = Guid.NewGuid().ToString();
    (await provider.GetString(id, key1, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    (await provider.GetString(id, key2, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    var value1 = Guid.NewGuid().ToString();

    await provider.SetString(id, key1, value1, TestContext.CancellationToken).ConfigureAwait(false);
    (await provider.GetString(id, key1, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value1);
    (await provider.GetString(id, key2, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();

    // Act
    await provider.Remove(id, key1, TestContext.CancellationToken).ConfigureAwait(false);
    await provider.Remove(id, key2, TestContext.CancellationToken).ConfigureAwait(false);

    // Assert
    (await provider.GetString(id, key1, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    (await provider.GetString(id, key2, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
  }

  [TestMethod]
  public async Task OnRunFinished_NotDryRun_CommitsTransaction() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    using var provider = new PostgresPluginStateProvider(notificationSender, _options);
    var id = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var key1 = Guid.NewGuid().ToString();
    var key2 = Guid.NewGuid().ToString();
    (await provider.GetString(id, key1, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    (await provider.GetString(id, key2, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    var value1 = Guid.NewGuid().ToString();
    var value2 = Guid.NewGuid().ToString();
    var verificationDataSource = NpgsqlDataSource.Create(_options.Value.StateAccessDetails!);
    await using var _ = verificationDataSource.ConfigureAwait(false);

    await provider.SetString(id, key1, value1, TestContext.CancellationToken).ConfigureAwait(false);
    await provider.OnRunFinished(dryRun: false, TestContext.CancellationToken).ConfigureAwait(false);
    await provider.SetString(id, key2, value2, TestContext.CancellationToken).ConfigureAwait(false);

    (await GetFromDb(verificationDataSource, id, key1).ConfigureAwait(false)).ShouldBe(value1);
    (await GetFromDb(verificationDataSource, id, key2).ConfigureAwait(false)).ShouldBe(null);

    // Act
    await provider.OnRunFinished(dryRun: false, TestContext.CancellationToken).ConfigureAwait(false);

    // Assert
    (await GetFromDb(verificationDataSource, id, key1).ConfigureAwait(false)).ShouldBe(value1);
    (await GetFromDb(verificationDataSource, id, key2).ConfigureAwait(false)).ShouldBe(value2);
  }

  [TestMethod]
  public async Task OnRunFinished_DryRun_RollsBackTransaction() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    using var provider = new PostgresPluginStateProvider(notificationSender, _options);
    var id = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var key = Guid.NewGuid().ToString();
    (await provider.GetString(id, key, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    var value = Guid.NewGuid().ToString();
    var verificationDataSource = NpgsqlDataSource.Create(_options.Value.StateAccessDetails!);
    await using var _ = verificationDataSource.ConfigureAwait(false);

    await provider.SetString(id, key, value, TestContext.CancellationToken).ConfigureAwait(false);
    (await provider.GetString(id, key, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value);
    (await GetFromDb(verificationDataSource, id, key).ConfigureAwait(false)).ShouldBe(null);

    // Act
    await provider.OnRunFinished(dryRun: true, TestContext.CancellationToken).ConfigureAwait(false);

    // Assert
    (await provider.GetString(id, key, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    (await GetFromDb(verificationDataSource, id, key).ConfigureAwait(false)).ShouldBeNull();
  }

  [TestMethod]
  public async Task OnRunFinished_NoConnection_DoesNothing() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    using var provider = new PostgresPluginStateProvider(notificationSender, _options);

    // Act
    await provider.OnRunFinished(dryRun: false, TestContext.CancellationToken).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Dispose_AfterCommit_DataStaysInDatabase() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new PostgresPluginStateProvider(notificationSender, _options);
    var id = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var key = Guid.NewGuid().ToString();
    (await provider.GetString(id, key, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    var value = Guid.NewGuid().ToString();
    var verificationDataSource = NpgsqlDataSource.Create(_options.Value.StateAccessDetails!);
    await using var _ = verificationDataSource.ConfigureAwait(false);

    await provider.SetString(id, key, value, TestContext.CancellationToken).ConfigureAwait(false);
    await provider.OnRunFinished(dryRun: false, TestContext.CancellationToken).ConfigureAwait(false);
    (await provider.GetString(id, key, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value);
    (await GetFromDb(verificationDataSource, id, key).ConfigureAwait(false)).ShouldBe(value);

    // Act
    provider.Dispose();

    // Assert
    (await GetFromDb(verificationDataSource, id, key).ConfigureAwait(false)).ShouldBe(value);
  }

  [TestMethod]
  public async Task Dispose_BeforeCommit_TransactionRolledBack() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    var provider = new PostgresPluginStateProvider(notificationSender, _options);
    var id = new P(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    var key = Guid.NewGuid().ToString();
    (await provider.GetString(id, key, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBeNull();
    var value = Guid.NewGuid().ToString();
    var verificationDataSource = NpgsqlDataSource.Create(_options.Value.StateAccessDetails!);
    await using var _ = verificationDataSource.ConfigureAwait(false);

    await provider.SetString(id, key, value, TestContext.CancellationToken).ConfigureAwait(false);
    (await provider.GetString(id, key, TestContext.CancellationToken).ConfigureAwait(false)).ShouldBe(value);
    (await GetFromDb(verificationDataSource, id, key).ConfigureAwait(false)).ShouldBeNull();

    // Act
    provider.Dispose();

    // Assert
    (await GetFromDb(verificationDataSource, id, key).ConfigureAwait(false)).ShouldBeNull();
  }

  [TestMethod]
  public async Task Dispose_NoConnection_DoesNothing() {
    // Arrange
    var notificationSender = Substitute.For<INotificationSender>();
    using var provider = new PostgresPluginStateProvider(notificationSender, _options);

    // Act
    provider.Dispose();
  }

  private async Task<string?> GetFromDb(NpgsqlDataSource verificationDataSource, IPluginIdentifiers pluginIdentifiers,
      string key) {
    var cmd = verificationDataSource.CreateCommand();

    await using (cmd.ConfigureAwait(false)) {
      cmd.CommandText =
          "SELECT value FROM cfn_state WHERE pluginName = @pluginName AND pluginDescription = @pluginDescription AND key = @key";
      cmd.Parameters.AddWithValue("pluginName", pluginIdentifiers.Name);
      cmd.Parameters.AddWithValue("pluginDescription", pluginIdentifiers.Description);
      cmd.Parameters.AddWithValue("key", key);
      return (await cmd.ExecuteScalarAsync(TestContext.CancellationToken).ConfigureAwait(false)) as string;
    }
  }

  public TestContext TestContext { get; set; }

  private record P(string Name, string Description, string? Id = null) : IPluginIdentifiers;
}
