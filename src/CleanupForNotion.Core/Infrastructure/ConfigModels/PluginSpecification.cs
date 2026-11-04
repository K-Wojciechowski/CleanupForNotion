namespace CleanupForNotion.Core.Infrastructure.ConfigModels;

public record PluginSpecification(int PluginIndex, string PluginName, string PluginDescription, string? GroupId, RawPluginOptions RawOptions);
