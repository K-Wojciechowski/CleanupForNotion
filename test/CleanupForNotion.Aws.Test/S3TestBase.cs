using Amazon.S3;
using Amazon.S3.Model;
using CleanupForNotion.Aws.S3;
using Shouldly;

namespace CleanupForNotion.Aws.Test;

public static class S3TestBase {
  public const string AppsettingsJson =
      """
      {
        "CleanupForNotion": {
          "AuthToken": "ntn_TEST",
          "DryRun": true,
          "Plugins": [
            {
              "PluginName": "DeleteOnNewMonthlyCycle",
              "PluginDescription": "Secondary Bank Card Fee cycle enforcement",
              "DatabaseId": "00000000000000000000000000000000",
              "PropertyName": "Date",
              "TimeZoneName": "Europe/Warsaw",
              "CycleResetDay": 10
            },
            {
              "PluginName": "DeleteByCheckbox",
              "PluginDescription": "Delete checked items",
              "DatabaseId": "11111111111111111111111111111111",
              "PropertyName": "Delete"
            }
          ]
        }
      }
      """;

  public static readonly Dictionary<string, string> ExpectedOptions = new Dictionary<string, string>() {
      { "CleanupForNotion:AuthToken", "ntn_TEST" },
      { "CleanupForNotion:DryRun", "True" },
      { "CleanupForNotion:Plugins:0:PluginName", "DeleteOnNewMonthlyCycle" },
      { "CleanupForNotion:Plugins:0:PluginDescription", "Secondary Bank Card Fee cycle enforcement" },
      { "CleanupForNotion:Plugins:0:DatabaseId", "00000000000000000000000000000000" },
      { "CleanupForNotion:Plugins:0:PropertyName", "Date" },
      { "CleanupForNotion:Plugins:0:TimeZoneName", "Europe/Warsaw" },
      { "CleanupForNotion:Plugins:0:CycleResetDay", "10" },
      { "CleanupForNotion:Plugins:1:PluginName", "DeleteByCheckbox" },
      { "CleanupForNotion:Plugins:1:PluginDescription", "Delete checked items" },
      { "CleanupForNotion:Plugins:1:DatabaseId", "11111111111111111111111111111111" },
      { "CleanupForNotion:Plugins:1:PropertyName", "Delete" }
  };

  public static async Task TestS3Configuration(IAmazonS3 s3Client) {
    var s3BucketName = $"cfn-test-{Guid.NewGuid()}";
    var s3Key = Guid.NewGuid().ToString();

    var bucketCreated = false;
    var objectCreated = false;

    try {
      await s3Client.PutBucketAsync(s3BucketName).ConfigureAwait(false);
      bucketCreated = true;
      await s3Client
          .PutObjectAsync(new PutObjectRequest() {
              BucketName = s3BucketName, Key = s3Key, ContentBody = AppsettingsJson
          }).ConfigureAwait(false);
      objectCreated = true;

      var s3ConfigurationSource = new S3ConfigurationSource(s3BucketName, s3Key, s3Client);
      var s3ConfigurationBuilder = s3ConfigurationSource.Build(null!);
      s3ConfigurationBuilder.Load();

      foreach (var (configKey, expectedValue) in ExpectedOptions) {
        s3ConfigurationBuilder.TryGet(configKey, out var actualValue).ShouldBeTrue();
        actualValue.ShouldBe(expectedValue);
      }
    } finally {
      if (objectCreated) await s3Client.DeleteObjectAsync(s3BucketName, s3Key).ConfigureAwait(false);
      if (bucketCreated) await s3Client.DeleteBucketAsync(s3BucketName).ConfigureAwait(false);
    }
  }
}
