namespace CleanupForNotion.Core.Infrastructure.Plugins;

public interface IPluginIdentifiers {
  public string? Id { get; }

  public string Name { get; }

  public string Description { get; }
}
