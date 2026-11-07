using CleanupForNotion.Core.Infrastructure.ConfigModels;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace CleanupForNotion.Web.Test;

public class CfnWebApplicationFactory(
    string? webhookSecret = null,
    List<WebhookConfiguration>? webhookConfigurations = null,
    bool traceLoggingEnabled = true) : WebApplicationFactory<Program> {
  protected override void ConfigureWebHost(IWebHostBuilder builder) {
    var options = new CfnOptions {
        AuthToken = "", Plugins = [], WebhookSecret = webhookSecret, Webhooks = webhookConfigurations
    };
    builder.ConfigureLogging(b => b.SetMinimumLevel(traceLoggingEnabled ? LogLevel.Trace : LogLevel.Debug));

    builder.ConfigureServices(services => {
      var hostedServices = services.Where(s => s.ServiceType == typeof(IHostedService)).ToList();
      foreach (var s in hostedServices) services.Remove(s);
      services.AddFakeLogging(o => {
          o.FilteredLevels = Enum.GetValues<LogLevel>().Where(l => traceLoggingEnabled || l != LogLevel.Trace).ToHashSet();
      });
      services.AddSingleton(Options.Create(options));
    });
  }
}
