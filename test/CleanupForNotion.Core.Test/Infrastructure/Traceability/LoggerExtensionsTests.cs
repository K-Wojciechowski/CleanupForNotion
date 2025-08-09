using CleanupForNotion.Core.Infrastructure.Traceability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Notion.Client;
using Shouldly;

namespace CleanupForNotion.Test.Infrastructure.Traceability;

[TestClass]
public class LoggerExtensionsTests {
  [TestMethod]
  public void TestLogFilters() {
    // Arrange
    var filters = new CompoundFilter(and: [
        new DateFilter("DateProperty", equal: new DateTime(2000, 1, 2, 3, 4, 5)),
        new NumberFilter("NumberProperty", greaterThanOrEqualTo: 21.37)
    ]);
    var logger = new FakeLogger();

    // Act
    logger.LogFilters(filters);

    // Assert
    logger.LatestRecord.Level.ShouldBe(LogLevel.Information);
    logger.LatestRecord.Message.ShouldBe(expected:
        """Searching for pages to change with filters: {"and":[{"date":{"equals":"2000-01-02T03:04:05"},"property":"DateProperty"},{"number":{"greater_than_or_equal_to":21.37},"property":"NumberProperty"}]}""");
  }
}
