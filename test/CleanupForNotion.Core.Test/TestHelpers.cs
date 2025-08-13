using CleanupForNotion.Core.Infrastructure.ConfigModels;
using Microsoft.Extensions.Options;

namespace CleanupForNotion.Core.Test;

public static class TestHelpers {
  public static IOptions<CfnOptions> GetCfnOptions() => GetCfnOptions([]);

  public static IOptions<CfnOptions> GetCfnOptions(List<Dictionary<string, object>>? plugins) {
    var options = new CfnOptions { AuthToken = string.Empty, Plugins = plugins! };
    return new OptionsWrapper<CfnOptions>(options);
  }
}
