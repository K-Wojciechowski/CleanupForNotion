namespace CleanupForNotion.Core.Infrastructure.Semaphores;

public class SemaphoreHolder : IDisposable {
  private readonly SemaphoreSlim _semaphore;
  private bool _disposed;

  public SemaphoreHolder(SemaphoreSlim semaphore) {
    _semaphore = semaphore;
  }

  public void Dispose() {
    if (_disposed) return;
    _disposed = true;
    _semaphore.Release();
  }
}
