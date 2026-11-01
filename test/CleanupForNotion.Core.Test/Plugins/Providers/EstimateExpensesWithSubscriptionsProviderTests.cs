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
  private static readonly IConfiguration _pluginConfiguration = new ConfigurationBuilder()
      .AddInMemoryCollection(new Dictionary<string, string?> {
          ["CleanupForNotion:Plugins:0:Subscriptions:0:Names:0"] = "Unused",
          ["CleanupForNotion:Plugins:0:Subscriptions:0:ExpectedTotal"] = "1000",
          ["CleanupForNotion:Plugins:3:Subscriptions:0:Names:0"] = "One Sub",
          ["CleanupForNotion:Plugins:3:Subscriptions:0:ExpectedTotal"] = "1500",
          ["CleanupForNotion:Plugins:3:Subscriptions:1:Names:0"] = "Two Sub",
          ["CleanupForNotion:Plugins:3:Subscriptions:1:Names:1"] = "Sub Two",
          ["CleanupForNotion:Plugins:3:Subscriptions:1:ExpectedTotal"] = "5000"
      })
      .Build();

  [TestMethod]
  public void GetPlugin_SubscriptionsConfiguredAtPluginIndex_ReturnsPlugin() {
    var provider = new EstimateExpensesWithSubscriptionsProvider(
        _pluginConfiguration,
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
            ["OutputPageId"] = "outputPageId",
            ["NamePropertyName"] = "name",
            ["AmountPropertyName"] = "amount",
            ["CurrencyDecimalPlaces"] = 2
        }));

    var plugin = provider.GetPlugin(pluginSpecification);

    plugin.ShouldBeOfType<EstimateExpensesWithSubscriptions>();
    plugin.Name.ShouldBe("EstimateExpensesWithSubscriptions");
    plugin.Description.ShouldBe("plugin description");
    var configuration = plugin.Configuration.ShouldBeOfType<EstimateExpensesWithSubscriptionsOptions>();
    configuration.DataSourceId.ShouldBe("dataSourceId");
    configuration.PropertyName.ShouldBe("propertyName");
    configuration.OutputPageId.ShouldBe("outputPageId");
    configuration.NamePropertyName.ShouldBe("name");
    configuration.AmountPropertyName.ShouldBe("amount");
    configuration.CurrencyDecimalPlaces.ShouldBe(2);
    configuration.Subscriptions.Length.ShouldBe(2);
    configuration.Subscriptions[0].Names.ShouldBe(["One Sub"]);
    configuration.Subscriptions[0].ExpectedTotal.ShouldBe(1500);
    configuration.Subscriptions[1].Names.ShouldBe(["Two Sub", "Sub Two"]);
    configuration.Subscriptions[1].ExpectedTotal.ShouldBe(5000);
  }
}
