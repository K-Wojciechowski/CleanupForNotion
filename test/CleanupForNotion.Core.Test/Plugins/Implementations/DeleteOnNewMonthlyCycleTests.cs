using System.Globalization;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Infrastructure.TimeZones;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Notion.Client;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Plugins.Implementations;

[TestClass]
public class DeleteOnNewMonthlyCycleTests : DeletePluginTestsBase {
  private const string DatabaseId = "databaseId";
  private const string PropertyName = "propertyName";
  private const int CycleResetDay = 10;
  private const string PluginName = "DeleteOnNewMonthlyCycle";
  private const string PluginDescription = "pluginDescription";
  private const string LastRunSettingsKey = "LastRun";

  private readonly DeleteOnNewMonthlyCycleOptions _standardOptions = new(
      DatabaseId: DatabaseId,
      PropertyName: PropertyName,
      CycleResetDay: CycleResetDay,
      MonthOverflowResetsOnFirstDayOfNextMonth: false,
      TimeZoneName: "Europe/Warsaw",
      GracePeriod: null);

  [TestMethod]
  public void Name_Get_ReturnsDeleteOnNewMonthlyCycle() {
    new DeleteOnNewMonthlyCycle(null!, null!, null!, null!, null!).Name.ShouldBe(PluginName);
  }

  [TestMethod]
  public void Description_Get_ReturnsDescription() {
    var description = Guid.NewGuid().ToString();
    new DeleteOnNewMonthlyCycle(null!, null!, null!, null!, description).Description.ShouldBe(description);
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public async Task Run_NoLastRun_Deletes(bool dryRun) {
    await TestRunWithDelete(
        lastRun: null,
        now: new DateTimeOffset(2025, 1, 9, 22, 0, 0, TimeSpan.Zero),
        currentCycleStartDate: new DateOnly(2024, 12, 10),
        options: _standardOptions,
        logMessage: "Last run is null, will delete old cycles",
        dryRun: dryRun
    ).ConfigureAwait(false);
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public async Task Run_LastRunInPreviousCycle_Deletes(bool dryRun) {
    await TestRunWithDelete(
        lastRun: new DateTimeOffset(2025, 2, 9, 22, 0, 0, TimeSpan.Zero),
        now: new DateTimeOffset(2025, 2, 9, 23, 0, 0, TimeSpan.Zero),
        currentCycleStartDate: new DateOnly(2025, 2, 10),
        options: _standardOptions,
        logMessage: "New cycle started, will delete old cycles",
        dryRun: dryRun
    ).ConfigureAwait(false);
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public async Task Run_LastRunInPreviousCycleNoUserTimeZone_Deletes(bool dryRun) {
    await TestRunWithDelete(
        lastRun: new DateTimeOffset(2025, 2, 9, 23, 59, 59, TimeSpan.Zero),
        now: new DateTimeOffset(2025, 2, 10, 0, 0, 0, TimeSpan.Zero),
        currentCycleStartDate: new DateOnly(2025, 2, 10),
        options: _standardOptions with { TimeZoneName = null },
        logMessage: "New cycle started, will delete old cycles",
        dryRun: dryRun
    ).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_LastRunInCurrentCycleSameMonth_DoesNotDelete() {
    var lastRun = new DateTimeOffset(2025, 1, 9, 23, 0, 0, TimeSpan.Zero);
    var now = new DateTimeOffset(2025, 1, 9, 22, 59, 59, TimeSpan.Zero);
    await TestRunWithoutDelete(lastRun, now, _standardOptions).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_LastRunInCurrentCycleNextMonthNoOverflowOnFirstDay_DoesNotDelete() {
    var lastRun = new DateTimeOffset(2025, 1, 9, 23, 0, 0, TimeSpan.Zero);
    var now = new DateTimeOffset(2025, 2, 9, 22, 59, 59, TimeSpan.Zero);
    await TestRunWithoutDelete(lastRun, now, _standardOptions).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_MonthOverflowResetsOnFirstDayOfNextMonthTrue_FebruaryDeletionOnFirstOfMarch() {
    await TestRunWithDelete(
        lastRun: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        now: new DateTimeOffset(2025, 3, 1, 10, 0, 0, TimeSpan.Zero),
        currentCycleStartDate: new DateOnly(2025, 3, 1),
        options: _standardOptions with { MonthOverflowResetsOnFirstDayOfNextMonth = true, CycleResetDay = 30 },
        logMessage: "New cycle started, will delete old cycles",
        dryRun: false
    ).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task Run_MonthOverflowResetsOnFirstDayOfNextMonthFalse_FebruaryDeletionOnSecondOfMarch() {
    await TestRunWithDelete(
        lastRun: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        now: new DateTimeOffset(2025, 3, 1, 10, 0, 0, TimeSpan.Zero),
        currentCycleStartDate: new DateOnly(2025, 3, 2),
        options: _standardOptions with { MonthOverflowResetsOnFirstDayOfNextMonth = false, CycleResetDay = 30 },
        logMessage: "New cycle started, will delete old cycles",
        dryRun: false
    ).ConfigureAwait(false);
  }

  private static async Task TestRunWithDelete(
      DateTimeOffset? lastRun,
      DateTimeOffset now,
      DateOnly currentCycleStartDate,
      DeleteOnNewMonthlyCycleOptions options,
      string logMessage,
      bool dryRun) {
    // Arrange
    var logger = new FakeLogger<DeleteOnNewMonthlyCycle>();
    var pluginStateProvider = CreateMockStateProvider(lastRun);
    var timeProvider = new FakeTimeProvider(now);
    var plugin = new DeleteOnNewMonthlyCycle(logger, pluginStateProvider, timeProvider, options, PluginDescription);

    var client = CreateMockNotionClient();

    var currentCycleStartGenericDateTime = currentCycleStartDate.ToDateTime(TimeOnly.MinValue);
    var lastEditedBefore = timeProvider.GetUtcNow() - ((IDeletePluginOptions)options).GracePeriodWithFallback;

    var expectedCompoundFilter = new CompoundFilter(and: [
        new TimestampLastEditedTimeFilter(onOrBefore: lastEditedBefore.DateTime),
        new DateFilter(options.PropertyName, before: currentCycleStartGenericDateTime)
    ]);

    var nowLocal = options.TimeZoneName != null
        ? TimeZoneInfo.ConvertTime(now, TimeZoneInfoHelper.GetTimeZone(options.TimeZoneName))
        : now;

    // Act
    await plugin.Run(client, new GlobalOptions { DryRun = dryRun }, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await AssertPagesDeleted(logger, client, expectedCompoundFilter, dryRun).ConfigureAwait(false);
    await pluginStateProvider.Received().SetDateTime(PluginName, PluginDescription, LastRunSettingsKey, nowLocal, CancellationToken.None).ConfigureAwait(false);
    logger.Collector.GetSnapshot().ShouldContain(l => l.Message == logMessage);
  }

  private static async Task TestRunWithoutDelete(
      DateTimeOffset? lastRun,
      DateTimeOffset now,
      DeleteOnNewMonthlyCycleOptions options) {
    // Arrange
    var logger = new FakeLogger<DeleteOnNewMonthlyCycle>();
    var pluginStateProvider = CreateMockStateProvider(lastRun);
    var timeProvider = new FakeTimeProvider(now);
    var plugin = new DeleteOnNewMonthlyCycle(logger, pluginStateProvider, timeProvider, options, PluginDescription);

    var client = Substitute.For<ICfnNotionClient>();

    // Act
    await plugin.Run(client, new GlobalOptions { DryRun = false }, CancellationToken.None).ConfigureAwait(false);

    // Assert
    logger.LatestRecord.Message.ShouldBe("Last run happened within the current cycle");
    client.ReceivedCalls().ShouldBeEmpty();
  }

  private static IPluginStateProvider CreateMockStateProvider(DateTimeOffset? lastRun) {
    var provider = Substitute.For<IPluginStateProvider>();
    provider.GetString(PluginName, PluginDescription, LastRunSettingsKey, CancellationToken.None)
        .Returns(lastRun?.ToString("o", CultureInfo.InvariantCulture));
    return provider;
  }
}
