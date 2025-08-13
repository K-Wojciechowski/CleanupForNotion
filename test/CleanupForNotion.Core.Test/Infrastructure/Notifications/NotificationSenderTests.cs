using CleanupForNotion.Core.Infrastructure.Notifications;
using NSubstitute;

namespace CleanupForNotion.Core.Test.Infrastructure.Notifications;

[TestClass]
public class NotificationSenderTests {
  [TestMethod]
  public async Task TestNotificationSender() {
    // Arrange
    var listener1 = Substitute.For<INotificationListener>();
    var listener2 = Substitute.For<INotificationListener>();
    var listener3 = Substitute.For<INotificationListener>();

    var notificationSender = new NotificationSender();

    notificationSender.Register(listener1);
    notificationSender.Register(listener2);
    notificationSender.Register(listener3);

    // Act
    await notificationSender.NotifyRunFinished(false, CancellationToken.None).ConfigureAwait(false);
    notificationSender.Unregister(listener2);
    await notificationSender.NotifyRunFinished(true, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await listener1.Received().OnRunFinished(false, CancellationToken.None).ConfigureAwait(false);
    await listener2.Received().OnRunFinished(false, CancellationToken.None).ConfigureAwait(false);
    await listener3.Received().OnRunFinished(false, CancellationToken.None).ConfigureAwait(false);

    await listener1.Received().OnRunFinished(true, CancellationToken.None).ConfigureAwait(false);
    await listener3.Received().OnRunFinished(true, CancellationToken.None).ConfigureAwait(false);

    await listener2.DidNotReceive().OnRunFinished(true, CancellationToken.None).ConfigureAwait(false);

  }
}
