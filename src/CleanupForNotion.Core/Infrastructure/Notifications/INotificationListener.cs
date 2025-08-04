namespace CleanupForNotion.Core.Infrastructure.Notifications;

public interface INotificationListener {
  Task OnRunFinished(bool dryRun, CancellationToken cancellationToken);
}
