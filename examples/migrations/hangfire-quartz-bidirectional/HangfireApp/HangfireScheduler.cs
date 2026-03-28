using Hangfire;

namespace HangfireApp;

/// <summary>
/// Scheduling abstraction wrapping Hangfire's static API surface.
/// Captures the four Hangfire scheduling patterns for migration comparison.
/// </summary>
public sealed class HangfireScheduler
{
    /// <summary>
    /// Enqueue a fire-and-forget job (executes immediately on next available worker).
    /// Hangfire: <c>BackgroundJob.Enqueue(() => job.Method(args))</c>
    /// </summary>
    public string EnqueueFireAndForget(string recipient)
    {
        return BackgroundJob.Enqueue(() => new EmailJob().SendWelcomeEmail(recipient));
    }

    /// <summary>
    /// Schedule a delayed job (executes after the specified delay).
    /// Hangfire: <c>BackgroundJob.Schedule(() => job.Method(args), delay)</c>
    /// </summary>
    public string ScheduleDelayed(string source, TimeSpan delay)
    {
        return BackgroundJob.Schedule(() => new DataSyncJob().SyncExternalData(source), delay);
    }

    /// <summary>
    /// Register a recurring job on a cron schedule.
    /// Hangfire: <c>RecurringJob.AddOrUpdate(id, () => job.Method(args), cronExpression)</c>
    /// </summary>
    public void AddRecurring(string jobId, string cronExpression)
    {
        RecurringJob.AddOrUpdate(jobId, () => new ReportJob().GenerateDailyReport(DateTime.Today), cronExpression);
    }

    /// <summary>
    /// Chain a continuation job that runs after the parent completes.
    /// Hangfire: <c>BackgroundJob.ContinueJobWith(parentId, () => job.Method(args))</c>
    /// </summary>
    public string EnqueueContinuation(string parentJobId, string source, bool success)
    {
        return BackgroundJob.ContinueJobWith(parentJobId, () => new DataSyncJob().PublishSyncResult(source, success));
    }

    /// <summary>
    /// Enqueue a job to a specific named queue.
    /// Hangfire: queue is declared via <c>[Queue("critical")]</c> attribute on the job method.
    /// </summary>
    public string EnqueueCritical(string orderId, string recipient)
    {
        return BackgroundJob.Enqueue(() => new EmailJob().SendOrderConfirmation(orderId, recipient));
    }

    /// <summary>
    /// Remove a recurring job by ID.
    /// Hangfire: <c>RecurringJob.RemoveIfExists(id)</c>
    /// </summary>
    public void RemoveRecurring(string jobId)
    {
        RecurringJob.RemoveIfExists(jobId);
    }

    /// <summary>
    /// Trigger an existing recurring job immediately (outside its schedule).
    /// Hangfire: <c>RecurringJob.TriggerJob(id)</c>
    /// </summary>
    public void TriggerRecurringNow(string jobId)
    {
        RecurringJob.TriggerJob(jobId);
    }
}
