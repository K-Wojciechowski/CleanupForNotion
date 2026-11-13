using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using CleanupForNotion.Core.Infrastructure.State;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CleanupForNotion.Core.Test;

[TestClass]
public class CfnServiceCollectionExtensionsTests {
  [TestMethod]
  public void AddCfnServices_PrerequisitesPresent_ServiceProviderIsValid() {
    // Arrange
    var options = new ServiceProviderOptions { ValidateOnBuild = true };

    // Act & Assert
    var provider = new DefaultServiceProviderFactory(options)
        .CreateServiceProvider(new ServiceCollection()
            .AddLogging()
            .Configure<CfnOptions>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>()).Build())
            .AddSingleton(TimeProvider.System)
            .AddCfnServices());

    var globalOptionsProvider = provider.GetRequiredService<IGlobalOptionsProvider>();
    globalOptionsProvider.ShouldBeOfType<GlobalOptionsProvider>();
    globalOptionsProvider.GlobalOptions.ShouldNotBeNull();
  }

  [TestMethod]
  public void AddCfnServices_LoggingMissing_ServiceProviderIsInvalid() {
    // Arrange
    var options = new ServiceProviderOptions { ValidateOnBuild = true };

    // Act
    Action act = () => new DefaultServiceProviderFactory(options)
        .CreateServiceProvider(new ServiceCollection()
            .Configure<CfnOptions>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>()).Build())
            .AddSingleton(TestHelpers.GetCfnOptions())
            .AddSingleton(TimeProvider.System)
            .AddCfnServices());

    // Assert
    act.ShouldThrow<Exception>()
        .Message.ShouldContain(
            "Unable to resolve service for type 'Microsoft.Extensions.Logging.ILoggerFactory' while attempting to activate");
  }

  [TestMethod]
  public void AddCfnServices_TimeProviderMissing_ServiceProviderIsInvalid() {
    // Arrange
    var options = new ServiceProviderOptions { ValidateOnBuild = true };

    // Act
    Action act = () => new DefaultServiceProviderFactory(options)
        .CreateServiceProvider(new ServiceCollection()
            .AddLogging()
            .Configure<CfnOptions>(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>()).Build())
            .AddSingleton(TestHelpers.GetCfnOptions())
            .AddCfnServices());

    // Assert
    act.ShouldThrow<Exception>()
        .Message.ShouldContain("Unable to resolve service for type 'System.TimeProvider' while attempting to activate");
  }

  [TestMethod]
  [DataRow(null, typeof(InMemoryPluginStateProvider))]
  [DataRow("InMemory", typeof(InMemoryPluginStateProvider))]
  [DataRow("File", typeof(JsonFilePluginStateProvider))]
  [DataRow("PostgreSQL", typeof(PostgresPluginStateProvider))]
  public void AddCfnServices_StateProviderConfigured_StateProviderCanBeActivated(string? stateEngine,
      Type providerType) {
    // Arrange
    var options = new ServiceProviderOptions { ValidateOnBuild = true };

    // Act & Assert
    var provider = new DefaultServiceProviderFactory(options)
        .CreateServiceProvider(new ServiceCollection()
            .AddLogging()
            .Configure<CfnOptions>(new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { { "StateEngine", stateEngine }, { "StateAccessDetails", "" } })
                .Build())
            .AddSingleton(TimeProvider.System)
            .AddCfnServices());

    var stateProvider = provider.GetRequiredService<IPluginStateProvider>();
    stateProvider.ShouldBeOfType(providerType);
  }

  [TestMethod]
  public void AddCfnServices_UnrecognisedStateProvider_StateProviderActivationThrows() {
    // Arrange
    var options = new ServiceProviderOptions { ValidateOnBuild = true };

    // Act & Assert
    var provider = new DefaultServiceProviderFactory(options)
        .CreateServiceProvider(new ServiceCollection()
            .AddLogging()
            .Configure<CfnOptions>(new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?> { { "StateEngine", Guid.NewGuid().ToString() } })
                .Build())
            .AddSingleton(TimeProvider.System)
            .AddCfnServices());

    Action act = () => {
      provider.GetRequiredService<IPluginStateProvider>();
    };

    act.ShouldThrow<InvalidConfigurationException>();
  }
}
