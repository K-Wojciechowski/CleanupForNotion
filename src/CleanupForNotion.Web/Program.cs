using System.Threading.Channels;
using CleanupForNotion.Core;
using CleanupForNotion.Core.Infrastructure.ConfigModels;
using CleanupForNotion.Core.Infrastructure.Loop;

namespace CleanupForNotion.Web;

public class Program {
  public static void Main(string[] args) {
    var builder = WebApplication.CreateBuilder(args);

    builder.Services
        .AddCfnServices()
        .AddSingleton(TimeProvider.System)
        .AddHostedService<ChannelTimerBackgroundService>()
        .AddHostedService<ChannelBasedLoop>()
        .AddLogging(loggingBuilder => loggingBuilder.AddSimpleConsole(options => {
          options.IncludeScopes = true;
          options.SingleLine = true;
          options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        }))
        .Configure<CfnOptions>(builder.Configuration.GetSection("CleanupForNotion"))
        .AddSingleton<Channel<DateTimeOffset>>(_ =>
            Channel.CreateUnbounded<DateTimeOffset>(
                new UnboundedChannelOptions {
                    SingleWriter = false, SingleReader = true, AllowSynchronousContinuations = false
                }));

    var app = builder.Build();

    app.MapPost("/", async (Channel<DateTimeOffset> channel, TimeProvider timeProvider) => {
      await channel.Writer.WriteAsync(timeProvider.GetUtcNow()).ConfigureAwait(false);
      return "Triggered";
    });

    app.Run();
  }
}
