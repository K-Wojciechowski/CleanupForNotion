using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Notion.Client;

namespace CleanupForNotion.Core.Infrastructure.Traceability;

public static class LoggerExtensions {
  private static readonly JsonSerializerSettings _jsonSerializerSettings = new() {
      NullValueHandling = NullValueHandling.Ignore
  };

  public static void LogFilters(this ILogger logger, Filter filter) {
    // Not a fan of Newtonsoft.Json, but Notion.Client is using that, so we must use it too to get correct output
    var jsonFilters = JsonConvert.SerializeObject(filter, Formatting.None, _jsonSerializerSettings);
    logger.LogInformation("Searching for pages to change with filters: {Filters}", jsonFilters);
  }
}
