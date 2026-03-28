using Quartz;
using Quartz.Impl;

namespace QuartzApp;

/// <summary>
/// Scheduling abstraction wrapping Quartz.NET's IScheduler API surface.
/// Captures the equivalent Quartz patterns for each Hangfire scheduling mode.
/// </summary>
public sealed class QuartzScheduler
{
    private readonly IScheduler _scheduler;

    public QuartzScheduler(IScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <summary>
    /// Create a QuartzScheduler backed by an in-memory RAMJobStore (for tests/demos).
    /// </summary>
    public static async Task<QuartzScheduler> CreateInMemoryAsync()
    {
        var factory = new StdSchedulerFactory();
        var scheduler = await factory.GetScheduler();
        return new QuartzScheduler(scheduler);
    }

    /// <summary>
    /// Schedule a fire-and-forget job (starts immediately).
    /// Quartz equivalent of <c>BackgroundJob.Enqueue</c>.
    /// </summary>
    public async Task<JobKey> ScheduleFireAndForget(string recipient)
    {
        var jobKey = new JobKey($"email-{Guid.NewGuid():N}", "email");

        var job = JobBuilder.Create<EmailJob>()
            .WithIdentity(jobKey)
            .UsingJobData(EmailJob.RecipientKey, recipient)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"trigger-{jobKey.Name}", "email")
            .StartNow()
            .Build();

        await _scheduler.ScheduleJob(job, trigger);
        return jobKey;
    }

    /// <summary>
    /// Schedule a delayed job (starts after the specified delay).
    /// Quartz equivalent of <c>BackgroundJob.Schedule</c>.
    /// </summary>
    public async Task<JobKey> ScheduleDelayed(string source, TimeSpan delay)
    {
        var jobKey = new JobKey($"sync-{Guid.NewGuid():N}", "data-sync");

        var job = JobBuilder.Create<DataSyncJob>()
            .WithIdentity(jobKey)
            .UsingJobData(DataSyncJob.SourceKey, source)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"trigger-{jobKey.Name}", "data-sync")
            .StartAt(DateTimeOffset.UtcNow.Add(delay))
            .Build();

        await _scheduler.ScheduleJob(job, trigger);
        return jobKey;
    }

    /// <summary>
    /// Register a recurring job on a cron schedule.
    /// Quartz equivalent of <c>RecurringJob.AddOrUpdate</c>.
    /// </summary>
    public async Task<JobKey> AddRecurring(string jobId, string cronExpression)
    {
        var jobKey = new JobKey(jobId, "recurring");

        var job = JobBuilder.Create<ReportJob>()
            .WithIdentity(jobKey)
            .UsingJobData(ReportJob.ReportDateKey, DateTime.Today.ToString("o"))
            .StoreDurably()
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"trigger-{jobId}", "recurring")
            .WithCronSchedule(cronExpression, x => x
                .WithMisfireHandlingInstructionFireAndProceed())
            .Build();

        await _scheduler.ScheduleJob(job, trigger);
        return jobKey;
    }

    /// <summary>
    /// Schedule a continuation-like job using a job listener pattern.
    /// Quartz has no built-in continuation; this schedules a follow-up job
    /// that must be triggered after the parent completes (via listener or manual trigger).
    /// </summary>
    public async Task<JobKey> ScheduleContinuation(string source, bool success)
    {
        var jobKey = new JobKey($"publish-{Guid.NewGuid():N}", "continuation");

        var job = JobBuilder.Create<PublishSyncResultJob>()
            .WithIdentity(jobKey)
            .UsingJobData(PublishSyncResultJob.SourceKey, source)
            .UsingJobData(PublishSyncResultJob.SuccessKey, success)
            .StoreDurably()
            .Build();

        await _scheduler.AddJob(job, replace: true);
        return jobKey;
    }

    /// <summary>
    /// Trigger a stored durable job immediately (for continuations or ad-hoc runs).
    /// </summary>
    public async Task TriggerJobNow(JobKey jobKey)
    {
        await _scheduler.TriggerJob(jobKey);
    }

    /// <summary>
    /// Schedule a critical job (Quartz uses priority instead of named queues).
    /// Quartz equivalent of Hangfire's <c>[Queue("critical")]</c> attribute.
    /// </summary>
    public async Task<JobKey> ScheduleCritical(string orderId, string recipient)
    {
        var jobKey = new JobKey($"critical-email-{Guid.NewGuid():N}", "critical");

        var job = JobBuilder.Create<EmailJob>()
            .WithIdentity(jobKey)
            .UsingJobData(EmailJob.RecipientKey, recipient)
            .UsingJobData(EmailJob.OrderIdKey, orderId)
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"trigger-{jobKey.Name}", "critical")
            .StartNow()
            .WithPriority(10) // higher priority = processed first (default is 5)
            .Build();

        await _scheduler.ScheduleJob(job, trigger);
        return jobKey;
    }

    /// <summary>
    /// Remove a scheduled job by key.
    /// Quartz equivalent of <c>RecurringJob.RemoveIfExists</c>.
    /// </summary>
    public async Task<bool> RemoveJob(JobKey jobKey)
    {
        return await _scheduler.DeleteJob(jobKey);
    }

    /// <summary>
    /// Start the scheduler (required before jobs will execute).
    /// </summary>
    public async Task StartAsync() => await _scheduler.Start();

    /// <summary>
    /// Shut down the scheduler.
    /// </summary>
    public async Task ShutdownAsync() => await _scheduler.Shutdown();
}
