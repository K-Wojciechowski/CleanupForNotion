using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using CleanupForNotion.Core.Infrastructure.Time;
using CleanupForNotion.Core.Plugins.Implementations;
using CleanupForNotion.Core.Plugins.Options;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Notion.Client;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Test.Plugins.Implementations;

[TestClass]
public class EnsureStaticRelatedPageTests {
  private const string DatabaseId = "databaseId";
  private const string PropertyName = "propertyName";
  private const string PluginDescription = "pluginDescription";
  private const string RelatedPageId = "relatedPageId";

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public async Task Run_NormalMode_UpdatesPagesIfNotDryRun(bool dryRun) {
    // Arrange
    var logger = new FakeLogger<EnsureStaticRelatedPage>();
    var timeProvider = new FakeTimeProvider();
    var options = new EnsureStaticRelatedPageOptions(
        DatabaseId: DatabaseId,
        PropertyName: PropertyName,
        RelatedPageId: RelatedPageId
    );
    var plugin = new EnsureStaticRelatedPage(logger, timeProvider, options, PluginDescription);

    var pages = new List<Page> {
        new() {
            Id = "page1",
            Properties = new Dictionary<string, PropertyValue> {
                {
                    PropertyName, new RelationPropertyValue {
                        Relation = [
                            new ObjectId { Id = "other1" },
                            new ObjectId { Id = "other2" }
                        ]
                    }
                }
            }
        },
        new() {
            Id = "page2",
            Properties = new Dictionary<string, PropertyValue> {
                { PropertyName, new NumberPropertyValue { Number = 21.37 } }
            }
        },
    };

    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDatabaseRawAsync(DatabaseId, Arg.Any<DatabasesQueryParameters>(), CancellationToken.None)
        .Returns(new DatabaseQueryResponse { Results = pages.Cast<IWikiDatabase>().ToList(), HasMore = false });

    var relationFilter = new RelationFilter(options.PropertyName, doesNotContain: options.RelatedPageId);
    var expectedFilter = LastEditedFilterHelper.GetCompoundFilterWithLastEdited(
        timeProvider, options, [relationFilter]);

    var expectedLogMessages = new[] { "Updating page 'page1'", "Updating page 'page2'", };

    // Act
    await plugin.Run(client, new GlobalOptions { DryRun = dryRun }, CancellationToken.None).ConfigureAwait(false);

    // Assert
    var queryCall = client.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "QueryDatabaseRawAsync");
    var queryCallArguments = queryCall.GetArguments();
    queryCallArguments[0].ShouldBe(DatabaseId);
    queryCallArguments[1].ShouldBeOfType<DatabasesQueryParameters>().Filter
        .ShouldBeEquivalentTo(expectedFilter);
    queryCallArguments[2].ShouldBe(CancellationToken.None);

    await client.DidNotReceiveWithAnyArgs().DeletePageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    var logMessages = logger.Collector.GetSnapshot().Select(r => r.Message).ToArray();

    logMessages[0].ShouldStartWith("Searching for pages to change with filters:");
    logMessages.Skip(1).ToArray().ShouldBeEquivalentTo(expectedLogMessages);

    if (dryRun) {
      await client.DidNotReceiveWithAnyArgs().UpdatePageAsync(Arg.Any<string>(), Arg.Any<PagesUpdateParameters>(),
              Arg.Any<CancellationToken>())
          .ConfigureAwait(false);
      return;
    }

    await client.Received(1).UpdatePageAsync("page1", Arg.Any<PagesUpdateParameters>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);
    await client.Received(1).UpdatePageAsync("page2", Arg.Any<PagesUpdateParameters>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    var updateParameters = client.ReceivedCalls()
        .Where(c => c.GetMethodInfo().Name == "UpdatePageAsync")
        .Select(c => c.GetArguments()[1]);

    foreach (var p in updateParameters) {
      var pagesUpdateParameters = p.ShouldBeOfType<PagesUpdateParameters>();
      pagesUpdateParameters.Properties.ShouldContainKey(options.PropertyName);
      var relationValue = pagesUpdateParameters.Properties[options.PropertyName]
          .ShouldBeOfType<RelationPropertyValue>();
      relationValue.Relation.ShouldHaveSingleItem().Id.ShouldBe(options.RelatedPageId);
    }
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public async Task Run_NormalModeWithNoPagesToUpdate_DoesNothing(bool dryRun) {
    // Arrange

    var logger = new FakeLogger<EnsureStaticRelatedPage>();
    var timeProvider = new FakeTimeProvider();
    var options = new EnsureStaticRelatedPageOptions(
        DatabaseId: DatabaseId,
        PropertyName: PropertyName,
        RelatedPageId: RelatedPageId
    );
    var plugin = new EnsureStaticRelatedPage(logger, timeProvider, options, PluginDescription);

    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDatabaseRawAsync(DatabaseId, Arg.Any<DatabasesQueryParameters>(), CancellationToken.None)
        .Returns(new DatabaseQueryResponse { Results = [], HasMore = false });

    var relationFilter = new RelationFilter(options.PropertyName, doesNotContain: options.RelatedPageId);
    var expectedFilter = LastEditedFilterHelper.GetCompoundFilterWithLastEdited(
        timeProvider, options, [relationFilter]);

    // Act
    await plugin.Run(client, new GlobalOptions { DryRun = dryRun }, CancellationToken.None).ConfigureAwait(false);

    // Assert
    var queryCall = client.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "QueryDatabaseRawAsync");
    var queryCallArguments = queryCall.GetArguments();
    queryCallArguments[0].ShouldBe(DatabaseId);
    queryCallArguments[1].ShouldBeOfType<DatabasesQueryParameters>().Filter
        .ShouldBeEquivalentTo(expectedFilter);
    queryCallArguments[2].ShouldBe(CancellationToken.None);

    await client.DidNotReceiveWithAnyArgs().UpdatePageAsync(Arg.Any<string>(), Arg.Any<PagesUpdateParameters>(),
            Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    await client.DidNotReceiveWithAnyArgs().DeletePageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    logger.Collector.LatestRecord.Message.ShouldBe("No pages to update");
  }

  [TestMethod]
  public async Task Run_DebugMode_PrintsPagesAndDoesNoUpdates() {
    // Arrange
    var logger = new FakeLogger<EnsureStaticRelatedPage>();
    var timeProvider = new FakeTimeProvider();
    var options = new EnsureStaticRelatedPageOptions(
        DatabaseId: DatabaseId,
        PropertyName: PropertyName,
        RelatedPageId: "?"
    );
    var plugin = new EnsureStaticRelatedPage(logger, timeProvider, options, PluginDescription);

    var pages = new List<Page> {
        new() {
            Id = "page1",
            Properties = new Dictionary<string, PropertyValue> {
                {
                    "Title", new TitlePropertyValue {
                        Title = [
                            new RichTextText { PlainText = "Foo" },
                            new RichTextText { PlainText = "Bar" }
                        ]
                    }
                }, {
                    PropertyName, new RelationPropertyValue {
                        Relation = [
                            new ObjectId { Id = "other1" },
                            new ObjectId { Id = "other2" }
                        ]
                    }
                }
            }
        },
        new() {
            Id = "page2",
            Properties =
                new Dictionary<string, PropertyValue> { { PropertyName, new NumberPropertyValue { Number = 21.37 } } }
        },
        new() {
            Id = "page3",
            Properties = new Dictionary<string, PropertyValue> {
                { PropertyName + "2", new NumberPropertyValue { Number = 21.37 } }
            }
        }
    };

    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDatabaseRawAsync(DatabaseId, Arg.Any<DatabasesQueryParameters>(), CancellationToken.None)
        .Returns(new DatabaseQueryResponse { Results = pages.Cast<IWikiDatabase>().ToList(), HasMore = false });

    var expectedFilter = LastEditedFilterHelper.GetLastEditedFilter(timeProvider, TimeSpan.Zero);
    var expectedLogMessages = new[] {
        $"Running {plugin.Name} ({plugin.Description}) in debug mode",
        $"Page 'page1' ('FooBar') - property {PropertyName} = 'other1, other2'",
        $"Page 'page2' ('(unknown)') - property {PropertyName} has incorrect type '{PropertyType.Number}'",
        $"Page 'page3' ('(unknown)') - property {PropertyName} not found",
    };

    // Act
    await plugin.Run(client, new GlobalOptions { DryRun = false }, CancellationToken.None).ConfigureAwait(false);

    // Assert
    var queryCall = client.ReceivedCalls().Single(c => c.GetMethodInfo().Name == "QueryDatabaseRawAsync");
    var queryCallArguments = queryCall.GetArguments();
    queryCallArguments[0].ShouldBe(DatabaseId);
    queryCallArguments[1].ShouldBeOfType<DatabasesQueryParameters>().Filter
        .ShouldBeEquivalentTo(expectedFilter);
    queryCallArguments[2].ShouldBe(CancellationToken.None);

    await client.DidNotReceiveWithAnyArgs().UpdatePageAsync(Arg.Any<string>(), Arg.Any<PagesUpdateParameters>(),
            Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    await client.DidNotReceiveWithAnyArgs().DeletePageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
        .ConfigureAwait(false);

    logger.Collector.GetSnapshot().Select(r => r.Message).ToArray()
        .ShouldBeEquivalentTo(expectedLogMessages);
  }
}
