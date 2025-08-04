using System.Diagnostics.CodeAnalysis;

namespace CleanupForNotion.Core.Infrastructure.TimeZones;

public static class TimeZoneInfoHelper {
  [ExcludeFromCodeCoverage(Justification = "Method is tested, but computing its coverage requires merging two runs on two operating systems")]
  public static TimeZoneInfo GetTimeZone(string timeZoneName) {
    if (!OperatingSystem.IsWindows()) {
      return TimeZoneInfo.FindSystemTimeZoneById(timeZoneName);
    }

    var converted = TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneName, out var windowsId);
    return TimeZoneInfo.FindSystemTimeZoneById(converted ? windowsId! : timeZoneName);
  }
}
