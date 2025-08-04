using CleanupForNotion.Core.Infrastructure.TimeZones;
using Shouldly;

namespace CleanupForNotion.Test.Infrastructure.TimeZones;

[TestClass]
public class TimeZoneInfoHelperTests {
  private const string IanaId = "Europe/Warsaw";
  private const string WindowsId = "Central European Standard Time";

  [TestMethod]
  public void GetTimeZone_RunningOnUnix_ReturnsTimeZoneWithIanaId() {
    // Arrange
    if (OperatingSystem.IsWindows()) Assert.Inconclusive("This test requires Unix.");

    // Act
    var timeZone = TimeZoneInfoHelper.GetTimeZone(IanaId);

    // Assert
    timeZone.Id.ShouldBe(IanaId);
  }

  [TestMethod]
  public void GetTimeZone_RunningOnWindows_ReturnsTimeZoneWithWindowsId() {
    // Arrange
    if (!OperatingSystem.IsWindows()) Assert.Inconclusive("This test requires Windows.");

    // Act
    var timeZone = TimeZoneInfoHelper.GetTimeZone(IanaId);

    // Assert
    timeZone.Id.ShouldBe(WindowsId);
  }

}
