using Amazon.Lambda.APIGatewayEvents;
using CleanupForNotion.Core;
using Shouldly;

namespace CleanupForNotion.Aws.Test;

[TestClass]
public class LambdaWebhookRequestTests {
  [TestMethod]
  public void BodyBytes_NotBase64Encoded_EncodesAsUtf8() {
    // Arrange
    const string body = "hello ąćęłńóśźż";
    byte[] byteBody = "hello ąćęłńóśźż"u8.ToArray();
    var awsRequest = new APIGatewayHttpApiV2ProxyRequest { IsBase64Encoded = false, Body = body };
    var request = new LambdaWebhookRequest(awsRequest);

    // Act & Assert
    var readBytes = request.ReadBodyAsync(CancellationToken.None).Result;
    readBytes.ShouldBeEquivalentTo(byteBody);
    request.ContentLength.ShouldBe(byteBody.Length);
  }

  [TestMethod]
  public void BodyBytes_Base64Encoded_DecodesBase64() {
    // Arrange
    byte[] byteBody = "hello ąćęłńóśźż"u8.ToArray();
    var base64Body = Convert.ToBase64String(byteBody);
    var awsRequest = new APIGatewayHttpApiV2ProxyRequest { IsBase64Encoded = true, Body = base64Body };
    var request = new LambdaWebhookRequest(awsRequest);

    // Act & Assert
    var readBytes = request.ReadBodyAsync(CancellationToken.None).Result;
    readBytes.ShouldBeEquivalentTo(byteBody);
    request.ContentLength.ShouldBe(byteBody.Length);
  }

  [TestMethod]
  public void SignatureHeader_Missing_ReturnsNull() {
    // Arrange
    var awsRequest = new APIGatewayHttpApiV2ProxyRequest { Body = "", Headers = new Dictionary<string, string>() };
    var request = new LambdaWebhookRequest(awsRequest);

    // Act & Assert
    request.SignatureHeader.ShouldBeNull();
  }

  [TestMethod]
  public void SignatureHeader_Present_ReturnsIt() {
    // Arrange
    var headerValue = Guid.NewGuid().ToString();
    var awsRequest = new APIGatewayHttpApiV2ProxyRequest {
        Body = "",
        Headers = new Dictionary<string, string> { { Constants.WebhookSignatureHeaderName.ToLower(), headerValue } }
    };
    var request = new LambdaWebhookRequest(awsRequest);

    // Act & Assert
    request.SignatureHeader.ShouldBe(headerValue);
  }
}
