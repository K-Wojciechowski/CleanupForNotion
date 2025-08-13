using CleanupForNotion.Core.Infrastructure.PluginManagement;
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
            .AddSingleton(TestHelpers.GetCfnOptions())
            .AddSingleton(TimeProvider.System)
            .AddCfnServices());

    // Assert
    act.ShouldThrow<Exception>()
        .Message.ShouldContain("Unable to resolve service for type 'Microsoft.Extensions.Logging.ILoggerFactory' while attempting to activate");
  }

  [TestMethod]
  public void AddCfnServices_TimeProviderMissing_ServiceProviderIsInvalid() {
    // Arrange
    var options = new ServiceProviderOptions { ValidateOnBuild = true };

    // Act
    Action act = () => new DefaultServiceProviderFactory(options)
        .CreateServiceProvider(new ServiceCollection()
            .AddLogging()
            .AddSingleton(TestHelpers.GetCfnOptions())
            .AddCfnServices());

    // Assert
    act.ShouldThrow<Exception>()
        .Message.ShouldContain("Unable to resolve service for type 'System.TimeProvider' while attempting to activate");
  }
}
