using CleanupForNotion.Core.Infrastructure.ConfigModels;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.ConfigModels;

[TestClass]
public sealed class RawPluginOptionsTests {
  private static readonly RawPluginOptions _rawPluginOptions = new(new Dictionary<string, object> {
      { "int", 123 },
      { "string", "abc" },
      { "bool", true },
      { "intString", "123" },
      { "trueString", "true" },
      { "falseString", "FALSE" },
      { "timespan", TimeSpan.FromMinutes(42) },
      { "ts", TimeSpan.FromMinutes(5) },
      { "tsString", "00:05:00" },
  });

  [TestMethod]
  public void GetOptionalString_PresentString_ReturnsIt() {
    // Act
    var result = _rawPluginOptions.GetOptionalString("string");

    // Assert
    result.ShouldBe("abc");
  }

  [TestMethod]
  public void GetOptionalString_MissingValue_ReturnsNull() {
    // Act
    var result = _rawPluginOptions.GetOptionalString("missing");

    // Assert
    result.ShouldBeNull();
  }

  [TestMethod]
  public void GetOptionalString_PresentInt_ThrowsException() {
    // Arrange
    var act = () => {
      _rawPluginOptions.GetOptionalString("int");
    };

    // Act & Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'int' is of invalid type (expected string)");
  }

  [TestMethod]
  public void GetOptionalInteger_PresentInteger_ReturnsIt() {
    // Act
    var result = _rawPluginOptions.GetOptionalInteger("int");

    // Assert
    result.ShouldBe(123);
  }

  [TestMethod]
  public void GetOptionalInteger_MissingValue_ReturnsNull() {
    // Act
    var result = _rawPluginOptions.GetOptionalInteger("missing");

    // Assert
    result.ShouldBeNull();
  }

  [TestMethod]
  public void GetOptionalInteger_PresentString_ParsesAndReturnsIt() {
    // Act
    var result = _rawPluginOptions.GetOptionalInteger("intString");

    // Assert
    result.ShouldBe(123);
  }

  [TestMethod]
  public void GetOptionalInteger_PresentBoolean_ThrowsException() {
    // Arrange
    var act = () => {
      _rawPluginOptions.GetOptionalInteger("bool");
    };

    // Act & Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'bool' is of invalid type (expected int)");
  }

  [TestMethod]
  public void GetOptionalInteger_InvalidFormat_ThrowsException() {
    // Arrange
    Action act = () => _rawPluginOptions.GetOptionalInteger("string");

    // Act & Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'string' is of invalid format (expected integer)");
  }

  [TestMethod]
  public void GetOptionalBoolean_PresentBoolean_ReturnsIt() {
    // Act
    var result = _rawPluginOptions.GetOptionalBoolean("bool");

    // Assert
    result.ShouldBe(true);
  }

  [TestMethod]
  public void GetOptionalBoolean_MissingValue_ReturnsNull() {
    // Act
    var result = _rawPluginOptions.GetOptionalBoolean("missing");

    // Assert
    result.ShouldBeNull();
  }

  [TestMethod]
  public void GetOptionalBoolean_PresentTrueString_ParsesAndReturnsTrue() {
    // Act
    var result = _rawPluginOptions.GetOptionalBoolean("trueString");

    // Assert
    result.ShouldBe(true);
  }

  [TestMethod]
  public void GetOptionalBoolean_PresentFalseString_ParsesAndReturnsFalse() {
    // Act
    var result = _rawPluginOptions.GetOptionalBoolean("falseString");

    // Assert
    result.ShouldBe(false);
  }

  [TestMethod]
  public void GetOptionalBoolean_PresentInteger_ThrowsException() {
    // Arrange
    var act = () => {
      _rawPluginOptions.GetOptionalBoolean("int");
    };

    // Act & Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'int' is of invalid type (expected bool)");
  }

  [TestMethod]
  public void GetOptionalBoolean_InvalidFormat_ThrowsException() {
    // Arrange
    Action act = () => _rawPluginOptions.GetOptionalBoolean("string");

    // Act & Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'string' is of invalid format (expected 'true' or 'false')");
  }

  [TestMethod]
  public void GetString_PresentString_ReturnsIt() {
    // Act
    var result = _rawPluginOptions.GetString("string");

    // Assert
    result.ShouldBe("abc");
  }

  [TestMethod]
  public void GetString_MissingValue_ThrowsException() {
    // Arrange
    var act = () => {
      _rawPluginOptions.GetString("missing");
    };

    // Act & Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Missing required option 'missing'");
  }

  [TestMethod]
  public void GetInteger_PresentInteger_ReturnsIt() {
    // Act
    var result = _rawPluginOptions.GetInteger("int");

    // Assert
    result.ShouldBe(123);
  }

  [TestMethod]
  public void GetInteger_MissingValue_ThrowsException() {
    // Arrange
    var act = () => {
      _rawPluginOptions.GetInteger("missing");
    };

    // Act & Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Missing required option 'missing'");
  }

  [TestMethod]
  public void GetOptionalTimeSpan_PresentTimeSpan_ReturnsIt() {
    // Act
    var result = _rawPluginOptions.GetOptionalTimeSpan("ts");

    // Assert
    result.ShouldBe(TimeSpan.FromMinutes(5));
  }

  [TestMethod]
  public void GetOptionalTimeSpan_PresentString_ParsesAndReturnsIt() {
    // Act
    var result = _rawPluginOptions.GetOptionalTimeSpan("tsString");

    // Assert
    result.ShouldBe(TimeSpan.FromMinutes(5));
  }

  [TestMethod]
  public void GetOptionalTimeSpan_MissingValue_ReturnsNull() {
    // Act
    var result = _rawPluginOptions.GetOptionalTimeSpan("missing");

    // Assert
    result.ShouldBeNull();
  }

  [TestMethod]
  public void GetOptionalTimeSpan_InvalidType_ThrowsException() {
    // Arrange
    Action act = () => _rawPluginOptions.GetOptionalTimeSpan("int");

    // Act & Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'int' is of invalid type (expected TimeSpan)");
  }

  [TestMethod]
  public void GetOptionalTimeSpan_InvalidFormat_ThrowsException() {
    // Arrange
    Action act = () => _rawPluginOptions.GetOptionalTimeSpan("string");

    // Act & Assert
    act.ShouldThrow<InvalidConfigurationException>()
        .Message.ShouldBe("Option 'string' is of invalid format (expected 'HH:MM:SS')");
  }
}
