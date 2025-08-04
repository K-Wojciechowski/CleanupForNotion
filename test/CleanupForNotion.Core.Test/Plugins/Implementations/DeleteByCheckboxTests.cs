using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Notion.Client;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Plugins.Implementations;

[TestClass]
public class DeleteByCheckboxTests : BasicDeletePluginTestsBase<DeleteByCheckbox, DeleteByCheckboxOptions> {
  [TestMethod]
  public void Name_Get_ReturnsDeleteByCheckbox() {
    new DeleteByCheckbox(null!, null!, null!, null!, null!).Name.ShouldBe("DeleteByCheckbox");
  }

  [TestMethod]
  public void Description_Get_ReturnsDescription() {
    var description = Guid.NewGuid().ToString();
    new DeleteByCheckbox(null!, null!, null!, null!, description).Description.ShouldBe(description);
  }

  [TestMethod]
  [DataRow(true, true)]
  [DataRow(true, false)]
  [DataRow(false, true)]
  [DataRow(false, false)]
  public async Task Run_Called_DeletesByCheckbox(bool dryRun, bool deleteIfChecked) {
    const string propertyName = "propertyName";
    var options = new DeleteByCheckboxOptions(
        DatabaseId: DatabaseId,
        PropertyName: propertyName,
        DeleteIfChecked: deleteIfChecked,
        GracePeriod: null);

    await TestRun(
        pluginCreator: (logger, timeProvider) => new DeleteByCheckbox(
            logger: logger,
            pluginStateProvider: Substitute.For<IPluginStateProvider>(),
            timeProvider: timeProvider,
            options: options,
            pluginDescription: string.Empty
        ),
        filters: [new CheckboxFilter(options.PropertyName, options.DeleteIfChecked)],
        options: options,
        dryRun: dryRun).ConfigureAwait(false);
  }
}
