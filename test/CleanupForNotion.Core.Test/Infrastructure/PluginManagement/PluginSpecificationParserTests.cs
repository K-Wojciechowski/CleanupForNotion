using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Shouldly;

namespace CleanupForNotion.Test.Infrastructure.PluginManagement;

[TestClass]
public class PluginSpecificationParserTests {
  private const string PluginName = "TestPluginName";
  private const string PluginDescription = "TestPluginDescription";
  private const string OtherKey = "OtherKey";
  private const int OtherValue = 12345;

  [TestMethod]
  public void ParseSpecification_ContainsRequiredFields_ReturnsSpecification() {
    // Arrange
    var parser = new PluginSpecificationParser();
    var rawSpecification = GetBaseRawSpecification();

    // Act
    var specification = parser.ParseSpecification(rawSpecification);

    // Assert
    specification.PluginName.ShouldBe(PluginName);
    specification.PluginDescription.ShouldBe(PluginDescription);
    specification.RawOptions.GetInteger(OtherKey).ShouldBe(OtherValue);
  }

  [TestMethod]
  [DataRow("PluginName")]
  [DataRow("PluginDescription")]
  public void ParseSpecification_MissingRequiredField_ThrowsException(string missingField) {
    // Arrange
    var parser = new PluginSpecificationParser();
    var rawSpecification = GetBaseRawSpecification();
    rawSpecification.Remove(missingField).ShouldBeTrue();

    // Act
    Action act = () => parser.ParseSpecification(rawSpecification);

    // Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe($"{missingField} is required");
  }

  private static Dictionary<string, object> GetBaseRawSpecification() => new() {
      { "PluginName", PluginName },
      { "PluginDescription", PluginDescription },
      { OtherKey, OtherValue },
  };

}
