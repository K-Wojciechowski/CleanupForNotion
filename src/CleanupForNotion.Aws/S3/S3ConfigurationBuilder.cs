using Amazon.S3;
using Microsoft.Extensions.Configuration.Json;

namespace CleanupForNotion.Aws.S3;

public class S3ConfigurationBuilder(string s3Bucket, string s3Key, IAmazonS3 s3Client)
    : JsonConfigurationProvider(new JsonConfigurationSource()) {
  public override void Load() {
    using var objectResponse = s3Client.GetObjectAsync(s3Bucket, s3Key).Result;
    base.Load(objectResponse.ResponseStream);
  }
}
