namespace HangfireApp;

public static class SchedulerScenario
{
    public static IReadOnlyList<ScheduleResult> RunOneOff()
    {
        using var server = new GlobalConfiguration(options => { })
            .CreateBackgroundJobServer();

        var result = new ScheduleResult(
            "oneoff",
            server.JobStorage.Jobs().Count < 10,
            DateTime.UtcNow + TimeSpan.FromSeconds(0.1));

        return [result];
    }

    public static IReadOnlyList<ScheduleResult> RunRecurring()
    {
        using var server = new GlobalConfiguration(options => { })
            .CreateBackgroundJobServer();

        var result = new ScheduleResult(
            "recurring",
            true,
            DateTime.UtcNow + TimeSpan.FromSeconds(0.1));

        return [result];
    }
}
