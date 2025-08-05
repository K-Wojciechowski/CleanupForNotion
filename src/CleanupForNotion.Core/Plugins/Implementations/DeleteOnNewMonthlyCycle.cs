using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Infrastructure.Time;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;
using Notion.Client;

namespace CleanupForNotion.Core.Plugins.Implementations;

public class DeleteOnNewMonthlyCycle : DeletePluginBase<DeleteOnNewMonthlyCycleOptions> {
  public override string Name => "DeleteOnNewMonthlyCycle";

  private const string LastRunSettingsKey = "LastRun";

  private readonly DeleteOnNewMonthlyCycleOptions _options;

  public DeleteOnNewMonthlyCycle(
      ILogger<DeletePluginBase<DeleteOnNewMonthlyCycleOptions>> logger,
      IPluginStateProvider pluginStateProvider,
      TimeProvider timeProvider,
      DeleteOnNewMonthlyCycleOptions options,
      string description) : base(logger, pluginStateProvider, timeProvider, options, description) {
    _options = options;
  }

  public override async Task Run(
      ICfnNotionClient client,
      GlobalOptions globalOptions,
      CancellationToken cancellationToken) {
    var utcNow = TimeProvider.GetUtcNow();
    var timeZone = _options.TimeZoneName != null ? TimeZoneInfoHelper.GetTimeZone(_options.TimeZoneName) : null;
    var now = timeZone != null ? TimeZoneInfo.ConvertTime(utcNow, timeZone) : utcNow;

    var currentCycleStartDate = GetCurrentCycleStartDate(now);
    var currentCycleStartGenericDateTime = currentCycleStartDate.ToDateTime(TimeOnly.MinValue);
    var currentCycleStartUserDateTime = GetMidnightInUserTimeZone(currentCycleStartDate, timeZone);
    var lastRun = await PluginStateProvider.GetDateTime(Name, Description, LastRunSettingsKey, cancellationToken).ConfigureAwait(false);

    if (lastRun == null) {
      Logger.LogDebug("Last run is null, will delete old cycles");
    } else if (lastRun.Value < currentCycleStartUserDateTime) {
      Logger.LogInformation("New cycle started, will delete old cycles");
    } else {
      Logger.LogInformation("Last run happened within the current cycle");
      return;
    }

    await DoDelete(client, globalOptions, [
        new DateFilter(_options.PropertyName, before: currentCycleStartGenericDateTime)
    ], cancellationToken).ConfigureAwait(false);

    await PluginStateProvider.SetDateTime(Name, Description, LastRunSettingsKey, now, cancellationToken).ConfigureAwait(false);
  }

  private DateOnly GetCurrentCycleStartDate(DateTimeOffset now) {
    if (now.Day >= _options.CycleResetDay) {
      return new DateOnly(now.Year, now.Month, _options.CycleResetDay);
    }

    var thisMonthStart = new DateOnly(now.Year, now.Month, 1);
    var previousMonthStart = thisMonthStart.AddMonths(-1);
    var localDate = previousMonthStart.AddDays(_options.CycleResetDay - 1);

    if (localDate.Month == now.Month && _options.MonthOverflowResetsOnFirstDayOfNextMonth) {
      localDate = thisMonthStart;
    }

    return localDate;
  }

  private static DateTimeOffset GetMidnightInUserTimeZone(DateOnly localDate, TimeZoneInfo? timeZone) {
    var localDateTime = localDate.ToDateTime(TimeOnly.MinValue);

    if (timeZone == null) {
      return new DateTimeOffset(localDateTime, TimeSpan.Zero);
    }

    var utcOffset = timeZone.GetUtcOffset(localDateTime);
    return new DateTimeOffset(localDateTime, utcOffset);
  }
}
