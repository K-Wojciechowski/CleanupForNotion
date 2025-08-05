using CleanupForNotion.Core;
using CleanupForNotion.Core.Infrastructure.Time;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Time.Testing;
using Notion.Client;
using Shouldly;

namespace CleanupForNotion.Test.Infrastructure.Time;

[TestClass]
public class LastEditedFilterHelperTests {
  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public void GetLastEditedFilter_WithOptions_ReturnsCorrectFilter(bool gracePeriodPresent) {
    var gracePeriod = gracePeriodPresent ? TimeSpan.FromMinutes(10) : Constants.DefaultGracePeriod;
    var options = new DeleteZeroSumOptions(DatabaseId: "", PropertyName: "", GracePeriod: gracePeriodPresent ? gracePeriod : null);
    TestGetLastEditedFilter(timeProvider => LastEditedFilterHelper.GetLastEditedFilter(timeProvider, options), gracePeriod);
  }

  [TestMethod]
  public void GetLastEditedFilter_WithGracePeriod_ReturnsCorrectFilter() {
    var gracePeriod = TimeSpan.FromMinutes(15);
    TestGetLastEditedFilter(timeProvider => LastEditedFilterHelper.GetLastEditedFilter(timeProvider, gracePeriod), gracePeriod);
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public void GetCompoundFilterWithLastEdited_WithOptions_ReturnsCorrectFilter(bool gracePeriodPresent) {
    var gracePeriod = gracePeriodPresent ? TimeSpan.FromMinutes(20) : Constants.DefaultGracePeriod;
    var options = new DeleteZeroSumOptions(DatabaseId: "", PropertyName: "", GracePeriod: gracePeriodPresent ? gracePeriod : null);
    TestGetCompoundFilterWithLastEdited((timeProvider, filters) => LastEditedFilterHelper.GetCompoundFilterWithLastEdited(timeProvider, options, filters), gracePeriod);
  }

  [TestMethod]
  public void GetCompoundFilterWithLastEdited_WithGracePeriod_ReturnsCorrectFilter() {
    var gracePeriod = TimeSpan.FromMinutes(25);
    TestGetCompoundFilterWithLastEdited((timeProvider, filters) => LastEditedFilterHelper.GetCompoundFilterWithLastEdited(timeProvider, gracePeriod, filters), gracePeriod);
  }

  private void TestGetLastEditedFilter(Func<TimeProvider, TimestampLastEditedTimeFilter> getLastEditedFilter, TimeSpan expectedGracePeriod) {
    // Arrange
    var now = new DateTimeOffset(2000, 1, 2, 3, 4, 5, TimeSpan.Zero);
    var timeProvider = new FakeTimeProvider(now);

    // Act
    var filter = getLastEditedFilter(timeProvider);

    // Assert
    filter.LastEditedTime.OnOrBefore.ShouldBe(now.DateTime - expectedGracePeriod);
  }

  private void TestGetCompoundFilterWithLastEdited(Func<TimeProvider, IEnumerable<Filter>, CompoundFilter> getCompoundFilter, TimeSpan expectedGracePeriod) {
    // Arrange
    var now = new DateTimeOffset(2000, 1, 2, 3, 4, 5, TimeSpan.Zero);
    var timeProvider = new FakeTimeProvider(now);

    var additionalFilter = new NumberFilter(propertyName: "TestProperty", greaterThan: 21.37);

    // Act
    var filter = getCompoundFilter(timeProvider, [additionalFilter]);

    // Assert
    filter.And.Count().ShouldBe(2);
    var lastEditedFilter = filter.And.First().ShouldBeOfType<TimestampLastEditedTimeFilter>();
    lastEditedFilter.ShouldNotBeNull();
    lastEditedFilter.LastEditedTime.OnOrBefore.ShouldBe(now.DateTime - expectedGracePeriod);
    filter.And.Last().ShouldBe(additionalFilter);
  }
}
