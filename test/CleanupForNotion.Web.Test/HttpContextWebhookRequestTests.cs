using CleanupForNotion.Core;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Web.Test;

[TestClass]
public class HttpContextWebhookRequestTests {
  [TestMethod]
  [DataRow(1L)]
  [DataRow(0L)]
  [DataRow(null)]
  public void ContentLength_Called_ReturnsContentLength(long? contentLength) {
    // Arrange
    var httpContext = Substitute.For<HttpContext>();
    httpContext.Request.ContentLength.Returns(contentLength);
    var httpContextWebhookRequest = new HttpContextWebhookRequest(httpContext);

    // Act & Assert
    httpContextWebhookRequest.ContentLength.ShouldBe(contentLength);
  }

  [TestMethod]
  public void SignatureHeader_HeaderMissing_ReturnsNull() {
    // Arrange
    var headerDictionary = new HeaderDictionary();
    var httpContext = Substitute.For<HttpContext>();
    httpContext.Request.Headers.Returns(headerDictionary);
    var httpContextWebhookRequest = new HttpContextWebhookRequest(httpContext);

    // Act & Assert
    httpContextWebhookRequest.SignatureHeader.ShouldBe(null);
  }

  [TestMethod]
  public void SignatureHeader_HeaderPresent_ReturnsIt() {
    // Arrange
    var headerDictionary = new HeaderDictionary();

    var httpContext = Substitute.For<HttpContext>();
    httpContext.Request.Headers.Returns(headerDictionary);
    var httpContextWebhookRequest = new HttpContextWebhookRequest(httpContext);

    // Act & Assert
    headerDictionary[Constants.WebhookSignatureHeaderName] = [];
    httpContextWebhookRequest.SignatureHeader.ShouldBeNull();

    headerDictionary[Constants.WebhookSignatureHeaderName] = new StringValues("one");
    httpContextWebhookRequest.SignatureHeader.ShouldBe("one");

    headerDictionary[Constants.WebhookSignatureHeaderName] = new StringValues(["one", "two"]);
    httpContextWebhookRequest.SignatureHeader.ShouldBe("one,two");
  }

  [TestMethod]
  public async Task ReadBodyAsync_ContentLengthNull_Throws() {
    // Arrange
    var httpContext = Substitute.For<HttpContext>();
    httpContext.Request.ContentLength.Returns((long?)null);
    var httpContextWebhookRequest = new HttpContextWebhookRequest(httpContext);

    // Act
    Func<Task> act = async () =>
        await httpContextWebhookRequest.ReadBodyAsync(CancellationToken.None).ConfigureAwait(false);

    // Assert
    await act.ShouldThrowAsync<InvalidOperationException>().ConfigureAwait(false);
  }

  [TestMethod]
  public async Task ReadBodyAsync_ContentLengthNotNull_ReturnsArray() {
    var httpContext = Substitute.For<HttpContext>();
    var body = "hello"u8.ToArray();
    var bodyStream = new MemoryStream(body);

    httpContext.Request.ContentLength.Returns(body.Length);
    httpContext.Request.Body.Returns(bodyStream);

    var httpContextWebhookRequest = new HttpContextWebhookRequest(httpContext);

    // Act
    var result = await httpContextWebhookRequest.ReadBodyAsync(CancellationToken.None).ConfigureAwait(false);
    result.Length.ShouldBe(body.Length);
    result.ShouldBeEquivalentTo(body);
  }
}
