using CleanupForNotion.Core.Infrastructure.State;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Infrastructure.State;

[TestClass]
public class PluginStateProviderExtensionsTests {
  private const string PluginName = "PluginName";
  private const string PluginDescription = "PluginDescription";
  private const string Key = "Key";

  private const string DateString = "2024-12-31T23:59:59.1230000+00:00";
  private static readonly DateTimeOffset _dateValue = new(2024, 12, 31, 23, 59, 59, 123, TimeSpan.Zero);

  [TestMethod]
  public async Task GetDateTime_NullValue_ReturnsNullAsync() {
    // Arrange
    var pluginStateProvider = Substitute.For<IPluginStateProvider>();
    pluginStateProvider.GetString(PluginName, PluginDescription, Key, CancellationToken.None).Returns(Task.FromResult((string?)null));

    // Act
    var result = await pluginStateProvider.GetDateTime(PluginName, PluginDescription, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBeNull();
  }

  [TestMethod]
  public async Task GetDateTime_DateValueExists_ReturnsIt() {
    // Arrange
    var pluginStateProvider = Substitute.For<IPluginStateProvider>();
    pluginStateProvider.GetString(PluginName, PluginDescription, Key, CancellationToken.None).Returns(DateString);

    // Act
    var result = await pluginStateProvider.GetDateTime(PluginName, PluginDescription, Key, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBe(_dateValue);
  }

  [TestMethod]
  public async Task SetDateTime_Called_ConvertsAndSavesStringAsync() {
    // Arrange
    var pluginStateProvider = Substitute.For<IPluginStateProvider>();

    // Act
    await pluginStateProvider.SetDateTime(PluginName, PluginDescription, Key, _dateValue, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await pluginStateProvider.Received().SetString(PluginName, PluginDescription, Key, DateString, CancellationToken.None).ConfigureAwait(false);
  }

}
