namespace CleanupForNotion.Web;

public abstract class CfnBackgroundService : BackgroundService {
  protected static Task ServiceStartupDelay(CancellationToken cancellationToken) => Task.Delay(100, cancellationToken);
}
