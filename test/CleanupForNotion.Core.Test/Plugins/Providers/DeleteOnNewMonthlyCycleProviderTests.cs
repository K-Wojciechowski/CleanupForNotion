using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Plugins.Providers;

[TestClass]
public class DeleteOnNewMonthlyCycleProviderTests {
  private const string PluginDescription = "Plugin Description";

  private readonly DeleteOnNewMonthlyCycleProvider _provider = new(
      new NullLoggerFactory(),
      Substitute.For<IPluginStateProvider>(),
      TimeProvider.System
  );

  [TestMethod]
  public void Name_Get_ReturnsDeleteOnNewMonthlyCycle()
    => _provider.Name.ShouldBe("DeleteOnNewMonthlyCycle");

  [TestMethod]
  [DataRow(null, null, null, null)]
  [DataRow("00:00:01", null, null, null)]
  [DataRow(null, 5, null, null)]
  [DataRow("00:00:01", 5, null, null)]
  [DataRow(null, 5, true, null)]
  [DataRow("00:00:01", 5, false, null)]
  [DataRow(null, 5, true, "Europe/Warsaw")]
  [DataRow("00:00:01", 5, false, "Europe/Warsaw")]
  [DataRow("00:00:01", "5", false, "Europe/Warsaw")]
  public void GetPlugin_ValidOptions_ReturnsPlugin(string? gracePeriod, object? cycleResetDay, bool? monthOverflowResets, string? timeZoneName) {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "PropertyName", "property name" },
        { "CycleResetDay", cycleResetDay ?? 1 }
    };
    if (gracePeriod != null) options["GracePeriod"] = gracePeriod;
    if (monthOverflowResets != null) options["MonthOverflowResetsOnFirstDayOfNextMonth"] = monthOverflowResets;
    if (timeZoneName != null) options["TimeZoneName"] = timeZoneName;

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    var plugin = _provider.GetPlugin(pluginSpecification);

    // Assert
    plugin.ShouldBeOfType<DeleteOnNewMonthlyCycle>();
    plugin.Name.ShouldBe("DeleteOnNewMonthlyCycle");
    plugin.Description.ShouldBe(PluginDescription);
  }

  [TestMethod]
  public void GetPlugin_MissingPropertyName_ThrowsException() {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "CycleResetDay", 1 }
    };

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    Action act = () => _provider.GetPlugin(pluginSpecification);

    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Missing required option 'PropertyName'");
  }

  [TestMethod]
  public void GetPlugin_MissingCycleResetDay_ThrowsException() {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "PropertyName", "property name" }
    };

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    Action act = () => _provider.GetPlugin(pluginSpecification);

    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Missing required option 'CycleResetDay'");
  }

  [TestMethod]
  public void GetPlugin_InvalidGracePeriod_ThrowsException() {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "PropertyName", "property name" },
        { "CycleResetDay", 1 },
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
  public void GetPlugin_InvalidCycleResetDay_ThrowsException() {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "PropertyName", "property name" },
        { "CycleResetDay", "one" },
    };

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    Action act = () => _provider.GetPlugin(pluginSpecification);

    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'CycleResetDay' is of invalid format (expected integer)");
  }

  [TestMethod]
  public void GetPlugin_InvalidMonthOverflowResetsOnFirstDayOfNextMonth_ThrowsException() {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "PropertyName", "property name" },
        { "CycleResetDay", 1 },
        { "MonthOverflowResetsOnFirstDayOfNextMonth", "invalid" }
    };

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    Action act = () => _provider.GetPlugin(pluginSpecification);

    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'MonthOverflowResetsOnFirstDayOfNextMonth' is of invalid format (expected 'true' or 'false')");
  }
}
