using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.PluginManagement;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.PluginManagement;

[TestClass]
public class GlobalOptionsProviderTests {
  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public void GlobalOptions_NullRunFrequency_ReturnsGlobalOptionsWithNullRunFrequency(bool dryRun) {
    // Arrange
    var cfnOptions = new CfnOptions { AuthToken = string.Empty, Plugins = [], DryRun = dryRun, RunFrequency = null };
    var optionsWrapper = new OptionsWrapper<CfnOptions>(cfnOptions);
    var globalOptionsProvider = new GlobalOptionsProvider(optionsWrapper);

    // Act
    var globalOptions = globalOptionsProvider.GlobalOptions;

    // Assert
    globalOptions.DryRun.ShouldBe(dryRun);
    globalOptions.RunFrequency.ShouldBeNull();
  }

  [TestMethod]
  [DataRow(true)]
  [DataRow(false)]
  public void GlobalOptions_ProvidedRunFrequency_ReturnsGlobalOptionsWithRunFrequency(bool dryRun) {
    // Arrange
    const string runFrequencyString = "12:34:56";
    var expectedRunFrequency = TimeSpan.FromHours(12).Add(TimeSpan.FromMinutes(34)).Add(TimeSpan.FromSeconds(56));
    var cfnOptions = new CfnOptions { AuthToken = string.Empty, Plugins = [], DryRun = dryRun, RunFrequency = runFrequencyString };
    var optionsWrapper = new OptionsWrapper<CfnOptions>(cfnOptions);
    var globalOptionsProvider = new GlobalOptionsProvider(optionsWrapper);

    // Act
    var globalOptions = globalOptionsProvider.GlobalOptions;

    // Assert
    globalOptions.DryRun.ShouldBe(dryRun);
    globalOptions.RunFrequency.ShouldBe(expectedRunFrequency);
  }
}
