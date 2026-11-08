using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Web.NotionModels;
using Notion.Client;
using Shouldly;

namespace CleanupForNotion.Web.Test;

[TestClass]
public class WebTests {
  private static readonly TimeSpan _delta = TimeSpan.FromMilliseconds(50);

  // taken from example payload: https://developers.notion.com/reference/webhooks#step-2-verifying-the-subscription
  private const string WebhookSecret = "sec" + "ret" + "_tMrlL1qK5vuQAh1b6cZGhFChZTSYJlce98V0pYn7yBl";

  [TestMethod]
  public async Task PostRoot_CalledOnce_SendsOneMessageToChannel() {
    // Arrange
    using var factory = new CfnWebApplicationFactory();
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var timeProvider = factory.Services.GetRequiredService<TimeProvider>();
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    // Act
    var before = timeProvider.GetUtcNow();
    var response = await client.PostAsync(requestUri: "", content: null, TestContext.CancellationToken)
        .ConfigureAwait(false);
    var after = timeProvider.GetUtcNow();

    // Assert
    var runRequest = await channel.Reader.ReadAsync(TestContext.CancellationToken).ConfigureAwait(false);
    runRequest.TriggerTime.ShouldBeInRange(before.Subtract(_delta), after.Add(_delta));
    runRequest.PluginId.ShouldBeNull();
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var responseText = await response.Content.ReadAsStringAsync(TestContext.CancellationToken).ConfigureAwait(false);
    responseText.ShouldBe("Triggered");
  }

  [TestMethod]
  public async Task PostRoot_CalledThousandTimes_SendsThousandMessagesToChannel() {
    // Arrange
    using var factory = new CfnWebApplicationFactory();
    const int requestCount = 1000;
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var timeProvider = factory.Services.GetRequiredService<TimeProvider>();
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    // Act
    var before = timeProvider.GetUtcNow();
    for (var i = 0; i < requestCount; i++) {
      var response = await client.PostAsync(requestUri: "", content: null, TestContext.CancellationToken)
          .ConfigureAwait(false);
      response.StatusCode.ShouldBe(HttpStatusCode.OK);
      var responseText = await response.Content.ReadAsStringAsync(TestContext.CancellationToken).ConfigureAwait(false);
      responseText.ShouldBe("Triggered");
    }

    var after = timeProvider.GetUtcNow();

    // Assert
    for (var i = 0; i < requestCount; i++) {
      var runRequest = await channel.Reader.ReadAsync(TestContext.CancellationToken).ConfigureAwait(false);
      runRequest.TriggerTime.ShouldBeInRange(before.Subtract(_delta), after.Add(_delta));
      runRequest.PluginId.ShouldBeNull();
    }

    channel.Reader.TryPeek(out _).ShouldBeFalse();

    var averageRequestTime = (after - before).TotalMilliseconds / requestCount;
    averageRequestTime.ShouldBeLessThan(20);
  }

  [TestMethod]
  public async Task PostPluginId_CalledOnce_SendsOneMessageToChannel() {
    // Arrange
    using var factory = new CfnWebApplicationFactory();
    var pluginId = Guid.NewGuid().ToString();
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var timeProvider = factory.Services.GetRequiredService<TimeProvider>();
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    // Act
    var before = timeProvider.GetUtcNow();
    var response = await client
        .PostAsync(requestUri: $"plugin/{pluginId}", content: null, TestContext.CancellationToken)
        .ConfigureAwait(false);
    var after = timeProvider.GetUtcNow();

    // Assert
    var runRequest = await channel.Reader.ReadAsync(TestContext.CancellationToken).ConfigureAwait(false);
    runRequest.TriggerTime.ShouldBeInRange(before.Subtract(_delta), after.Add(_delta));
    runRequest.PluginId.ShouldBe(pluginId);
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var responseText = await response.Content.ReadAsStringAsync(TestContext.CancellationToken).ConfigureAwait(false);
    responseText.ShouldBe("Triggered plugin");
  }

  [TestMethod]
  public async Task PostWebhook_NoSecretConfiguredAndShortRequest_LogsRequestBody() {
    // Arrange
    using var factory = new CfnWebApplicationFactory();
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();
    const string body = $$"""
                          {
                            "verification_token": "{{WebhookSecret}}"
                          }
                          """;

    var content = new StringContent(body, Encoding.UTF8, "application/json");

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r =>
        r.Message == "Received new webhook request with body " + body.Replace("\n", "").Trim());
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  [TestMethod]
  public async Task PostWebhook_NoSecretConfiguredAndLongRequest_ReturnsBadRequest() {
    // Arrange
    using var factory = new CfnWebApplicationFactory();
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();
    var body = new string('a', 1025);
    var content = new StringContent(body, Encoding.UTF8, "application/json");

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Webhook request rejected due to invalid content length");
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  [TestMethod]
  [DataRow("secret", DisplayName = "webhook secret configured")]
  [DataRow(null, DisplayName = "webhook secret not configured")]
  public async Task PostWebhook_RequestWithoutContentLength_ReturnsBadRequest(string? webhookSecret) {
    // Arrange
    using var factory = new CfnWebApplicationFactory(webhookSecret);
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();
    const string body = "hello";
    using var content = new UnknownLengthContent(body);
    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
    using var request = new HttpRequestMessage(HttpMethod.Post, "webhook") { Content = content };
    request.Headers.TransferEncodingChunked = true;

    // Act
    var response = await client.SendAsync(request, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Webhook request rejected due to invalid content length");
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  [TestMethod]
  public async Task PostWebhook_SecretConfiguredButWebhooksNotConfigured_ReturnsOkWithoutSendingMessagesToChannel() {
    // Arrange
    using var factory = new CfnWebApplicationFactory(webhookSecret: "secret");
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();
    const string body = "invalid json which will not be parsed";
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    content.Headers.Add("X-Notion-Signature", "sha256-invalid");

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Webhooks not configured");
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  [TestMethod]
  public async Task PostWebhook_MissingSignature_ReturnsBadRequest() {
    // Arrange
    using var factory = new CfnWebApplicationFactory(webhookSecret: "secret", webhookConfigurations: []);
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();
    const string body = "invalid json which will not be parsed";
    var content = new StringContent(body, Encoding.UTF8, "application/json");

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Received unsigned webhook request");
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  [TestMethod]
  public async Task PostWebhook_InvalidSignature_ReturnsBadRequest() {
    // Arrange
    using var factory = new CfnWebApplicationFactory(webhookSecret: WebhookSecret, webhookConfigurations: []);
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();
    const string body = "invalid json which will not be parsed";
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    content.Headers.Add("X-Notion-Signature", "sha256-invalid");

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Received webhook request with invalid signature");
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  [TestMethod]
  public async Task PostWebhook_ValidDataSourceChange_SendsPluginIdsInMessagesToChannel() {
    // Arrange
    var dataSourceId = Guid.NewGuid().ToString();
    var webhookConfigurations = new List<WebhookConfiguration> {
        new() { DataSourceId = dataSourceId, PluginIds = ["foo", "bar"] },
        new() { DataSourceId = dataSourceId, PluginIds = ["foo", "baz"] },
        new() { DataSourceId = "other", PluginIds = ["quux"] }
    };

    using var factory =
        new CfnWebApplicationFactory(webhookSecret: WebhookSecret, webhookConfigurations: webhookConfigurations);
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();
    var webhookBody = new NotionWebhookBody {
        Type = "data_source.whatever",
        Data = new NotionWebhookData(),
        Entity = new NotionWebhookEntity { Type = "unused", Id = dataSourceId }
    };

    var body = JsonSerializer.Serialize(webhookBody);
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);
    content.Headers.Add("X-Notion-Signature", signature);

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r =>
        r.Message == $"Webhook request: Type={webhookBody.Type}, DataSourceId={dataSourceId}");

    var pluginIds = new List<string?>();
    while (channel.Reader.TryRead(out var runRequest)) {
      pluginIds.Add(runRequest.PluginId);
    }

    pluginIds.ShouldBeEquivalentTo(new List<string> { "foo", "bar", "baz" });
  }

  [TestMethod]
  public async Task PostWebhook_ValidPageChange_SendsPluginIdsInMessagesToChannel() {
    // Arrange
    var dataSourceId = Guid.NewGuid().ToString();
    var webhookConfigurations = new List<WebhookConfiguration> {
        new() { DataSourceId = dataSourceId, PluginIds = ["hello"] },
        new() { DataSourceId = "other", PluginIds = ["world"] }
    };

    using var factory =
        new CfnWebApplicationFactory(webhookSecret: WebhookSecret, webhookConfigurations: webhookConfigurations);
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();
    var webhookBody = new NotionWebhookBody {
        Type = "page.whatever",
        Data = new NotionWebhookData {
            Parent = new NotionWebhookParent { Id = "unused", Type = "unused", DataSourceId = dataSourceId }
        },
        Entity = new NotionWebhookEntity { Id = "unused", Type = "unused" }
    };

    var body = JsonSerializer.Serialize(webhookBody);
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);
    content.Headers.Add("X-Notion-Signature", signature);

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r =>
        r.Message == $"Webhook request: Type={webhookBody.Type}, DataSourceId={dataSourceId}");
    logSnapshot.ShouldContain(r =>
        r.Message == "Plugin IDs in webhook request: hello");

    var pluginIds = new List<string?>();
    while (channel.Reader.TryRead(out var runRequest)) {
      pluginIds.Add(runRequest.PluginId);
    }

    pluginIds.ShouldHaveSingleItem().ShouldBe("hello");
  }

  [TestMethod]
  public async Task PostWebhook_UnexpectedEntityType_ReturnsBadRequest() {
    // Arrange
    using var factory =
        new CfnWebApplicationFactory(webhookSecret: WebhookSecret, webhookConfigurations: []);
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();
    var webhookBody = new NotionWebhookBody {
        Type = "unexpected.whatever",
        Data = new NotionWebhookData(),
        Entity = new NotionWebhookEntity { Id = "unused", Type = "unused" }
    };

    var body = JsonSerializer.Serialize(webhookBody);
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);
    content.Headers.Add("X-Notion-Signature", signature);

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r =>
        r.Message == "Unexpected entity type unexpected for webhook request of type unexpected.whatever");
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  [TestMethod]
  public async Task PostWebhook_UnreadableDataSourceId_ReturnsOkWithoutSendingMessagesToChannel() {
    // Arrange
    using var factory =
        new CfnWebApplicationFactory(webhookSecret: WebhookSecret, webhookConfigurations: []);
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();
    var webhookBody = new NotionWebhookBody {
        Type = "page.whatever",
        Data = new NotionWebhookData() { Parent = new NotionWebhookParent() {
            Id = "unused",
            Type = "unused",
            DataSourceId = null
        }},
        Entity = new NotionWebhookEntity { Type = "unused", Id = "unused" }
    };

    var body = JsonSerializer.Serialize(webhookBody);
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);
    content.Headers.Add("X-Notion-Signature", signature);

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r =>
        r.Message == $"Webhook request: Type={webhookBody.Type}, DataSourceId=(null)");
    logSnapshot.ShouldContain(r =>
        r.Message == "Webhook not associated with data source, ignoring");
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  [TestMethod]
  public async Task PostWebhook_NullBody_ReturnsBadRequest() {
    // Arrange
    using var factory =
        new CfnWebApplicationFactory(webhookSecret: WebhookSecret, webhookConfigurations: []);
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    const string body = "null";
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);
    content.Headers.Add("X-Notion-Signature", signature);

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r => r.Message == "Body is 'null'");
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  [TestMethod]
  public async Task PostWebhook_BodyFailedToDeserialize_ReturnsBadRequest() {
    // Arrange
    using var factory =
        new CfnWebApplicationFactory(webhookSecret: WebhookSecret, webhookConfigurations: []);
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    const string body = "invalid json";
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);
    content.Headers.Add("X-Notion-Signature", signature);

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r => r.Message == "Failed to deserialize body" && r.Exception != null);
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  [TestMethod]
  public async Task PostWebhook_TraceLoggingDisabled_DoesNotLogBody() {
    // Arrange
    using var factory =
        new CfnWebApplicationFactory(webhookSecret: WebhookSecret, webhookConfigurations: [], traceLoggingEnabled: false);
    var client = factory.CreateClient();
    var channel = factory.Services.GetRequiredService<Channel<RunRequest>>();
    var logCollector = factory.Services.GetFakeLogCollector();
    channel.Reader.TryPeek(out _).ShouldBeFalse();

    const string body = "invalid json";
    var content = new StringContent(body, Encoding.UTF8, "application/json");
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);
    content.Headers.Add("X-Notion-Signature", signature);

    // Act
    var response = await client.PostAsync("webhook", content, TestContext.CancellationToken)
        .ConfigureAwait(false);

    // Assert
    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    var logSnapshot = logCollector.GetSnapshot();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldNotContain(r => r.Message.StartsWith("Received request body:"));
    logSnapshot.ShouldContain(r => r.Message == "Failed to deserialize body" && r.Exception != null);
    channel.Reader.TryPeek(out _).ShouldBeFalse();
  }

  public TestContext TestContext { get; set; }

  private sealed class UnknownLengthContent(string body) : HttpContent {
    private readonly byte[] _body = Encoding.UTF8.GetBytes(body);

    protected override bool TryComputeLength(out long length) {
      length = 0;
      return false;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) {
      await stream.WriteAsync(_body).ConfigureAwait(false);
    }
  }
}
