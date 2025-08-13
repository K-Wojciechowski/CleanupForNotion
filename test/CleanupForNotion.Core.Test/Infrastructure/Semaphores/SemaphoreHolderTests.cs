using CleanupForNotion.Core.Infrastructure.Semaphores;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.Semaphores;

[TestClass]
public class SemaphoreHolderTests
{
    [TestMethod]
    public void Dispose_CalledOnce_ReleasesSemaphore()
    {
        // Arrange
        var semaphore = new SemaphoreSlim(0, 1);
        var holder = new SemaphoreHolder(semaphore);

        // Act
        holder.Dispose();

        // Assert
        semaphore.CurrentCount.ShouldBe(1);
    }

    [TestMethod]
    public void Dispose_CalledMultipleTimes_ReleasesSemaphoreOnlyOnce()
    {
        // Arrange
        var semaphore = new SemaphoreSlim(0, 1);
        var holder = new SemaphoreHolder(semaphore);

        // Act
        holder.Dispose();
        holder.Dispose();

        // Assert
        semaphore.CurrentCount.ShouldBe(1);
    }
}
