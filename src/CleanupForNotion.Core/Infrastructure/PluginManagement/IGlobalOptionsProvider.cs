using CleanupForNotion.Core.Infrastructure.ConfigModels;

namespace CleanupForNotion.Core.Infrastructure.PluginManagement;

public interface IGlobalOptionsProvider {
  GlobalOptions GlobalOptions { get; }
}
