namespace CleanupForNotion.Core.Infrastructure.Execution;

public record RunRequest(DateTimeOffset TriggerTime, string? GroupId = null);
