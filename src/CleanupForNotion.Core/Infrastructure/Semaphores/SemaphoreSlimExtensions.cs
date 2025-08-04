namespace CleanupForNotion.Core.Infrastructure.Semaphores;

public static class SemaphoreSlimExtensions {
  public static SemaphoreHolder Acquire(this SemaphoreSlim semaphore) {
    semaphore.Wait();
    return new SemaphoreHolder(semaphore);
  }

  public static async Task<SemaphoreHolder> AcquireAsync(this SemaphoreSlim semaphore) {
    await semaphore.WaitAsync().ConfigureAwait(false);
    return new SemaphoreHolder(semaphore);
  }

  public static async Task<SemaphoreHolder> AcquireAsync(this SemaphoreSlim semaphore, CancellationToken cancellationToken) {
    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
    return new SemaphoreHolder(semaphore);
  }


  public static async Task<SemaphoreHolder> AcquireAsync(this SemaphoreSlim semaphore, TimeSpan timeout, CancellationToken cancellationToken) {
    var acquired = await semaphore.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
    if (!acquired) throw new TimeoutException("Could not acquire semaphore within timeout.");
    return new SemaphoreHolder(semaphore);
  }
}
