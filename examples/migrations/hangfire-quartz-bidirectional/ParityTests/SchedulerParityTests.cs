using HangfireApp;
using QuartzApp;
using Quartz;
using Quartz.Impl;
using Xunit;

namespace ParityTests;

/// <summary>
/// Verifies that scheduling abstractions in both HangfireApp and QuartzApp
/// expose equivalent API surfaces and produce comparable scheduling configurations.
/// These are compile-time parity checks — actual job execution requires infrastructure
/// (Hangfire storage, Quartz scheduler) that is not wired in unit tests.
/// </summary>
public class SchedulerParityTests
{
    // ---------------------------------------------------------------
    // API surface parity: both schedulers expose the same operations
    // ---------------------------------------------------------------

    [Fact]
    public void Both_schedulers_expose_fire_and_forget()
    {
        var hangfire = new HangfireScheduler();
        var quartz = typeof(QuartzScheduler);

        Assert.True(HasMethod(typeof(HangfireScheduler), "EnqueueFireAndForget"));
        Assert.True(HasMethod(quartz, "ScheduleFireAndForget"));
    }

    [Fact]
    public void Both_schedulers_expose_delayed_scheduling()
    {
        Assert.True(HasMethod(typeof(HangfireScheduler), "ScheduleDelayed"));
        Assert.True(HasMethod(typeof(QuartzScheduler), "ScheduleDelayed"));
    }

    [Fact]
    public void Both_schedulers_expose_recurring_job_registration()
    {
        Assert.True(HasMethod(typeof(HangfireScheduler), "AddRecurring"));
        Assert.True(HasMethod(typeof(QuartzScheduler), "AddRecurring"));
    }

    [Fact]
    public void Both_schedulers_expose_job_removal()
    {
        Assert.True(HasMethod(typeof(HangfireScheduler), "RemoveRecurring"));
        Assert.True(HasMethod(typeof(QuartzScheduler), "RemoveJob"));
    }

    [Fact]
    public void Both_schedulers_expose_critical_queue_scheduling()
    {
        Assert.True(HasMethod(typeof(HangfireScheduler), "EnqueueCritical"));
        Assert.True(HasMethod(typeof(QuartzScheduler), "ScheduleCritical"));
    }

    // ---------------------------------------------------------------
    // Job definition parity: both apps define equivalent job classes
    // ---------------------------------------------------------------

    [Fact]
    public void Hangfire_EmailJob_has_equivalent_Quartz_EmailJob()
    {
        var hangfireType = typeof(HangfireApp.EmailJob);
        var quartzType = typeof(QuartzApp.EmailJob);

        Assert.NotNull(hangfireType);
        Assert.NotNull(quartzType);
        Assert.True(typeof(IJob).IsAssignableFrom(quartzType));
    }

    [Fact]
    public void Hangfire_ReportJob_has_equivalent_Quartz_ReportJob()
    {
        var hangfireType = typeof(HangfireApp.ReportJob);
        var quartzType = typeof(QuartzApp.ReportJob);

        Assert.NotNull(hangfireType);
        Assert.NotNull(quartzType);
        Assert.True(typeof(IJob).IsAssignableFrom(quartzType));
    }

    [Fact]
    public void Hangfire_DataSyncJob_has_equivalent_Quartz_DataSyncJob()
    {
        var hangfireType = typeof(HangfireApp.DataSyncJob);
        var quartzType = typeof(QuartzApp.DataSyncJob);

        Assert.NotNull(hangfireType);
        Assert.NotNull(quartzType);
        Assert.True(typeof(IJob).IsAssignableFrom(quartzType));
    }

    // ---------------------------------------------------------------
    // Cron expression parity: both use standard cron strings
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("0 0 * * *")]   // Hangfire Cron.Daily equivalent
    [InlineData("0 0 * * 1")]   // Hangfire Cron.Weekly equivalent
    [InlineData("0 */2 * * *")] // Every 2 hours
    public void Cron_expressions_are_shared_between_hangfire_and_quartz(string cron)
    {
        // Hangfire uses 5-field cron (minute hour day month weekday)
        // Quartz uses 6-7 field cron (second minute hour day month weekday [year])
        // Migration must prepend "0 " to convert Hangfire cron to Quartz cron
        var quartzCron = $"0 {cron}";
        Assert.True(CronExpression.IsValidExpression(quartzCron),
            $"Quartz should accept converted cron: {quartzCron}");
    }

    // ---------------------------------------------------------------
    // Retry policy parity
    // ---------------------------------------------------------------

    [Fact]
    public void Hangfire_retry_attribute_maps_to_documented_quartz_listener_pattern()
    {
        // Hangfire: [AutomaticRetry(Attempts = 3)] on method
        var retryAttr = typeof(HangfireApp.EmailJob)
            .GetMethod(nameof(HangfireApp.EmailJob.SendWelcomeEmail))!
            .GetCustomAttributes(typeof(Hangfire.AutomaticRetryAttribute), false);
        Assert.Single(retryAttr);

        var attr = (Hangfire.AutomaticRetryAttribute)retryAttr[0];
        Assert.Equal(3, attr.Attempts);

        // Quartz: retry is handled via IJobListener or re-scheduling in Execute()
        // No attribute equivalent — this is a semantic gap documented in diagnostics.md
    }

    // ---------------------------------------------------------------
    // Concurrency control parity
    // ---------------------------------------------------------------

    [Fact]
    public void Quartz_jobs_use_DisallowConcurrentExecution_where_hangfire_uses_server_default()
    {
        // Quartz: [DisallowConcurrentExecution] attribute on job class
        var quartzEmailAttrs = typeof(QuartzApp.EmailJob)
            .GetCustomAttributes(typeof(DisallowConcurrentExecutionAttribute), false);
        Assert.Single(quartzEmailAttrs);

        var quartzSyncAttrs = typeof(QuartzApp.DataSyncJob)
            .GetCustomAttributes(typeof(DisallowConcurrentExecutionAttribute), false);
        Assert.Single(quartzSyncAttrs);

        // Hangfire: concurrency is controlled by worker count per server,
        // not per-job. This is a semantic gap documented in diagnostics.md.
    }

    // ---------------------------------------------------------------
    // Queue/priority mapping parity
    // ---------------------------------------------------------------

    [Fact]
    public void Hangfire_queue_attribute_is_present_on_critical_job()
    {
        var attrs = typeof(HangfireApp.EmailJob)
            .GetMethod(nameof(HangfireApp.EmailJob.SendOrderConfirmation))!
            .GetCustomAttributes(typeof(Hangfire.QueueAttribute), false);
        Assert.Single(attrs);

        var queueAttr = (Hangfire.QueueAttribute)attrs[0];
        Assert.Equal("critical", queueAttr.Queue);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static bool HasMethod(Type type, string methodName)
    {
        return type.GetMethod(methodName) != null;
    }
}
