using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Providers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Plugins.Providers;

[TestClass]
public class EnsureStaticRelatedPageProviderTests {
  private const string PluginDescription = "Plugin Description";

  private readonly EnsureStaticRelatedPageProvider _provider = new(
      new NullLoggerFactory(),
      Substitute.For<IPluginStateProvider>(),
      TimeProvider.System
  );

  [TestMethod]
  public void Name_Get_ReturnsEnsureStaticRelatedPage()
    => _provider.Name.ShouldBe("EnsureStaticRelatedPage");

  [TestMethod]
  [DataRow(null)]
  [DataRow("00:00:01")]
  public void GetPlugin_ValidOptions_ReturnsPlugin(string? gracePeriod) {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "PropertyName", "property name" },
        { "RelatedPageId", "related page id" },
    };
    if (gracePeriod != null) options["GracePeriod"] = gracePeriod;

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    var plugin = _provider.GetPlugin(pluginSpecification);

    // Assert
    plugin.ShouldBeOfType<EnsureStaticRelatedPage>();
    plugin.Name.ShouldBe("EnsureStaticRelatedPage");
    plugin.Description.ShouldBe(PluginDescription);
  }

  [TestMethod]
  public void GetPlugin_MissingRelatedPageId_ThrowsException() {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "PropertyName", "property name" },
    };

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    Action act = () => _provider.GetPlugin(pluginSpecification);


    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Missing required option 'RelatedPageId'");
  }

  [TestMethod]
  public void GetPlugin_InvalidRelatedPageIdType_ThrowsException() {
    // Arrange
    var options = new Dictionary<string, object> {
        { "DatabaseId", "database id" },
        { "PropertyName", "property name" },
        { "GracePeriod", "00:00:01" },
        { "RelatedPageId", Guid.NewGuid() }
    };

    var pluginSpecification = new PluginSpecification(
        PluginName: string.Empty,
        PluginDescription: PluginDescription,
        RawOptions: new RawPluginOptions(options));

    // Act
    Action act = () => _provider.GetPlugin(pluginSpecification);

    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'RelatedPageId' is of invalid type (expected string)");
  }
}
