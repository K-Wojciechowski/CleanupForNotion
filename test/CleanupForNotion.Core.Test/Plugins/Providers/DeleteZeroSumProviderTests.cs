using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Plugins.Providers;

[TestClass]
public class DeleteZeroSumProviderTests {
  private const string PluginDescription = "Plugin Description";

  private readonly DeleteZeroSumProvider _provider = new(
      new NullLoggerFactory(),
      Substitute.For<IPluginStateProvider>(),
      TimeProvider.System
  );

  [TestMethod]
  public void Name_Get_ReturnsDeleteZeroSum()
    => _provider.Name.ShouldBe("DeleteZeroSum");

  [TestMethod]
  public void GetPlugin_ValidOptions_ReturnsPlugin() {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" }, { "PropertyName", "property name" },
    };

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    var plugin = _provider.GetPlugin(pluginSpecification);

    // Assert
    plugin.ShouldBeOfType<DeleteZeroSum>();
    plugin.Name.ShouldBe("DeleteZeroSum");
    plugin.Description.ShouldBe(PluginDescription);
  }

  [TestMethod]
  public void GetPlugin_MissingPropertyName_ThrowsException() {
    // Arrange
    const string description = "Plugin Description";
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "GracePeriod", "00:00:01" }
    };

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: description,
        RawOptions: new RawPluginOptions(options));

    // Act
    Action act = () => _provider.GetPlugin(pluginSpecification);


    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Missing required option 'PropertyName'");
  }
}
