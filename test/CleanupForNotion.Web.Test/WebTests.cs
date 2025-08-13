using System.Net;
using System.Threading.Channels;
using Shouldly;

namespace CleanupForNotion.Web.Test;

[TestClass]
public class WebTests {
  private static readonly TimeSpan _delta = TimeSpan.FromMilliseconds(50);

  [TestMethod]
  public async Task Post_CalledOnce_SendsOneMessageToChannel() {
    // Arrange
    using var factory = new CfnWebApplicationFactory();
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<DateTimeOffset>>();
    var timeProvider = factory.Services.GetRequiredService<TimeProvider>();
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    // Act
    var before = timeProvider.GetUtcNow();
    var response = await client.PostAsync(requestUri: "", content: null).ConfigureAwait(false);
    var after = timeProvider.GetUtcNow();

    // Assert
    var channelItem = await channel.Reader.ReadAsync().ConfigureAwait(false);
    channelItem.ShouldBeInRange(before.Subtract(_delta), after.Add(_delta));
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    responseText.ShouldBe("Triggered");
  }

  [TestMethod]
  public async Task Post_CalledThousandTimes_SendsThousandMessagesToChannel() {
    // Arrange
    using var factory = new CfnWebApplicationFactory();
    const int requestCount = 1000;
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<DateTimeOffset>>();
    var timeProvider = factory.Services.GetRequiredService<TimeProvider>();
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    // Act
    var before = timeProvider.GetUtcNow();
    for (var i = 0; i < requestCount; i++) {
      var response = await client.PostAsync(requestUri: "", content: null).ConfigureAwait(false);
      response.StatusCode.ShouldBe(HttpStatusCode.OK);
      var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
      responseText.ShouldBe("Triggered");
    }
    var after = timeProvider.GetUtcNow();

    // Assert
    for (var i = 0; i < requestCount; i++) {
      var channelItem = await channel.Reader.ReadAsync().ConfigureAwait(false);
      channelItem.ShouldBeInRange(before.Subtract(_delta), after.Add(_delta));
    }

    channel.Reader.TryPeek(out _).ShouldBeFalse();

    var averageRequestTime = (after - before).TotalMilliseconds / requestCount;
    averageRequestTime.ShouldBeLessThan(20);
  }
}
