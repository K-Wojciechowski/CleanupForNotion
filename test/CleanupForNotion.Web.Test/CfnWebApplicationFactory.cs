using Microsoft.AspNetCore.Mvc.Testing;

namespace CleanupForNotion.Web.Test;

public class CfnWebApplicationFactory : WebApplicationFactory<Program> {
  protected override void ConfigureWebHost(IWebHostBuilder builder) {
    builder.ConfigureServices(services => {
      var hostedServices = services.Where(s => s.ServiceType == typeof(IHostedService)).ToList();
      foreach (var s in hostedServices) services.Remove(s);
    });
  }
}
