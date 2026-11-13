using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Notifications;
using CleanupForNotion.Core.Infrastructure.State;
using CleanupForNotion.Core.Infrastructure.State.Factories;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace CleanupForNotion.Core.Test.Infrastructure.State.Factories;

[TestClass]
public class PostgresPluginStateProviderFactoryTests {
  [TestMethod]
  public void Name_ReturnsInMemory() =>
      new PostgresPluginStateProviderFactory(null!, null!).Name.ShouldBe("PostgreSQL");

  [TestMethod]
  public void CanCreate_InMemory_ReturnsTrue() =>
      new PostgresPluginStateProviderFactory(
              null!,
              new OptionsWrapper<CfnOptions>(
                  new CfnOptions { AuthToken = "", Plugins = [], StateEngine = "PostgreSQL" }))
          .CanCreate().ShouldBeTrue();

  [TestMethod]
  public void CanCreate_Null_ReturnsFalse() =>
      new PostgresPluginStateProviderFactory(
              null!,
              new OptionsWrapper<CfnOptions>(new CfnOptions { AuthToken = "", Plugins = [], StateEngine = null }))
          .CanCreate().ShouldBeFalse();

  [TestMethod]
  public void Create_ReturnsProvider() =>
      new PostgresPluginStateProviderFactory(
              Substitute.For<INotificationSender>(),
              new OptionsWrapper<CfnOptions>(new CfnOptions {
                  AuthToken = "", Plugins = [], StateEngine = null, StateAccessDetails = "connection string"
              }))
          .Create().ShouldBeOfType<PostgresPluginStateProvider>();
}
