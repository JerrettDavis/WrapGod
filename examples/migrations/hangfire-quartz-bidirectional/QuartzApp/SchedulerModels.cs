using Quartz;

namespace QuartzApp;

public sealed class JobData
{
    public string? Group { get; set; }
    public TimeSpan? Timeout { get; set; }
    public int? RetryCount { get; set; }
}

public sealed record ScheduleResult(string JobKey, bool Scheduled, DateTime ScheduledAt, string? Error = null);
