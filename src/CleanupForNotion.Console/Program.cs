using CleanupForNotion.Console;
using CleanupForNotion.Core;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddCfnServices()
    .AddSingleton(TimeProvider.System)
    .AddHostedService<ConsoleHostedServiceWrapper>()
    .AddLogging(loggingBuilder => loggingBuilder.AddSimpleConsole(options => {
      options.IncludeScopes = true;
      options.SingleLine = true;
      options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    }))
    .Configure<CfnOptions>(builder.Configuration.GetSection("CleanupForNotion"));

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
