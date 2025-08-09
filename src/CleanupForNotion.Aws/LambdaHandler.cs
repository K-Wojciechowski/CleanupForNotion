using System.Diagnostics.CodeAnalysis;
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using Amazon.Lambda.Core;
using Amazon.S3;
using CleanupForNotion.Aws.DynamoDb;
using CleanupForNotion.Aws.S3;
using CleanupForNotion.Core;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CleanupForNotion.Aws;


public class LambdaHandler {
  [ExcludeFromCodeCoverage(Justification = "Trivial code that cannot be tested without AWS credentials")]
  // ReSharper disable once UnusedMember.Global
#pragma warning disable CA1822
  public async Task Handle(ILambdaContext context) {
    using var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.CancelAfter(context.RemainingTime);
    using var amazonS3Client = new AmazonS3Client();
    var host = BuildHost(context, amazonS3Client);
    await host.RunAsync(cancellationTokenSource.Token).ConfigureAwait(false);
  }
#pragma warning restore CA1822

  internal static IHost BuildHost(ILambdaContext context, IAmazonS3 amazonS3Client) {
    var loggerOptions = new LambdaLoggerOptions {
        IncludeCategory = true,
        IncludeLogLevel = true,
        IncludeNewline = true,
        IncludeException = true,
        IncludeEventId = true,
        IncludeScopes = true,
    };

    var s3Bucket = GetEnvironmentVariable("CFN_S3_BUCKET", context);
    var s3Key = GetEnvironmentVariable("CFN_S3_KEY", context);
    var dynamoDbTableName = GetEnvironmentVariable("CFN_DYNAMODB_TABLE_NAME", context);

    var builder = Host.CreateApplicationBuilder();

    ((IConfigurationBuilder)(builder.Configuration)).Add(new S3ConfigurationSource(s3Bucket, s3Key, amazonS3Client));

    builder.Services
        .AddCfnServices()
        .AddSingleton(TimeProvider.System)
        .AddLogging(options => options.ClearProviders().AddLambdaLogger(loggerOptions))
        .AddDefaultAWSOptions(new AWSOptions())
        .AddAWSService<IAmazonDynamoDB>()
        .AddHostedService<LambdaHostedServiceWrapper>()
        .Configure<CfnOptions>(builder.Configuration.GetSection("CleanupForNotion"))
        .Configure<DynamoDbOptions>(o => o.TableName = dynamoDbTableName);


    var existingPluginStateProvider = builder.Services.FirstOrDefault(s => s.ServiceType == typeof(IPluginStateProvider));
    if (existingPluginStateProvider != null) {
      builder.Services.Remove(existingPluginStateProvider);
    }

    builder.Services.AddSingleton<IPluginStateProvider, DynamoDbPluginStateProvider>();

    return builder.Build();
  }

  private static string GetEnvironmentVariable(string name, ILambdaContext context) {
    var value = Environment.GetEnvironmentVariable(name);
    if (!string.IsNullOrEmpty(value)) {
      return value;
    }

    context.Logger.LogCritical($"Required environment variable '{name}' was not found.");
    throw new InvalidConfigurationException($"Required environment variable '{name}' was not found.");
  }
}
