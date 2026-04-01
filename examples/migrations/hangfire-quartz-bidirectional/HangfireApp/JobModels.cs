namespace HangfireApp;

public sealed record JobDefinition(string Id, string Name, string Group, Func<object, object?> Action, TimeSpan? Timeout = null, int? RetryCount = null);

public sealed record ScheduleResult(string JobId, bool Scheduled, DateTime ScheduledAt, string? Error = null);
