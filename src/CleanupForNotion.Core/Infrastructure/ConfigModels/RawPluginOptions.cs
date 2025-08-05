namespace CleanupForNotion.Core.Infrastructure.ConfigModels;

public class RawPluginOptions(Dictionary<string, object> options) {
  public string? GetOptionalString(string key) {
    return options.GetValueOrDefault(key) switch {
        null => null,
        string stringValue => stringValue,
        _ => throw new InvalidConfigurationException($"Option '{key}' is of invalid type (expected string)")
    };
  }

  public int? GetOptionalInteger(string key) {
    return options.GetValueOrDefault(key) switch {
        null => null,
        int intValue => intValue,
        string stringValue => int.TryParse(stringValue, out int result) ? result : throw new InvalidConfigurationException($"Option '{key}' is of invalid format (expected integer)"),
        _ => throw new InvalidConfigurationException($"Option '{key}' is of invalid type (expected int)")
    };
  }

  public bool? GetOptionalBoolean(string key) {
    return options.GetValueOrDefault(key) switch {
        null => null,
        bool boolValue => boolValue,
        string stringValue => StringToBoolean(key, stringValue),
        _ => throw new InvalidConfigurationException($"Option '{key}' is of invalid type (expected bool)")
    };
  }

  public TimeSpan? GetOptionalTimeSpan(string key) {
    return options.GetValueOrDefault(key) switch {
        null => null,
        TimeSpan timeSpan => timeSpan,
        string stringValue => TimeSpan.TryParse(stringValue, out TimeSpan result)
            ? result
            : throw new InvalidConfigurationException($"Option '{key}' is of invalid format (expected 'HH:MM:SS')"),
        _ => throw new InvalidConfigurationException($"Option '{key}' is of invalid type (expected TimeSpan)")
    };
  }

  private static bool? StringToBoolean(string key, string stringValue) {
    if (stringValue.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
    if (stringValue.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

    throw new InvalidConfigurationException($"Option '{key}' is of invalid format (expected 'true' or 'false')");
  }

  public string GetString(string key) {
    return GetOptionalString(key) ?? throw new InvalidConfigurationException($"Missing required option '{key}'");
  }

  public int GetInteger(string key) {
    return GetOptionalInteger(key) ?? throw new InvalidConfigurationException($"Missing required option '{key}'");
  }
}
