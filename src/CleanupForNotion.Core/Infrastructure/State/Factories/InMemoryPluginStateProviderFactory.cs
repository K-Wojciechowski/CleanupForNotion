using CleanupForNotion.Core.Infrastructure.ConfigModels;
using Microsoft.Extensions.Options;

namespace CleanupForNotion.Core.Infrastructure.State.Factories;

internal class InMemoryPluginStateProviderFactory(IOptions<CfnOptions> options) : IPluginStateProviderFactory {
  public string Name => "InMemory";

  public bool CanCreate() => string.Equals(options.Value.StateEngine ?? Name, Name, StringComparison.OrdinalIgnoreCase);

  public IPluginStateProvider Create() => new InMemoryPluginStateProvider();
}
