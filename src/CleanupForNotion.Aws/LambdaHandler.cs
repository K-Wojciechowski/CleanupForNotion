using System.Diagnostics.CodeAnalysis;
using Amazon.Lambda.Core;
using Amazon.S3;
using CleanupForNotion.Core.Infrastructure.Loop;
using Microsoft.Extensions.DependencyInjection;

namespace CleanupForNotion.Aws;

[ExcludeFromCodeCoverage(Justification = "Trivial code that cannot be tested without AWS credentials")]
// ReSharper disable once UnusedType.Global
public class LambdaHandler {
  // ReSharper disable once UnusedMember.Global
#pragma warning disable CA1822 // method could be static
  public async Task Handle(ILambdaContext context) {
    using var cancellationTokenSource = new CancellationTokenSource();
    cancellationTokenSource.CancelAfter(context.RemainingTime);
    using var amazonS3Client = new AmazonS3Client();
    var host = LambdaHostBuilder.BuildHost(context, amazonS3Client);
    await host.Services.GetRequiredService<OneShotLoop>().ExecuteAsync(cancellationTokenSource.Token).ConfigureAwait(false);
  }
#pragma warning restore CA1822
}
