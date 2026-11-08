using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Infrastructure.State.Factories;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.State.Factories;

[TestClass]
public class InMemoryPluginStateProviderFactoryTests {
  [TestMethod]
  public void Name_ReturnsInMemory() => new InMemoryPluginStateProviderFactory(null!).Name.ShouldBe("InMemory");

  [TestMethod]
  public void CanCreate_InMemory_ReturnsTrue() =>
      new InMemoryPluginStateProviderFactory(
              new OptionsWrapper<CfnOptions>(new CfnOptions { AuthToken = "", Plugins = [], StateEngine = "InMemory" }))
          .CanCreate().ShouldBeTrue();

  [TestMethod]
  public void CanCreate_Null_ReturnsTrue() =>
      new InMemoryPluginStateProviderFactory(
              new OptionsWrapper<CfnOptions>(new CfnOptions { AuthToken = "", Plugins = [], StateEngine = null }))
          .CanCreate().ShouldBeTrue();

  [TestMethod]
  public void Create_ReturnsProvider() =>
      new InMemoryPluginStateProviderFactory(
              new OptionsWrapper<CfnOptions>(new CfnOptions { AuthToken = "", Plugins = [], StateEngine = null }))
          .Create().ShouldBeOfType<InMemoryPluginStateProvider>();
}
