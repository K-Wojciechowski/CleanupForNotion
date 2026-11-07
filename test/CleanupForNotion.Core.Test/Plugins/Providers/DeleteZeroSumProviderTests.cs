using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using CleanupForNotion.Core.Plugins.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Plugins.Providers;

[TestClass]
public class DeleteZeroSumProviderTests {
  private const string PluginDescription = "Plugin Description";
  private const string PluginId = "Plugin ID";

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
    var rawOptions = new Dictionary<string, object> {
        { "DataSourceId", "data source id" }, { "PropertyName", "property name" },
    };

    var pluginSpecification = new PluginSpecification(
        PluginIndex: 0,
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        PluginId: PluginId,
        RawOptions: new RawPluginOptions(rawOptions));

    // Act
    var plugin = _provider.GetPlugin(pluginSpecification);

    // Assert
    plugin.ShouldBeOfType<DeleteZeroSum>();
    plugin.Name.ShouldBe("DeleteZeroSum");
    plugin.Description.ShouldBe(PluginDescription);
    plugin.Id.ShouldBe(PluginId);

    var options = plugin.Configuration.ShouldBeOfType<DeleteZeroSumOptions>();
    options.DataSourceId.ShouldBe("data source id");
    options.PropertyName.ShouldBe("property name");
  }

  [TestMethod]
  public void GetPlugin_MissingPropertyName_ThrowsException() {
    // Arrange
    var rawOptions = new Dictionary<string, object> {
        { "DataSourceId", "data source id" },
        { "GracePeriod", "00:00:01" }
    };

    var pluginSpecification = new PluginSpecification(
        PluginIndex: 0,
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        PluginId: PluginId,
        RawOptions: new RawPluginOptions(rawOptions));

    // Act
    Action act = () => _provider.GetPlugin(pluginSpecification);


    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Missing required option 'PropertyName'");
  }
}
