using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.State;

[TestClass]
public class InMemoryPluginStateProviderTests {
  private const string PluginName = "p";
  private const string PluginDescription = "d";
  private const string Key = "k";
  private const string Value = "v";

  [TestMethod]
  public async Task SetString_ThenGetString_ReturnsValue() {
    // Arrange
    var provider = new InMemoryPluginStateProvider();

    // Act
    await provider.SetString(new P(PluginName, PluginDescription), Key, Value, CancellationToken.None)
        .ConfigureAwait(false);
    var result = await provider.GetString(new P(PluginName, PluginDescription), Key, CancellationToken.None)
        .ConfigureAwait(false);

    // Assert
    result.ShouldBe(Value);
  }

  [TestMethod]
  public async Task Remove_RemovesValue() {
    // Arrange
    var provider = new InMemoryPluginStateProvider();
    await provider.SetString(new P(PluginName, PluginDescription), Key, Value, CancellationToken.None)
        .ConfigureAwait(false);

    var resultBefore = await provider.GetString(new P(PluginName, PluginDescription), Key, CancellationToken.None)
        .ConfigureAwait(false);
    resultBefore.ShouldBe(Value);

    // Act
    await provider.Remove(new P(PluginName, PluginDescription), Key, CancellationToken.None).ConfigureAwait(false);
    var result = await provider.GetString(new P(PluginName, PluginDescription), Key, CancellationToken.None)
        .ConfigureAwait(false);

    // Assert
    result.ShouldBeNull();
  }

  [TestMethod]
  public async Task GetString_ReturnsNullOnFirstAccess() {
    // Arrange
    var provider = new InMemoryPluginStateProvider();

    // Act
    var result = await provider.GetString(new P(PluginName, PluginDescription), Key, CancellationToken.None)
        .ConfigureAwait(false);

    // Assert
    result.ShouldBeNull();
  }

  private record P(string Name, string Description, string? Id = null) : IPluginIdentifiers;
}
