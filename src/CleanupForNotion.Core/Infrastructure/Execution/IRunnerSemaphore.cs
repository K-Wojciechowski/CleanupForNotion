using CleanupForNotion.Core.Infrastructure.Semaphores;

namespace CleanupForNotion.Core.Infrastructure.Execution;

public interface IRunnerSemaphore
{
  Task<SemaphoreHolder> AcquireAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
