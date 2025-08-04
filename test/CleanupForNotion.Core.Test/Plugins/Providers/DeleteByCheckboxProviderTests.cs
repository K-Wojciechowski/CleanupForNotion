using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Plugins.Providers;

[TestClass]
public class DeleteByCheckboxProviderTests {
  private const string PluginDescription = "Plugin Description";

  private readonly DeleteByCheckboxProvider _provider = new(
      new NullLoggerFactory(),
      Substitute.For<IPluginStateProvider>(),
      TimeProvider.System
  );

  [TestMethod]
  public void Name_Get_ReturnsDeleteByCheckbox()
    => _provider.Name.ShouldBe("DeleteByCheckbox");

  [TestMethod]
  [DataRow(null, null)]
  [DataRow("00:00:01", null)]
  [DataRow(null, "true")]
  [DataRow("00:00:01", "true")]
  [DataRow(null, "false")]
  [DataRow("00:00:01", "false")]
  public void GetPlugin_ValidOptions_ReturnsPlugin(string? gracePeriod, string? deleteIfChecked) {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" }, { "PropertyName", "property name" },
    };
    if (gracePeriod != null) options["GracePeriod"] = gracePeriod;
    if (deleteIfChecked != null) options["DeleteIfChecked"] = deleteIfChecked;

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    var plugin = _provider.GetPlugin(pluginSpecification);

    // Assert
    plugin.ShouldBeOfType<DeleteByCheckbox>();
    plugin.Name.ShouldBe("DeleteByCheckbox");
    plugin.Description.ShouldBe(PluginDescription);
  }

  [TestMethod]
  public void GetPlugin_InvalidGracePeriod_ThrowsException() {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "PropertyName", "property name" },
        { "DeleteIfChecked", "true" },
        { "GracePeriod", "invalid" }
    };

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    Action act = () => _provider.GetPlugin(pluginSpecification);


    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'GracePeriod' is of invalid format (expected 'HH:MM:SS')");
  }

  [TestMethod]
  public void GetPlugin_InvalidDeleteIfChecked_ThrowsException() {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "PropertyName", "property name" },
        { "GracePeriod", "00:00:01" },
        { "DeleteIfChecked", "invalid" }
    };

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    Action act = () => _provider.GetPlugin(pluginSpecification);


    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'DeleteIfChecked' is of invalid format (expected 'true' or 'false')");
  }
}
