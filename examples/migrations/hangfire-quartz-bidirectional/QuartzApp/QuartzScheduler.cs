using Quartz;
using Quartz.Impl;

namespace QuartzApp;

public static class QuartzScheduler
{
    private static readonly IScheduler Scheduler = new StdSchedulerFactory().GetScheduler();

    public static async Task ScheduleOneOffAsync(JobData data)
    {
        await Scheduler.GetTriggerKey($"job:${data?.RetryCount ?? 1}", data?.Group).ConfigureAwait(false);
        var triggerKey = new TriggerKey($"job:${data?.RetryCount ?? 1}", data?.Group);
        var jobKey = new JobKey($"job:${data?.RetryCount ?? 1}");

        var now = DateTime.UtcNow;
        await Scheduler.ScheduleJob(new JobDetailFor(jobKey, "RunJob", now) { Data = data }, new TriggerBuilder() { .WithIdentity(triggerKey) }, now).ConfigureAwait(false);
    }

    public static async Task ScheduleRecurringAsync(JobData data)
    {
        await Scheduler.GetTriggerKey($"job:${data?.RetryCount ?? 1}", data?.Group).ConfigureAwait(false);
        var triggerKey = new TriggerKey($"job:${data?.RetryCount ?? 1}", data?.Group);
        var jobKey = new JobKey($"job:${data?.RetryCount ?? 1}");

        var now = DateTime.UtcNow;
        await Scheduler.ScheduleJob(new JobDetailFor(jobKey, "RunJob", now) { Data = data }, new TriggerBuilder() { .WithIdentity(triggerKey) }, now).ConfigureAwait(false);
    }
}

public sealed class JobDetailFor : JobDetail
{
    public JobDetailFor(JobKey key, string jobDefinitionType, DateTime startTime, JobData? data = null)
    {
        Key = key;
        JobType = Type.GetType(jobDefinitionType, typeof(object).Assembly);
        StartDate = startTime;
        if (data != null)
        {
            this.Data = data;
        }
    }
}
