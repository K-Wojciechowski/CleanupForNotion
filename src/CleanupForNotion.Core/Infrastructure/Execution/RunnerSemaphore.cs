using CleanupForNotion.Core.Infrastructure.Semaphores;

namespace CleanupForNotion.Core.Infrastructure.Execution;

public class RunnerSemaphore : IRunnerSemaphore {
  private readonly SemaphoreSlim _semaphore = new(1, 1);

  public Task<SemaphoreHolder> AcquireAsync(TimeSpan timeout, CancellationToken cancellationToken) => _semaphore.AcquireAsync(timeout, cancellationToken);
}
