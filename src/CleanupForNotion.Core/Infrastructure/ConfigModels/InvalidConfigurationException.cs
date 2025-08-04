namespace CleanupForNotion.Core.Infrastructure.ConfigModels;

public class InvalidConfigurationException : Exception {
  public InvalidConfigurationException(string? message) : base(message) {
  }
}
