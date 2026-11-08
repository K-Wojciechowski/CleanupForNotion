using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Infrastructure.State.Factories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.State.Factories;

[TestClass]
public class JsonFilePluginStateProviderFactoryTests {
  [TestMethod]
  public void Name_ReturnsInMemory() => new JsonFilePluginStateProviderFactory(null!, null!, null!).Name.ShouldBe("File");

  [TestMethod]
  public void CanCreate_InMemory_ReturnsTrue() =>
      new JsonFilePluginStateProviderFactory(
              null!,
              null!,
              new OptionsWrapper<CfnOptions>(new CfnOptions { AuthToken = "", Plugins = [], StateEngine = "File" }))
          .CanCreate().ShouldBeTrue();

  [TestMethod]
  public void CanCreate_Null_ReturnsFalse() =>
      new JsonFilePluginStateProviderFactory(
              null!,
              null!,
              new OptionsWrapper<CfnOptions>(new CfnOptions { AuthToken = "", Plugins = [], StateEngine = null }))
          .CanCreate().ShouldBeFalse();

  [TestMethod]
  public void Create_ReturnsProvider() =>
      new JsonFilePluginStateProviderFactory(
              NullLoggerFactory.Instance,
              Substitute.For<INotificationSender>(),
              new OptionsWrapper<CfnOptions>(new CfnOptions { AuthToken = "", Plugins = [], StateEngine = null }))
          .Create().ShouldBeOfType<JsonFilePluginStateProvider>();
}