using System.Text;
using Amazon.Lambda.TestUtilities;
using Amazon.S3;
using Amazon.S3.Model;
using CleanupForNotion.Aws.DynamoDb;
using CleanupForNotion.Aws.Test.Utils;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Aws.Test;

[TestClass]
[DoNotParallelize] // Tests change environment variables, which may break other tests
public class LambdaHandlerTests {
  [TestMethod]
  public void BuildHost_EnvironmentVariablesSet_BuildsHostWithServicesOptionsAndRedirectedLogging() {
    // Arrange
    var s3BucketName = Guid.NewGuid().ToString();
    var s3Key = Guid.NewGuid().ToString();
    var dynamoDbTableName = Guid.NewGuid().ToString();
    Environment.SetEnvironmentVariable("CFN_S3_BUCKET", s3BucketName);
    Environment.SetEnvironmentVariable("CFN_S3_KEY", s3Key);
    Environment.SetEnvironmentVariable("CFN_DYNAMODB_TABLE_NAME", dynamoDbTableName);

    var s3Client = Substitute.For<IAmazonS3>();
    s3Client.GetObjectAsync(bucketName: s3BucketName, key: s3Key, Arg.Any<CancellationToken>())
        .Returns(_ => new GetObjectResponse() {
            ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(S3TestBase.AppsettingsJson))
        });

    var testContext = new TestLambdaContext();

    // Act
    using var _1 = new TempEnvironmentVariable("AWS_ACCESS_KEY_ID", "X");
    using var _2 = new TempEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "X");
    using var _3 = new TempEnvironmentVariable("AWS_SESSION_TOKEN", "X");
    var host = LambdaHandler.BuildHost(testContext, s3Client);

    // Assert
    host.Services.GetServices<IPluginStateProvider>().ShouldHaveSingleItem()
        .ShouldBeOfType<DynamoDbPluginStateProvider>();
    host.Services.GetService<IPluginStateProvider>().ShouldNotBeNull().ShouldBeOfType<DynamoDbPluginStateProvider>();
    host.Services.GetService<IHostedService>().ShouldNotBeNull().ShouldBeOfType<LambdaHostedServiceWrapper>();
    host.Services.GetService<IOptions<CfnOptions>>().ShouldNotBeNull().Value.AuthToken
        .ShouldBe(S3TestBase.ExpectedOptions["CleanupForNotion:AuthToken"]);
    host.Services.GetService<IOptions<DynamoDbOptions>>().ShouldNotBeNull().Value.TableName.ShouldBe(dynamoDbTableName);
    host.Services.GetServices<ILoggerProvider>().ShouldHaveSingleItem()
        .GetType().Name.ShouldBe("LambdaILoggerProvider");
  }

  [TestMethod]
  public void BuildHost_EnvironmentVariablesNotSet_Throws() {
    // Arrange
    var s3Client = Substitute.For<IAmazonS3>();
    var testContext = new TestLambdaContext();
    const string expectedMessage = "Required environment variable 'CFN_S3_BUCKET' was not found.";

    // Act
    using var _1 = new TempEnvironmentVariable("CFN_S3_BUCKET", null);
    using var _2 = new TempEnvironmentVariable("CFN_S3_KEY", null);
    using var _3 = new TempEnvironmentVariable("CFN_DYNAMODB_TABLE_NAME", null);

    Action act = () => LambdaHandler.BuildHost(testContext, s3Client);

    // Assert
    act.ShouldThrow<InvalidConfigurationException>().Message.ShouldBe(expectedMessage);
    testContext.Logger.ShouldBeOfType<TestLambdaLogger>().Buffer.ToString().ShouldContain(expectedMessage);
  }

  /*
  // for manual testing
  [TestMethod]
  public async Task RunLambdaHandler() {
    var testContext = new TestLambdaContext();

    Environment.SetEnvironmentVariable("CFN_S3_BUCKET", "cfn-test");
    Environment.SetEnvironmentVariable("CFN_S3_KEY", "cfn-appsettings.json");
    Environment.SetEnvironmentVariable("CFN_DYNAMODB_TABLE_NAME", "CfnTest");

    var handler = new LambdaHandler();
    await handler.Handle(testContext).ConfigureAwait(false);
  }
  */
}
