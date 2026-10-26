using CleanupForNotion.Core.Infrastructure.NotionIntegration;
using Notion.Client;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.NotionIntegration;

[TestClass]
public class CfnNotionClientExtensionsTests {
  [TestMethod]
  public async Task QueryDataSourceAsync_EmptyResults_ReturnsEmptyList() {
    // Arrange
    const string dataSourceId = "dataSourceId";
    var filter = new CheckboxFilter("propertyName", true);
    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDataSourceRawAsync(Arg.Is<QueryDataSourceRequest>(d => d.DataSourceId == dataSourceId && d.Filter == filter),
            CancellationToken.None)
        .Returns(new QueryDataSourceResponse { Results = [], HasMore = false });

    // Act
    var result = await client.QueryDataSourceAsync(dataSourceId, filter, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBeEmpty();
    await client.Received(1).QueryDataSourceRawAsync(Arg.Any<QueryDataSourceRequest>(), CancellationToken.None).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task QueryDataSourceAsync_OneResultOnOnePage_ReturnsOneItem() {
    // Arrange
    const string dataSourceId = "dataSourceId";
    var filter = new CheckboxFilter("propertyName", true);
    var page = new Page { Id = "1" };
    var client = Substitute.For<ICfnNotionClient>();
    client.QueryDataSourceRawAsync(Arg.Is<QueryDataSourceRequest>(d => d.DataSourceId == dataSourceId && d.Filter == filter),
            CancellationToken.None)
        .Returns(new QueryDataSourceResponse { Results = [page], HasMore = false });

    // Act
    var result = await client.QueryDataSourceAsync(dataSourceId, filter, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldHaveSingleItem().ShouldBe(page);
    await client.Received(1).QueryDataSourceRawAsync(Arg.Any<QueryDataSourceRequest>(), CancellationToken.None).ConfigureAwait(false);
  }

  [TestMethod]
  public async Task QueryDataSourceAsync_SevenResultsOnThreePages_ReturnsSevenItems() {
    // Arrange
    const string dataSourceId = "dataSourceId";
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
    client.QueryDataSourceRawAsync(Arg.Is<QueryDataSourceRequest>(d => d.DataSourceId == dataSourceId && d.Filter == filter && d.StartCursor == null),
            CancellationToken.None)
        .Returns(new QueryDataSourceResponse { Results = pages.Where(p => p.Id.StartsWith('1')).Cast<IQueryDataSourceResponseObject>().ToList(), HasMore = true, NextCursor = page2Cursor });

    client.QueryDataSourceRawAsync(Arg.Is<QueryDataSourceRequest>(d => d.DataSourceId == dataSourceId && d.Filter == filter && d.StartCursor == page2Cursor),
            CancellationToken.None)
        .Returns(new QueryDataSourceResponse { Results = pages.Where(p => p.Id.StartsWith('2')).Cast<IQueryDataSourceResponseObject>().ToList(), HasMore = true, NextCursor = page3Cursor });

    client.QueryDataSourceRawAsync(Arg.Is<QueryDataSourceRequest>(d => d.DataSourceId == dataSourceId && d.Filter == filter && d.StartCursor == page3Cursor),
            CancellationToken.None)
        .Returns(new QueryDataSourceResponse { Results = pages.Where(p => p.Id.StartsWith('3')).Cast<IQueryDataSourceResponseObject>().ToList(), HasMore = false });

    // Act
    var result = await client.QueryDataSourceAsync(dataSourceId, filter, CancellationToken.None).ConfigureAwait(false);

    // Assert
    result.ShouldBeEquivalentTo(pages);
    await client.Received(1).QueryDataSourceRawAsync(Arg.Is<QueryDataSourceRequest>(d => d.DataSourceId == dataSourceId && d.StartCursor == null), CancellationToken.None).ConfigureAwait(false);
    await client.Received(1).QueryDataSourceRawAsync(Arg.Is<QueryDataSourceRequest>(d => d.DataSourceId == dataSourceId && d.StartCursor == page2Cursor), CancellationToken.None).ConfigureAwait(false);
    await client.Received(1).QueryDataSourceRawAsync(Arg.Is<QueryDataSourceRequest>(d => d.DataSourceId == dataSourceId && d.StartCursor == page3Cursor), CancellationToken.None).ConfigureAwait(false);
  }
}
