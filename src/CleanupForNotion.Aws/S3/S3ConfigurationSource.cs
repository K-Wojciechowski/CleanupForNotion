using Amazon.S3;
using Microsoft.Extensions.Configuration;

namespace CleanupForNotion.Aws.S3;

public class S3ConfigurationSource(string s3Bucket, string s3Key, IAmazonS3 s3Client) : IConfigurationSource {
  public IConfigurationProvider Build(IConfigurationBuilder builder) => new S3ConfigurationBuilder(s3Bucket, s3Key, s3Client);
}
