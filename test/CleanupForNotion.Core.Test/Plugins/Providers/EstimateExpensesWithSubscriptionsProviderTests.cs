using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using CleanupForNotion.Core.Plugins.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Plugins.Providers;

[TestClass]
public class EstimateExpensesWithSubscriptionsProviderTests {
  private const string PluginDescription = "Plugin Description";
  private const string PluginId = "Plugin ID";

  private readonly EstimateExpensesWithSubscriptionsProvider _provider = new(
      new NullLoggerFactory(),
      Substitute.For<IPluginStateProvider>(),
      TimeProvider.System
  );

  [TestMethod]
  public void Name_Get_ReturnsEstimateExpensesWithSubscriptions()
    => _provider.Name.ShouldBe("EstimateExpensesWithSubscriptions");

  [TestMethod]
  [DataRow(null)]
  [DataRow(0)]
  [DataRow(2)]
  [DataRow(3)]
  public void GetPlugin_ValidOptions_ReturnsPlugin(int? currencyDecimalPlaces) {
    var rawOptions = new Dictionary<string, object> {
        ["DataSourceId"] = "dataSourceId",
        ["PropertyName"] = "propertyName",
        ["ExpensesDataSourceId"] = "expensesDataSourceId",
        ["OutputPageId"] = "outputPageId",
        ["VendorPropertyName"] = "vendor",
        ["AmountPropertyName"] = "amount",
        ["AlternateVendorPropertyName"] = "altvendor",
        ["ActualTotalPropertyName"] = "actualTotal"
    };

    if (currencyDecimalPlaces is not null) {
      rawOptions["CurrencyDecimalPlaces"] = currencyDecimalPlaces;
    }

    var pluginSpecification = new PluginSpecification(
        PluginIndex: 0,
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        PluginId: PluginId,
        RawOptions: new RawPluginOptions(rawOptions));

    var plugin = _provider.GetPlugin(pluginSpecification);

    plugin.ShouldBeOfType<EstimateExpensesWithSubscriptions>();
    plugin.Name.ShouldBe("EstimateExpensesWithSubscriptions");
    plugin.Description.ShouldBe(PluginDescription);

    var options = plugin.Configuration.ShouldBeOfType<EstimateExpensesWithSubscriptionsOptions>();
    options.DataSourceId.ShouldBe("dataSourceId");
    options.PropertyName.ShouldBe("propertyName");
    options.ExpensesDataSourceId.ShouldBe("expensesDataSourceId");
    options.VendorPropertyName.ShouldBe("vendor");
    options.AmountPropertyName.ShouldBe("amount");
    options.AlternateVendorPropertyName.ShouldBe("altvendor");
    options.ActualTotalPropertyName.ShouldBe("actualTotal");
    options.CurrencyDecimalPlaces.ShouldBe(currencyDecimalPlaces ?? 2);
  }

  [TestMethod]
  public void GetPlugin_MissingVendorPropertyName_ThrowsException() {
    var rawOptions = new Dictionary<string, object> {
        ["DataSourceId"] = "dataSourceId",
        ["PropertyName"] = "propertyName",
        ["ExpensesDataSourceId"] = "expensesDataSourceId",
        ["OutputPageId"] = "outputPageId",
        ["AmountPropertyName"] = "amount"
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
        .Message.ShouldBe("Missing required option 'VendorPropertyName'");
  }

  [TestMethod]
  public void GetPlugin_InvalidCurrencyDecimalPlacesType_ThrowsException() {
    var rawOptions = new Dictionary<string, object> {
        ["DataSourceId"] = "dataSourceId",
        ["PropertyName"] = "propertyName",
        ["ExpensesDataSourceId"] = "expensesDataSourceId",
        ["OutputPageId"] = "outputPageId",
        ["VendorPropertyName"] = "vendor",
        ["AmountPropertyName"] = "amount",
        ["AlternateVendorPropertyName"] = "altvendor",
        ["CurrencyDecimalPlaces"] = "zero"
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
        .Message.ShouldBe("Option 'CurrencyDecimalPlaces' is of invalid format (expected integer)");
  }
}
