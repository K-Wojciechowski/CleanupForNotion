using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Notion.Client;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Plugins.Implementations;

[TestClass]
public class DeleteWithoutRelationshipsTests : BasicDeletePluginTestsBase<DeleteWithoutRelationships, DeleteWithoutRelationshipsOptions> {
  [TestMethod]
  public void Name_Get_ReturnsDeleteWithoutRelationships() {
    new DeleteWithoutRelationships(null!, null!, null!, null!, null!).Name.ShouldBe("DeleteWithoutRelationships");
  }

  [TestMethod]
  public void Description_Get_ReturnsDescription() {
    var description = Guid.NewGuid().ToString();
    new DeleteWithoutRelationships(null!, null!, null!, null!, description).Description.ShouldBe(description);
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public async Task Run_Called_DeletesWithoutRelationships(bool dryRun) {
    const string propertyName = "propertyName";
    var options = new DeleteWithoutRelationshipsOptions(
        DatabaseId: DatabaseId,
        PropertyName: propertyName,
        GracePeriod: TimeSpan.FromMinutes(1));

    await TestRun(
        pluginCreator: (logger, timeProvider) => new DeleteWithoutRelationships(
            logger: logger,
            pluginStateProvider: Substitute.For<IPluginStateProvider>(),
            timeProvider: timeProvider,
            options: options,
            pluginDescription: string.Empty
        ),
        filters: [new RelationFilter(options.PropertyName, isEmpty: true)],
        options: options,
        dryRun: dryRun).ConfigureAwait(false);
  }
}
