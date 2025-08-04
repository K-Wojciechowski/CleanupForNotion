using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Plugins;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Notion.Client;

namespace CleanupForNotion.Test.Plugins.Implementations;

public abstract class BasicDeletePluginTestsBase<TPlugin, TOptions> : DeletePluginTestsBase
    where TPlugin : DeletePluginBase<TOptions>
    where TOptions : IDeletePluginOptions {
  protected const string DatabaseId = "databaseId";

  protected async Task TestRun(
      Func<ILogger<TPlugin>, TimeProvider, TPlugin> pluginCreator,
      List<Filter> filters,
      TOptions options,
      bool dryRun
  ) {
    // Arrange
    var logger = new FakeLogger<TPlugin>();
    var timeProvider = new FakeTimeProvider();
    var plugin = pluginCreator(logger, timeProvider);
    var lastEditedBefore = timeProvider.GetUtcNow() - options.GracePeriodWithFallback;
    var expectedCompoundFilter = new CompoundFilter(and: [
        new TimestampLastEditedTimeFilter(onOrBefore: lastEditedBefore.DateTime),
        .. filters
    ]);

    var client = CreateMockNotionClient();

    // Act
    await plugin.Run(client, new GlobalOptions { DryRun = dryRun }, CancellationToken.None).ConfigureAwait(false);

    // Assert
    await AssertPagesDeleted(logger, client, expectedCompoundFilter, dryRun).ConfigureAwait(false);
  }
}
