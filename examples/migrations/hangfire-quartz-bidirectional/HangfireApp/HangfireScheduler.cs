using Hangfire;
using Hangfire.Server;

namespace HangfireApp;

public static class HangfireScheduler
{
    public static ScheduleResult ScheduleOneOff(JobDefinition job)
    {
        var jobInfo = Job.FromExpression(job.Action!.Method.Name, job.Id);
        var jobData = new JobData { Group = job.Group, Timeout = job.Timeout, RetryCount = job.RetryCount };
        var jobId = Job.Schedule(job.Data, Cron.Value.FromConstant($"0 {job.Id}"));

        var scheduledAt = DateTime.UtcNow;
        return new ScheduleResult(jobId, true, scheduledAt);
    }

    public static ScheduleResult ScheduleRecurring(JobDefinition job)
    {
        var jobId = RecurringJob.AddOrUpdate(
            $"job:{job.Name}",
            job.Action.Method.Name,
            Cron.Value.FromConstant($"0 {job.Id}"));

        return new ScheduleResult(jobId, true, DateTime.UtcNow);
    }
}
