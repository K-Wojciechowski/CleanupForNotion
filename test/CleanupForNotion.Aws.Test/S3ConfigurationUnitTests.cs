using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;

namespace CleanupForNotion.Aws.Test;

[TestClass]
public class S3ConfigurationUnitTests {
  [TestMethod]
  public async Task TestS3Configuration() {
    var s3Client = Substitute.For<IAmazonS3>();

    string? bucketName = null;
    string? key = null;
    string? json = null;

    s3Client
        .When(c => c.PutBucketAsync(bucketName: Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>()))
        .Do(callInfo => bucketName = callInfo.Arg<string>());

    s3Client
        .When(c => c.PutObjectAsync(Arg.Any<PutObjectRequest>(), cancellationToken: Arg.Any<CancellationToken>()))
        .Do(callInfo => {
          var request = callInfo.Arg<PutObjectRequest>();
          key = request.Key;
          json = request.ContentBody;
        });

    s3Client.GetObjectAsync(bucketName: Arg.Is<string>(s => s == bucketName), key: Arg.Is<string>(s => s == key),
            Arg.Any<CancellationToken>())
        .Returns(_ => new GetObjectResponse() { ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(json!)) });

    await S3TestBase.TestS3Configuration(s3Client).ConfigureAwait(false);

    await s3Client.ReceivedWithAnyArgs().GetObjectAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ConfigureAwait(false);
  }

}
