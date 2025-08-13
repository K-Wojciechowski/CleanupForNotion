using System.Text.Json;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.State;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.State;

[TestClass]
public class JsonFilePluginStateProviderTests {
  private const string PluginName = "p";
  private const string PluginDescription = "d";
  private const string Key = "k";
  private const string Value = "v";

  private static IOptions<CfnOptions> GetOptions(string? filePath = null) =>
    new OptionsWrapper<CfnOptions>(new CfnOptions {
      AuthToken = string.Empty,
      Plugins = [],
      StateFilePath = filePath
    });

  [TestMethod]
  public void Constructor_Called_RegistersWithNotificationSender() {
    // Arrange
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions();

    // Act
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);

    // Assert
    notificationSender.Received().Register(provider);
  }

  [TestMethod]
  public void Dispose_Called_UnregistersFromNotificationSenderAndDisposesSemaphore() {
    // Arrange
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions();
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);

    // Act
    provider.Dispose();

    // Assert
    notificationSender.Received().Unregister(provider);
  }

  [TestMethod]
  public async Task SetString_ThenGetString_ReturnsValue() {
    // Arrange
    using var tempFile = new TempFile();
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions(tempFile.Path);
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);

    // Act
    await provider.SetString(PluginName, PluginDescription, Key, Value, CancellationToken.None).ConfigureAwait(false);
    var result = await provider.GetString(PluginName, PluginDescription, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBe(Value);
  }

  [TestMethod]
  public async Task Remove_RemovesValue() {
    // Arrange
    using var tempFile = new TempFile();
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions(tempFile.Path);
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);
    await provider.SetString(PluginName, PluginDescription, Key, Value, CancellationToken.None).ConfigureAwait(false);

    // Act
    await provider.Remove(PluginName, PluginDescription, Key, CancellationToken.None).ConfigureAwait(false);
    var result = await provider.GetString(PluginName, PluginDescription, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBeNull();
  }

  [TestMethod]
  public async Task GetString_LoadsFromFileOnFirstAccess() {
    // Arrange
    using var tempFile = new TempFile();
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var encodedKey = PluginName + '\x1c' + PluginDescription + '\x1d' + Key;
    var dict = new Dictionary<string, string> { [encodedKey] = Value };
    await File.WriteAllTextAsync(tempFile.Path, JsonSerializer.Serialize(dict)).ConfigureAwait(false);
    var options = GetOptions(tempFile.Path);
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);

    // Act
    var result = await provider.GetString(PluginName, PluginDescription, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBe(Value);
  }

  [TestMethod]
  public async Task GetString_FileLoadFails_LogsErrorAndUsesBlankState() {
    // Arrange
    using var tempFile = new TempFile();
    await File.WriteAllTextAsync(tempFile.Path, "not a json").ConfigureAwait(false);
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions(tempFile.Path);
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);

    // Act
    var result = await provider.GetString(PluginName, PluginDescription, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBeNull();
    logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Error);
    logger.Collector.LatestRecord.Exception.ShouldNotBeNull();
    logger.Collector.LatestRecord.Message.ShouldContain("Failed to load state file");
  }

  [TestMethod]
  public async Task GetString_FileContainsNull_LogsErrorAndUsesBlankState() {
    // Arrange
    using var tempFile = new TempFile();
    await File.WriteAllTextAsync(tempFile.Path, "null").ConfigureAwait(false);
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions(tempFile.Path);
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);

    // Act
    var result = await provider.GetString(PluginName, PluginDescription, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBeNull();
    logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Error);
    logger.Collector.LatestRecord.Exception.ShouldNotBeNull()
        .Message.ShouldBe("Null state deserialized");
    logger.Collector.LatestRecord.Message.ShouldContain("Failed to load state file");
  }

  [TestMethod]
  public async Task Dispose_Dirty_SavesToFile() {
    // Arrange
    using var tempFile = new TempFile();
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions(tempFile.Path);
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);
    await provider.SetString(PluginName, PluginDescription, Key, Value, CancellationToken.None).ConfigureAwait(false);

    // Act
    provider.Dispose();

    // Assert
    var json = await File.ReadAllTextAsync(tempFile.Path).ConfigureAwait(false);
    json.ShouldContain(Value);
  }

  [TestMethod]
  public async Task OnRunFinished_DirtyAndNotDryRun_SavesToFile() {
    // Arrange
    using var tempFile = new TempFile();
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions(tempFile.Path);
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);
    await provider.SetString(PluginName, PluginDescription, Key, Value, CancellationToken.None).ConfigureAwait(false);

    // Act
    await provider.OnRunFinished(false, CancellationToken.None).ConfigureAwait(false);

    // Assert
    var json = await File.ReadAllTextAsync(tempFile.Path).ConfigureAwait(false);
    json.ShouldContain(Value);
  }

  [TestMethod]
  public async Task OnRunFinished_NotDirty_DoesNotSave() {
    // Arrange
    using var tempFile = new TempFile();
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions(tempFile.Path);
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);

    // Act
    await provider.OnRunFinished(false, CancellationToken.None).ConfigureAwait(false);

    // Assert
    (await File.ReadAllTextAsync(tempFile.Path).ConfigureAwait(false)).ShouldBeEmpty();
  }

  [TestMethod]
  public async Task OnRunFinished_DryRun_DoesNotSave() {
    // Arrange
    using var tempFile = new TempFile();
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions(tempFile.Path);
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);

    // Act
    await provider.SetString(PluginName, PluginDescription, Key, Value, CancellationToken.None).ConfigureAwait(false);
    await provider.OnRunFinished(true, CancellationToken.None).ConfigureAwait(false);

    // Assert
    (await File.ReadAllTextAsync(tempFile.Path).ConfigureAwait(false)).ShouldBeEmpty();
  }

  [TestMethod]
  public async Task OnRunFinished_NoFilePath_DoesNotSave() {
    // Arrange
    var logger = new FakeLogger<JsonFilePluginStateProvider>();
    var notificationSender = Substitute.For<INotificationSender>();
    var options = GetOptions();
    var provider = new JsonFilePluginStateProvider(logger, notificationSender, options);

    // Act
    await provider.SetString(PluginName, PluginDescription, Key, Value, CancellationToken.None).ConfigureAwait(false);
    Func<Task> act = async () => await provider.OnRunFinished(false, CancellationToken.None).ConfigureAwait(false);

    // Assert
    act.ShouldNotThrow();
  }

  private sealed class TempFile : IDisposable {
    public string Path { get; } = System.IO.Path.GetTempFileName();
    public void Dispose() {
      if (File.Exists(Path)) File.Delete(Path);
    }
  }
}
