#pragma warning disable CA1822 // method could be static

using System.Diagnostics.CodeAnalysis;
using System.Net;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using CleanupForNotion.Aws.DynamoDb;
using CleanupForNotion.Core.Infrastructure.Loop;
using CleanupForNotion.Core.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Aws;

[ExcludeFromCodeCoverage(Justification = "Trivial code that cannot be tested without AWS credentials")]
// ReSharper disable once UnusedType.Global
public class LambdaHandler {
  // ReSharper disable once UnusedMember.Global
  public async Task<APIGatewayHttpApiV2ProxyResponse> Handle(
      APIGatewayHttpApiV2ProxyRequest request,
      ILambdaContext context) {
    using var cancellationTokenSource = new CancellationTokenSource();
    var cancellationToken = cancellationTokenSource.Token;
    cancellationTokenSource.CancelAfter(context.RemainingTime);
    using var amazonS3Client = new AmazonS3Client();
    var host = LambdaHostBuilder.BuildHost(context, amazonS3Client);

    RunRequest runRequest;

    // This is a slightly hacky way to have the same Lambda function handle webhooks and full cleanup runs.
    // APIGatewayHttpApiV2ProxyRequest.Version is always "2.0" for incoming requests.
    // EventBridge and the invoke scripts pass a payload with "cfn-schedule" and "cfn-script" versions respectively.
    // If someone makes a request via the "Test" tab in the AWS console, and they keep the "hello world" payload, version is null.
    if (request.Version == null || request.Version.StartsWith("cfn")) {
      context.Logger.LogTrace($"Scheduled/manual full run mode - source: {request.Version ?? "(null)"}");
      runRequest = new RunRequest(HttpStatusCode.OK, [null]);
    } else {
      context.Logger.LogTrace($"HTTP mode - version: {request.Version}");
      runRequest = await HandleWebhook(request, host, cancellationToken).ConfigureAwait(false);
    }

    if (runRequest.PluginIds.Count == 0) {
      return new APIGatewayHttpApiV2ProxyResponse { StatusCode = (int)runRequest.StatusCode };
    }

    var oneShotLoop = host.Services.GetRequiredService<OneShotLoop>();
    var lockManager = host.Services.GetRequiredService<DynamoDbLockManager>();

    var lockToken = await lockManager.AcquireLock(cancellationToken).ConfigureAwait(false);
    try {
      foreach (var pluginId in runRequest.PluginIds) {
        await oneShotLoop.ExecuteAsync(pluginId, cancellationToken).ConfigureAwait(false);
      }
    } finally {
      await lockManager.ReleaseLock(lockToken).ConfigureAwait(false);
    }

    return new APIGatewayHttpApiV2ProxyResponse { StatusCode = (int)runRequest.StatusCode };
  }

  private static async Task<RunRequest> HandleWebhook(
      APIGatewayHttpApiV2ProxyRequest request,
      IHost host,
      CancellationToken cancellationToken) {
    var logger = host.Services.GetRequiredService<ILogger<LambdaHandler>>();

    if (request.RequestContext.Http.Method != "POST") {
      logger.LogTrace("Ignoring non-POST request");
      return new RunRequest(HttpStatusCode.BadRequest, []);
    }

    var webhookRequest = new LambdaWebhookRequest(request);
    var webhookProcessor = host.Services.GetRequiredService<IWebhookProcessor>();
    var webhookResult = await webhookProcessor.ProcessWebhookAsync(webhookRequest, cancellationToken)
        .ConfigureAwait(false);
    logger.LogInformation("Webhook status code: {StatusCode}", webhookResult.StatusCode);

    return new RunRequest(webhookResult.StatusCode, webhookResult.PluginIds);
  }

  private record RunRequest(HttpStatusCode StatusCode, IReadOnlyCollection<string?> PluginIds);
}
