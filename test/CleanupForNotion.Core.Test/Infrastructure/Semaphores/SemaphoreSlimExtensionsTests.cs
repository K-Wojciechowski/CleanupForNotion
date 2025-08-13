using CleanupForNotion.Core.Infrastructure.Semaphores;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.Semaphores;

[TestClass]
public class SemaphoreSlimExtensionsTests
{
  [TestMethod]
  public void Acquire_Called_BlocksAndReturnsHolder()
  {
    // Arrange
    var semaphore = new SemaphoreSlim(1, 1);

    // Act
    using var holder = semaphore.Acquire();

    // Assert
    semaphore.CurrentCount.ShouldBe(0);
  }

  [TestMethod]
  public async Task AcquireAsync_Called_BlocksAndReturnsHolder()
  {
    // Arrange
    var semaphore = new SemaphoreSlim(1, 1);

    // Act
    using var holder = await semaphore.AcquireAsync().ConfigureAwait(false);

    // Assert
    semaphore.CurrentCount.ShouldBe(0);
  }

  [TestMethod]
  public void Dispose_CalledAfterAcquire_ReleasesSemaphore()
  {
    // Arrange
    var semaphore = new SemaphoreSlim(1, 1);

    // Act
    SemaphoreHolder holder = semaphore.Acquire();
    holder.Dispose();

    // Assert
    semaphore.CurrentCount.ShouldBe(1);
  }

  [TestMethod]
  public async Task Dispose_CalledAfterAcquireAsync_ReleasesSemaphore()
  {
    // Arrange
    var semaphore = new SemaphoreSlim(1, 1);

    // Act
    var holder = await semaphore.AcquireAsync().ConfigureAwait(false);
    holder.Dispose();

    // Assert
    semaphore.CurrentCount.ShouldBe(1);
  }

  [TestMethod]
  public async Task AcquireAsync_Twice_SecondTaskIsNotCompletedIfSemaphoreTaken()
  {
    // Arrange
    var semaphore = new SemaphoreSlim(1, 1);

    // Act
    var holder1 = await semaphore.AcquireAsync().ConfigureAwait(false);
    semaphore.CurrentCount.ShouldBe(0);
    var holder2Task = semaphore.AcquireAsync();

    // Assert
    holder2Task.IsCompleted.ShouldBeFalse();
    semaphore.CurrentCount.ShouldBe(0);

    holder1.Dispose();
    var holder2 = await holder2Task.ConfigureAwait(false);
    semaphore.CurrentCount.ShouldBe(0);
    holder2.Dispose();
  }

  [TestMethod]
  public async Task AcquireAsyncWithCancellationToken_Twice_SecondTaskIsNotCompletedIfSemaphoreTaken()
  {
    // Arrange
    var semaphore = new SemaphoreSlim(1, 1);
    using var cancellationTokenSource = new CancellationTokenSource();

    // Act
    var holder1 = await semaphore.AcquireAsync(cancellationTokenSource.Token).ConfigureAwait(false);
    semaphore.CurrentCount.ShouldBe(0);
    var holder2Task = semaphore.AcquireAsync(cancellationTokenSource.Token);

    // Assert
    holder2Task.IsCompleted.ShouldBeFalse();
    semaphore.CurrentCount.ShouldBe(0);

    await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
    await Task.Yield();

    holder1.Dispose();
    semaphore.CurrentCount.ShouldBe(1);
    Func<Task> holder2Act = async () => await holder2Task.ConfigureAwait(false);
    holder2Act.ShouldThrow<TaskCanceledException>();
  }
}
