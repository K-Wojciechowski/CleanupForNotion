using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using CleanupForNotion.Core;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Execution;
using CleanupForNotion.Core.Infrastructure.Loop;
using CleanupForNotion.Core.Webhooks;
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
        .AddSingleton<IWebhookProcessor, WebhookProcessor>()
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
        IWebhookProcessor webhookProcessor,
        Channel<RunRequest> channel,
        TimeProvider timeProvider,
        HttpContext httpContext,
        CancellationToken cancellationToken) => {
      var webhookRequest = new HttpContextWebhookRequest(httpContext);
      var result = await webhookProcessor.ProcessWebhookAsync(webhookRequest, cancellationToken).ConfigureAwait(false);

      foreach (var pluginId in result.PluginIds) {
        await channel.Writer.WriteAsync(new RunRequest(timeProvider.GetUtcNow(), pluginId), cancellationToken)
            .ConfigureAwait(false);
      }

      return Results.StatusCode((int)result.StatusCode);
    });

    app.Run();
  }
}
