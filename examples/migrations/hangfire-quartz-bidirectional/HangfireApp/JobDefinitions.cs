using Hangfire;

namespace HangfireApp;

/// <summary>
/// Demonstrates Hangfire job patterns: fire-and-forget, delayed, recurring,
/// continuations, retry policies, and queue assignment.
/// </summary>
public sealed class EmailJob
{
    [AutomaticRetry(Attempts = 3)]
    [Queue("default")]
    public void SendWelcomeEmail(string recipient)
    {
        // fire-and-forget: BackgroundJob.Enqueue(() => new EmailJob().SendWelcomeEmail("user@example.com"))
        Console.WriteLine($"Sending welcome email to {recipient}");
    }

    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    [Queue("critical")]
    public void SendOrderConfirmation(string orderId, string recipient)
    {
        Console.WriteLine($"Sending order {orderId} confirmation to {recipient}");
    }
}

public sealed class ReportJob
{
    [AutomaticRetry(Attempts = 2)]
    [Queue("default")]
    public void GenerateDailyReport(DateTime reportDate)
    {
        // recurring: RecurringJob.AddOrUpdate("daily-report", () => new ReportJob().GenerateDailyReport(DateTime.Today), Cron.Daily)
        Console.WriteLine($"Generating daily report for {reportDate:yyyy-MM-dd}");
    }

    [Queue("default")]
    public void GenerateWeeklyDigest()
    {
        // recurring: RecurringJob.AddOrUpdate("weekly-digest", () => new ReportJob().GenerateWeeklyDigest(), Cron.Weekly)
        Console.WriteLine("Generating weekly digest");
    }
}

public sealed class DataSyncJob
{
    [AutomaticRetry(Attempts = 3)]
    [Queue("default")]
    public void SyncExternalData(string source)
    {
        // delayed: BackgroundJob.Schedule(() => new DataSyncJob().SyncExternalData("crm"), TimeSpan.FromMinutes(30))
        Console.WriteLine($"Syncing data from {source}");
    }

    [Queue("default")]
    public void PublishSyncResult(string source, bool success)
    {
        // continuation: BackgroundJob.ContinueJobWith(parentId, () => new DataSyncJob().PublishSyncResult("crm", true))
        Console.WriteLine($"Publishing sync result for {source}: {(success ? "OK" : "FAILED")}");
    }
}
