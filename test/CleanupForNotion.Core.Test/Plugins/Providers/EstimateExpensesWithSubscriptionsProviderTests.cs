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
  [TestMethod]
  public void GetPlugin_SubscriptionsConfiguredAtPluginIndex_ReturnsPlugin() {
    var provider = new EstimateExpensesWithSubscriptionsProvider(
        new NullLoggerFactory(),
        Substitute.For<IPluginStateProvider>(),
        TimeProvider.System);

    var pluginSpecification = new PluginSpecification(
        PluginIndex: 3,
        PluginName: string.Empty,
        PluginDescription: "plugin description",
        RawOptions: new RawPluginOptions(new Dictionary<string, object> {
            ["DataSourceId"] = "dataSourceId",
            ["PropertyName"] = "propertyName",
            ["ExpensesDataSourceId"] = "expensesDataSourceId",
            ["OutputPageId"] = "outputPageId",
            ["VendorPropertyName"] = "vendor",
            ["AmountPropertyName"] = "amount",
            ["AlternateVendorPropertyName"] = "altvendor",
            ["CurrencyDecimalPlaces"] = 2
        }));

    var plugin = provider.GetPlugin(pluginSpecification);

    plugin.ShouldBeOfType<EstimateExpensesWithSubscriptions>();
    plugin.Name.ShouldBe("EstimateExpensesWithSubscriptions");
    plugin.Description.ShouldBe("plugin description");
    var configuration = plugin.Configuration.ShouldBeOfType<EstimateExpensesWithSubscriptionsOptions>();
    configuration.DataSourceId.ShouldBe("dataSourceId");
    configuration.PropertyName.ShouldBe("propertyName");
    configuration.ExpensesDataSourceId.ShouldBe("expensesDataSourceId");
    configuration.VendorPropertyName.ShouldBe("vendor");
    configuration.AmountPropertyName.ShouldBe("amount");
    configuration.AlternateVendorPropertyName.ShouldBe("altvendor");
    configuration.CurrencyDecimalPlaces.ShouldBe(2);
  }
}
