using System.Net;
using System.Text;
using System.Text.Json;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Webhooks.NotionModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notion.Client;

namespace CleanupForNotion.Core.Webhooks;

public class WebhookProcessor(
    ILogger<WebhookProcessor> logger,
    IOptions<CfnOptions> options) : IWebhookProcessor {
  private static readonly UTF8Encoding _safeUtf8Encoding = new UTF8Encoding(false, false);

  public async Task<WebhookResult> ProcessWebhookAsync(IWebhookRequest request, CancellationToken cancellationToken) {
      var webhookSecret = options.Value.WebhookSecret;

      var contentLength = request.ContentLength;
      var signatureHeader = request.SignatureHeader;

      if (contentLength == null || (webhookSecret == null && contentLength > 1024)) {
        logger.LogTrace("Webhook request rejected due to invalid content length");
        return WebhookResult.Failure(HttpStatusCode.BadRequest);
      }

      if (webhookSecret != null && string.IsNullOrEmpty(signatureHeader)) {
        logger.LogTrace("Received unsigned webhook request");
        return WebhookResult.Failure(HttpStatusCode.BadRequest);
      }

      var bodyBytes = await request.ReadBodyAsync(cancellationToken).ConfigureAwait(false);

      if (webhookSecret == null) {
        var bodyString = _safeUtf8Encoding.GetString(bodyBytes);
        logger.LogInformation("Received new webhook request with body {Body}", bodyString.Replace("\n", "").Trim());
        return WebhookResult.Ok();
      }

      if (options.Value.Webhooks is null) {
        logger.LogError("Webhooks not configured");
        return WebhookResult.Ok();
      }

      var signatureValid = await NotionWebhookSignature.VerifySignatureAsync(bodyBytes, signatureHeader!, webhookSecret)
          .ConfigureAwait(false);

      if (!signatureValid) {
        logger.LogError("Received webhook request with invalid signature");
        return WebhookResult.Failure(HttpStatusCode.BadRequest);
      }

      logger.LogInformation("Received valid webhook request");

      if (logger.IsEnabled(LogLevel.Trace)) {
        var bodyString = _safeUtf8Encoding.GetString(bodyBytes);
        logger.LogTrace("Received request body: {Body}", bodyString);
      }

      NotionWebhookBody? body;
      try {
        body = JsonSerializer.Deserialize<NotionWebhookBody>(bodyBytes);
      } catch (JsonException exc) {
        logger.LogError(exc, "Failed to deserialize body");
        return WebhookResult.Failure(HttpStatusCode.BadRequest);
      }

      if (body is null) {
        logger.LogError("Body is 'null'");
        return WebhookResult.Failure(HttpStatusCode.BadRequest);
      }

      var eventEntityType = body.Type.Split(".")[0];

      string? dataSourceId;

      switch (eventEntityType) {
        case "data_source":
          dataSourceId = body.Entity.Id;
          break;
        case "page":
          dataSourceId = body.Data.Parent.DataSourceId;
          break;
        default:
          logger.LogError("Unexpected entity type {EntityType} for webhook request of type {Type}", eventEntityType,
              body.Type);
          return WebhookResult.Failure(HttpStatusCode.BadRequest);
      }

      logger.LogInformation("Webhook request: Type={Type}, DataSourceId={DataSourceId}", body.Type, dataSourceId);

      if (dataSourceId == null) {
        logger.LogDebug("Webhook not associated with data source, ignoring");
        return WebhookResult.Ok();
      }

      var pluginIds = options.Value.Webhooks
          .Where(e => e.DataSourceId == dataSourceId)
          .SelectMany(w => w.PluginIds)
          .Distinct()
          .ToList();

      logger.LogInformation("Plugin IDs in webhook request: {PluginIds}", string.Join(", ", pluginIds));

      return WebhookResult.Ok(pluginIds);
  }

}
