namespace CleanupForNotion.Core.Infrastructure.Notifications;

public interface INotificationSender {
  void Register(INotificationListener listener);

  void Unregister(INotificationListener listener);

  Task NotifyRunFinished(bool dryRun, CancellationToken cancellationToken);
}
