using CleanupForNotion.Core.Infrastructure.ConfigModels;

namespace CleanupForNotion.Core.Infrastructure.Execution;

public interface IRunner {
  public Task RunCleanup(GlobalOptions globalOptions, string? groupId, CancellationToken cancellationToken);
}
