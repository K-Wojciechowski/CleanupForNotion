using CleanupForNotion.Core.Infrastructure.Plugins;
using CleanupForNotion.Core.Infrastructure.State;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.State;

[TestClass]
public class PluginStateProviderExtensionsTests {
  private const string Key = "Key";

  private const string DateString = "2024-12-31T23:59:59.1230000+00:00";
  private static readonly DateTimeOffset _dateValue = new(2024, 12, 31, 23, 59, 59, 123, TimeSpan.Zero);

  [TestMethod]
  public async Task GetDateTime_NullValue_ReturnsNull() {
    // Arrange
    var pluginStateProvider = Substitute.For<IPluginStateProvider>();
    var pluginIdentifiers = Substitute.For<IPluginIdentifiers>();
    pluginStateProvider.GetString(pluginIdentifiers, Key, CancellationToken.None).Returns(Task.FromResult((string?)null));

    // Act
    var result = await pluginStateProvider.GetDateTime(pluginIdentifiers, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBeNull();
  }

  [TestMethod]
  public async Task GetDateTime_DateValueExists_ReturnsIt() {
    // Arrange
    var pluginStateProvider = Substitute.For<IPluginStateProvider>();
    var pluginIdentifiers = Substitute.For<IPluginIdentifiers>();
    pluginStateProvider.GetString(pluginIdentifiers, Key, CancellationToken.None).Returns(DateString);

    // Act
    var result = await pluginStateProvider.GetDateTime(pluginIdentifiers, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBe(_dateValue);
  }

  [TestMethod]
  public async Task SetDateTime_Called_ConvertsAndSavesStringAsync() {
    // Arrange
    var pluginStateProvider = Substitute.For<IPluginStateProvider>();
    var pluginIdentifiers = Substitute.For<IPluginIdentifiers>();

    // Act
    await pluginStateProvider.SetDateTime(pluginIdentifiers, Key, _dateValue, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await pluginStateProvider.Received().SetString(pluginIdentifiers, Key, DateString, CancellationToken.None).ConfigureAwait(false);
  }


  [TestMethod]
  public async Task GetInteger_NullValue_ReturnsNull() {
    // Arrange
    var pluginStateProvider = Substitute.For<IPluginStateProvider>();
    var pluginIdentifiers = Substitute.For<IPluginIdentifiers>();
    pluginStateProvider.GetString(pluginIdentifiers, Key, CancellationToken.None).Returns(Task.FromResult((string?)null));

    // Act
    var result = await pluginStateProvider.GetInteger(pluginIdentifiers, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBeNull();
  }

  [TestMethod]
  public async Task GetInteger_IntegerValueExists_ReturnsIt() {
    // Arrange
    var pluginStateProvider = Substitute.For<IPluginStateProvider>();
    var pluginIdentifiers = Substitute.For<IPluginIdentifiers>();
    pluginStateProvider.GetString(pluginIdentifiers, Key, CancellationToken.None).Returns("12345");

    // Act
    var result = await pluginStateProvider.GetInteger(pluginIdentifiers, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBe(12345);
  }

  [TestMethod]
  public async Task SetInteger_Called_ConvertsAndSavesStringAsync() {
    // Arrange
    var pluginStateProvider = Substitute.For<IPluginStateProvider>();
    var pluginIdentifiers = Substitute.For<IPluginIdentifiers>();

    // Act
    await pluginStateProvider.SetInteger(pluginIdentifiers, Key, 12345, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await pluginStateProvider.Received().SetString(pluginIdentifiers, Key, "12345", CancellationToken.None).ConfigureAwait(false);
  }
}
