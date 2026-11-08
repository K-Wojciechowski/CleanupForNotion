namespace CleanupForNotion.Core.Infrastructure.State.Factories;

public interface IPluginStateProviderFactory {
  string Name { get; }

  bool CanCreate();

  IPluginStateProvider Create();
}
