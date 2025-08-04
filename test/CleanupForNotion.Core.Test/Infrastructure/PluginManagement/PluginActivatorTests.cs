using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using CleanupForNotion.Core.Infrastructure.Plugins;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Infrastructure.PluginManagement;

[TestClass]
public class PluginActivatorTests {
  private readonly PluginSpecification _expectedPluginSpecification =
      new("TestPlugin", "Description", new RawPluginOptions([]));

  private readonly PluginSpecification _unexpectedPluginSpecification =
      new("MissingPlugin", "Description", new RawPluginOptions([]));

  private readonly IPlugin _expectedPlugin = Substitute.For<IPlugin>();

  private readonly PluginActivator _pluginActivator;

  public PluginActivatorTests() {
    var pluginProvider = Substitute.For<IPluginProvider>();
    pluginProvider.Name.Returns(_expectedPluginSpecification.PluginName);
    pluginProvider.GetPlugin(_expectedPluginSpecification).Returns(_expectedPlugin);

    _pluginActivator = new PluginActivator([pluginProvider]);
  }

  [TestMethod]
  public void ActivatePlugin_PluginProviderExists_ReturnsPlugin() {
    // Act
    var activatedPlugin = _pluginActivator.ActivatePlugin(_expectedPluginSpecification);

    // Assert
    activatedPlugin.ShouldBe(_expectedPlugin);
  }

  [TestMethod]
  public void ActivatePlugin_PluginProviderDoesNotExist_ReturnsPlugin() {
    // Act
    Action act = () => _pluginActivator.ActivatePlugin(_unexpectedPluginSpecification);

    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe($"The plugin provider for '{_unexpectedPluginSpecification.PluginName}' does not exist.");
  }
}
