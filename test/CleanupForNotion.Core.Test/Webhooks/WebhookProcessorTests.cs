using System.Net;
using System.Text;
using System.Text.Json;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Webhooks;
using CleanupForNotion.Core.Webhooks.NotionModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Notion.Client;
using Shouldly;

namespace CleanupForNotion.Core.Test.Webhooks;

[TestClass]
public class WebhookProcessorTests {
  // taken from example payload: https://developers.notion.com/reference/webhooks#step-2-verifying-the-subscription
  private const string WebhookSecret = "sec" + "ret" + "_tMrlL1qK5vuQAh1b6cZGhFChZTSYJlce98V0pYn7yBl";

  [TestMethod]
  public async Task ProcessWebhookAsync_NoSecretConfiguredAndShortRequest_LogsRequestBody() {
    // Arrange
    const string body = $$"""
                          {
                            "verification_token": "{{WebhookSecret}}"
                          }
                          """;

    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(body).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.OK);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r =>
        r.Message == "Received new webhook request with body " + body.Replace("\n", "").Trim());
  }

  [TestMethod]
  public async Task ProcessWebhookAsync_NoSecretConfiguredAndLongRequest_ReturnsBadRequest() {
    // Arrange
    var body = new string('a', 1025);

    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(body).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r => r.Message == "Webhook request rejected due to invalid content length");
  }

  [TestMethod]
  [DataRow("secret", DisplayName = "webhook secret configured")]
  [DataRow(null, DisplayName = "webhook secret not configured")]
  public async Task ProcessWebhookAsync_RequestWithoutContentLength_ReturnsBadRequest(string? webhookSecret) {
    // Arrange
    const string body = "hello";

    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(body, contentLength: null, webhookSecret: webhookSecret)
        .ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r => r.Message == "Webhook request rejected due to invalid content length");
  }

  [TestMethod]
  public async Task
      ProcessWebhookAsync_SecretConfiguredButWebhooksNotConfigured_ReturnsOkWithoutSendingMessagesToChannel() {
    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(
        body: "invalid json which will not be parsed",
        webhookSecret: "secret",
        signatureHeader: "sha256-invalid").ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.OK);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r => r.Message == "Webhooks not configured");
  }

  [TestMethod]
  public async Task ProcessWebhookAsync_MissingSignature_ReturnsBadRequest() {
    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(
        body: "invalid json which will not be parsed",
        webhookSecret: "secret",
        webhookConfigurations: []).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r => r.Message == "Received unsigned webhook request");
  }

  [TestMethod]
  public async Task ProcessWebhookAsync_InvalidSignature_ReturnsBadRequest() {
    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(
        body: "invalid json which will not be parsed",
        signatureHeader: "sha256-invalid",
        webhookSecret: WebhookSecret,
        webhookConfigurations: []).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r => r.Message == "Received webhook request with invalid signature");
  }

  [TestMethod]
  public async Task ProcessWebhookAsync_ValidDataSourceChange_SendsPluginIdsInMessagesToChannel() {
    // Arrange
    var dataSourceId = Guid.NewGuid().ToString();
    var webhookConfigurations = new List<WebhookConfiguration> {
        new() { DataSourceId = dataSourceId, PluginIds = ["foo", "bar"] },
        new() { DataSourceId = dataSourceId, PluginIds = ["foo", "baz"] },
        new() { DataSourceId = "other", PluginIds = ["quux"] }
    };

    var webhookBody = new NotionWebhookBody {
        Type = "data_source.whatever",
        Data = new NotionWebhookData(),
        Entity = new NotionWebhookEntity { Type = "unused", Id = dataSourceId }
    };

    var body = JsonSerializer.Serialize(webhookBody);
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);

    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(
        body: body,
        signatureHeader: signature,
        webhookSecret: WebhookSecret,
        webhookConfigurations: webhookConfigurations).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.OK);
    result.PluginIds.ShouldBeEquivalentTo(new List<string> { "foo", "bar", "baz" });
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r =>
        r.Message == $"Webhook request: Type={webhookBody.Type}, DataSourceId={dataSourceId}");
  }

  [TestMethod]
  public async Task ProcessWebhookAsync_ValidPageChange_SendsPluginIdsInMessagesToChannel() {
    // Arrange
    var dataSourceId = Guid.NewGuid().ToString();
    var webhookConfigurations = new List<WebhookConfiguration> {
        new() { DataSourceId = dataSourceId, PluginIds = ["hello"] },
        new() { DataSourceId = "other", PluginIds = ["world"] }
    };

    var webhookBody = new NotionWebhookBody {
        Type = "page.whatever",
        Data = new NotionWebhookData {
            Parent = new NotionWebhookParent { Id = "unused", Type = "unused", DataSourceId = dataSourceId }
        },
        Entity = new NotionWebhookEntity { Id = "unused", Type = "unused" }
    };

    var body = JsonSerializer.Serialize(webhookBody);
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);

    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(
        body: body,
        signatureHeader: signature,
        webhookSecret: WebhookSecret,
        webhookConfigurations: webhookConfigurations).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.OK);
    result.PluginIds.ShouldHaveSingleItem().ShouldBe("hello");
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r =>
        r.Message == $"Webhook request: Type={webhookBody.Type}, DataSourceId={dataSourceId}");
    logSnapshot.ShouldContain(r =>
        r.Message == "Plugin IDs in webhook request: hello");
  }

  [TestMethod]
  public async Task ProcessWebhookAsync_UnexpectedEntityType_ReturnsBadRequest() {
    // Arrange
    var webhookBody = new NotionWebhookBody {
        Type = "unexpected.whatever",
        Data = new NotionWebhookData(),
        Entity = new NotionWebhookEntity { Id = "unused", Type = "unused" }
    };

    var body = JsonSerializer.Serialize(webhookBody);
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);

    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(
        body: body,
        signatureHeader: signature,
        webhookSecret: WebhookSecret,
        webhookConfigurations: []).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r =>
        r.Message == "Unexpected entity type unexpected for webhook request of type unexpected.whatever");
  }

  [TestMethod]
  [DataRow(true, DisplayName = "data.parent present, data source ID null")]
  [DataRow(false, DisplayName = "data.parent missing")]
  public async Task ProcessWebhookAsync_UnreadableDataSourceId_ReturnsOkWithoutSendingMessagesToChannel(bool parentPresent) {
    // Arrange
    var webhookBody = new NotionWebhookBody {
        Type = "page.whatever",
        Data = new NotionWebhookData() {
            Parent = parentPresent ? new NotionWebhookParent() { Id = "unused", Type = "unused", DataSourceId = null } : null
        },
        Entity = new NotionWebhookEntity { Type = "unused", Id = "unused" }
    };

    var body = JsonSerializer.Serialize(webhookBody);
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);

    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(
        body: body,
        signatureHeader: signature,
        webhookSecret: WebhookSecret,
        webhookConfigurations: []).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.OK);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r =>
        r.Message == $"Webhook request: Type={webhookBody.Type}, DataSourceId=(null)");
    logSnapshot.ShouldContain(r =>
        r.Message == "Webhook not associated with data source, ignoring");
  }

  [TestMethod]
  public async Task ProcessWebhookAsync_NullBody_ReturnsBadRequest() {
    // Arrange
    const string body = "null";
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);

    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(
        body: body,
        signatureHeader: signature,
        webhookSecret: WebhookSecret,
        webhookConfigurations: []).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r => r.Message == "Body is 'null'");
  }

  [TestMethod]
  public async Task ProcessWebhookAsync_BodyFailedToDeserialize_ReturnsBadRequest() {
    // Arrange
    const string body = "invalid json";
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);

    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(
        body: body,
        signatureHeader: signature,
        webhookSecret: WebhookSecret,
        webhookConfigurations: []).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldContain(r => r.Message == "Received request body: " + body);
    logSnapshot.ShouldContain(r => r.Message == "Failed to deserialize body" && r.Exception != null);
  }

  [TestMethod]
  public async Task ProcessWebhookAsync_TraceLoggingDisabled_DoesNotLogBody() {
    // Arrange
    const string body = "invalid json";
    var signature = await NotionWebhookSignature.SignPayloadAsync(body, WebhookSecret).ConfigureAwait(false);

    // Act
    var (result, logSnapshot) = await CallProcessWebhookAsync(
        body: body,
        signatureHeader: signature,
        webhookSecret: WebhookSecret,
        webhookConfigurations: [],
        traceLoggingEnabled: false).ConfigureAwait(false);

    // Assert
    result.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    result.PluginIds.ShouldBeEmpty();
    logSnapshot.ShouldContain(r => r.Message == "Received valid webhook request");
    logSnapshot.ShouldNotContain(r => r.Message.StartsWith("Received request body:"));
    logSnapshot.ShouldContain(r => r.Message == "Failed to deserialize body" && r.Exception != null);
  }

  public TestContext TestContext { get; set; }

  private async Task<(WebhookResult, IReadOnlyList<FakeLogRecord>)> CallProcessWebhookAsync(
      string body,
      string? signatureHeader = null,
      long? contentLength = -1,
      string? webhookSecret = null,
      List<WebhookConfiguration>? webhookConfigurations = null,
      bool traceLoggingEnabled = true) {
    var options = new CfnOptions {
        AuthToken = "", Plugins = [], WebhookSecret = webhookSecret, Webhooks = webhookConfigurations
    };

    var logCollector = new FakeLogCollector(new OptionsWrapper<FakeLogCollectorOptions>(new FakeLogCollectorOptions() {
        FilteredLevels = Enum.GetValues<LogLevel>().Where(l => traceLoggingEnabled || l != LogLevel.Trace).ToHashSet()
    }));
    var logger = new FakeLogger<WebhookProcessor>(logCollector);

    var processor = new WebhookProcessor(logger, new OptionsWrapper<CfnOptions>(options));

    var bodyBytes = Encoding.UTF8.GetBytes(body);
    var request = new FakeWebhookRequest(
        BodyBytes: bodyBytes,
        ContentLength: contentLength == -1 ? bodyBytes.Length : contentLength,
        SignatureHeader: signatureHeader);

    var result = await processor.ProcessWebhookAsync(request, TestContext.CancellationToken).ConfigureAwait(false);

    return (result, logCollector.GetSnapshot());
  }

  private record FakeWebhookRequest(byte[] BodyBytes, long? ContentLength, string? SignatureHeader) : IWebhookRequest {
    public Task<byte[]> ReadBodyAsync(CancellationToken cancellationToken) => Task.FromResult(BodyBytes);
  }
}
