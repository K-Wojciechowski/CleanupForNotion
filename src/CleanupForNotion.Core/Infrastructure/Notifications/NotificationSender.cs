using CleanupForNotion.Core.Infrastructure.Semaphores;

namespace CleanupForNotion.Core.Infrastructure.Notifications;

public class NotificationSender : INotificationSender {
  private readonly List<INotificationListener> _listeners = [];
  private readonly SemaphoreSlim _semaphore = new(1, 1);

  public void Register(INotificationListener listener) {
    using var _ = _semaphore.Acquire();
    _listeners.Add(listener);
  }

  public void Unregister(INotificationListener listener) {
    using var _ = _semaphore.Acquire();
    _listeners.Remove(listener);
  }

  public async Task NotifyRunFinished(bool dryRun, CancellationToken cancellationToken) {
    List<Task> tasks;

    using (await _semaphore.AcquireAsync(cancellationToken).ConfigureAwait(false)) {
      tasks = _listeners.Select(listener => listener.OnRunFinished(dryRun, cancellationToken)).ToList();
    }

    await Task.WhenAll(tasks).ConfigureAwait(false);
  }
}
