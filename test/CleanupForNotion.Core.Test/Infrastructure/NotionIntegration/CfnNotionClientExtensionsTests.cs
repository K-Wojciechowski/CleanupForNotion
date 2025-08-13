using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using Notion.Client;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.NotionIntegration;

[TestClass]
public class CfnNotionClientExtensionsTests {
  [TestMethod]
  public async Task QueryDatabaseAsync_EmptyResults_ReturnsEmptyList() {
    // Arrange
    const string databaseId = "databaseId";
    var filter = new CheckboxFilter("propertyName", true);
    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDatabaseRawAsync(databaseId, Arg.Is<DatabasesQueryParameters>(d => d.Filter == filter),
            CancellationToken.None)
        .Returns(new DatabaseQueryResponse { Results = [], HasMore = false });

    // Act
    var result = await client.QueryDatabaseAsync(databaseId, filter, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBeEmpty();
    await client.Received(1).QueryDatabaseRawAsync(databaseId, Arg.Any<DatabasesQueryParameters>(), CancellationToken.None).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task QueryDatabaseAsync_OneResultOnOnePage_ReturnsOneItem() {
    // Arrange
    const string databaseId = "databaseId";
    var filter = new CheckboxFilter("propertyName", true);
    var page = new Page { Id = "1" };
    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDatabaseRawAsync(databaseId, Arg.Is<DatabasesQueryParameters>(d => d.Filter == filter),
            CancellationToken.None)
        .Returns(new DatabaseQueryResponse { Results = [page], HasMore = false });

    // Act
    var result = await client.QueryDatabaseAsync(databaseId, filter, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldHaveSingleItem().ShouldBe(page);
    await client.Received(1).QueryDatabaseRawAsync(databaseId, Arg.Any<DatabasesQueryParameters>(), CancellationToken.None).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task QueryDatabaseAsync_SevenResultsOnThreePages_ReturnsSevenItems() {
    // Arrange
    const string databaseId = "databaseId";
    var filter = new CheckboxFilter("propertyName", true);
    var pages = new List<Page> {
        new() { Id = "1a" },
        new() { Id = "1b" },
        new() { Id = "1c" },
        new() { Id = "2a" },
        new() { Id = "2b" },
        new() { Id = "2c" },
        new() { Id = "3a" },
    };

    const string page2Cursor = "page2Cursor";
    const string page3Cursor = "page3Cursor";

    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDatabaseRawAsync(databaseId, Arg.Is<DatabasesQueryParameters>(d => d.Filter == filter && d.StartCursor == null),
            CancellationToken.None)
        .Returns(new DatabaseQueryResponse { Results = pages.Where(p => p.Id.StartsWith('1')).Cast<IWikiDatabase>().ToList(), HasMore = true, NextCursor = page2Cursor });

    client.QueryDatabaseRawAsync(databaseId, Arg.Is<DatabasesQueryParameters>(d => d.Filter == filter && d.StartCursor == page2Cursor),
            CancellationToken.None)
        .Returns(new DatabaseQueryResponse { Results = pages.Where(p => p.Id.StartsWith('2')).Cast<IWikiDatabase>().ToList(), HasMore = true, NextCursor = page3Cursor });

    client.QueryDatabaseRawAsync(databaseId, Arg.Is<DatabasesQueryParameters>(d => d.Filter == filter && d.StartCursor == page3Cursor),
            CancellationToken.None)
        .Returns(new DatabaseQueryResponse { Results = pages.Where(p => p.Id.StartsWith('3')).Cast<IWikiDatabase>().ToList(), HasMore = false });

    // Act
    var result = await client.QueryDatabaseAsync(databaseId, filter, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBeEquivalentTo(pages);
    await client.Received(1).QueryDatabaseRawAsync(databaseId, Arg.Is<DatabasesQueryParameters>(d => d.StartCursor == null), CancellationToken.None).ConfigureAwait(false);
    await client.Received(1).QueryDatabaseRawAsync(databaseId, Arg.Is<DatabasesQueryParameters>(d => d.StartCursor == page2Cursor), CancellationToken.None).ConfigureAwait(false);
    await client.Received(1).QueryDatabaseRawAsync(databaseId, Arg.Is<DatabasesQueryParameters>(d => d.StartCursor == page3Cursor), CancellationToken.None).ConfigureAwait(false);
  }
}
