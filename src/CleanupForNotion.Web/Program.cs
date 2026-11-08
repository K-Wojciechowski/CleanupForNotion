using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CleanupForNotion.Core;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.Loop;
using CleanupForNotion.Web.NotionModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Notion.Client;

namespace CleanupForNotion.Web;

public class Program {
  public static void Main(string[] args) {
    var builder = WebApplication.CreateBuilder(args);

    builder.Services
        .AddCfnServices()
        .AddSingleton(TimeProvider.System)
        .AddHostedService<ChannelTimerBackgroundService>()
        .AddHostedService<ChannelBasedLoop>()
        .AddLogging(loggingBuilder => loggingBuilder.AddSimpleConsole(options => {
          options.IncludeScopes = true;
          options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        }))
        .Configure<CfnOptions>(builder.Configuration.GetSection("CleanupForNotion"))
        .AddSingleton<Channel<RunRequest>>(_ =>
            Channel.CreateUnbounded<RunRequest>(
                new UnboundedChannelOptions {
                    SingleWriter = false, SingleReader = true, AllowSynchronousContinuations = false
                }));

    var app = builder.Build();

    var safeUtf8Encoding = new UTF8Encoding(false, false);

    app.MapPost("/",
        async (Channel<RunRequest> channel, TimeProvider timeProvider, CancellationToken cancellationToken) => {
          await channel.Writer.WriteAsync(new RunRequest(timeProvider.GetUtcNow()), cancellationToken)
              .ConfigureAwait(false);
          return "Triggered";
        });

    app.MapPost("/plugin/{pluginId}", async (
        [FromRoute] string pluginId,
        Channel<RunRequest> channel,
        TimeProvider timeProvider,
        CancellationToken cancellationToken) => {
      await channel.Writer.WriteAsync(new RunRequest(timeProvider.GetUtcNow(), pluginId), cancellationToken)
          .ConfigureAwait(false);

      return "Triggered plugin";
    });

    app.MapPost("/webhook", async (
        ILogger<Program> logger,
        Channel<RunRequest> channel,
        TimeProvider timeProvider,
        IOptions<CfnOptions> options,
        HttpContext httpContext,
        CancellationToken cancellationToken) => {
      var webhookSecret = options.Value.WebhookSecret;

      var contentLength = httpContext.Request.ContentLength;

      if (contentLength == null || (webhookSecret == null && contentLength > 1024)) {
        logger.LogTrace("Webhook request rejected due to invalid content length");
        return Results.BadRequest();
      }

      var hasSignature = httpContext.Request.Headers.TryGetValue("X-Notion-Signature", out var signatureHeaders);
      var signatureHeader = hasSignature ? signatureHeaders.ToString() : string.Empty;

      if (webhookSecret != null && string.IsNullOrEmpty(signatureHeader)) {
        logger.LogTrace("Received unsigned webhook request");
        return Results.BadRequest();
      }

      var bodyBytes = new byte[contentLength.Value];
      _ = await httpContext.Request.Body.ReadAsync(bodyBytes, cancellationToken).ConfigureAwait(false);

      if (webhookSecret == null) {
        var bodyString = safeUtf8Encoding.GetString(bodyBytes);
        logger.LogInformation("Received new webhook request with body {Body}", bodyString.Replace("\n", "").Trim());
        return Results.Ok();
      }

      if (options.Value.Webhooks is null) {
        logger.LogError("Webhooks not configured");
        return Results.Ok();
      }

      var signatureValid = await NotionWebhookSignature.VerifySignatureAsync(bodyBytes, signatureHeader, webhookSecret)
          .ConfigureAwait(false);

      if (!signatureValid) {
        logger.LogError("Received webhook request with invalid signature");
        return Results.BadRequest();
      }

      logger.LogInformation("Received valid webhook request");

      if (logger.IsEnabled(LogLevel.Trace)) {
        var bodyString = safeUtf8Encoding.GetString(bodyBytes);
        logger.LogTrace("Received request body: {Body}", bodyString);
      }

      NotionWebhookBody? body;
      try {
        body = JsonSerializer.Deserialize<NotionWebhookBody>(bodyBytes);
      } catch (JsonException exc) {
        logger.LogError(exc, "Failed to deserialize body");
        return Results.BadRequest();
      }

      if (body is null) {
        logger.LogError("Body is 'null'");
        return Results.BadRequest();
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
          return Results.BadRequest();
      }

      logger.LogInformation("Webhook request: Type={Type}, DataSourceId={DataSourceId}", body.Type, dataSourceId);

      if (dataSourceId == null) {
        logger.LogDebug("Webhook not associated with data source, ignoring");
        return Results.Ok();
      }

      var pluginIds = options.Value.Webhooks
          .Where(e => e.DataSourceId == dataSourceId)
          .SelectMany(w => w.PluginIds)
          .Distinct()
          .ToList();

      logger.LogInformation("Plugin IDs in webhook request: {PluginIds}", string.Join(", ", pluginIds));

      foreach (var pluginId in pluginIds) {
        await channel.Writer.WriteAsync(new RunRequest(timeProvider.GetUtcNow(), pluginId), cancellationToken)
            .ConfigureAwait(false);
      }

      return Results.Ok();
    });

    app.Run();
  }
}
