using Quartz;

namespace QuartzApp;

/// <summary>
/// Demonstrates Quartz.NET job patterns: IJob implementations with triggers,
/// cron schedules, misfire instructions, and concurrency control.
/// </summary>
[DisallowConcurrentExecution]
public sealed class EmailJob : IJob
{
    public const string RecipientKey = "recipient";
    public const string OrderIdKey = "orderId";

    public Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var recipient = data.GetString(RecipientKey) ?? "unknown";
        var orderId = data.GetString(OrderIdKey);

        if (!string.IsNullOrEmpty(orderId))
        {
            Console.WriteLine($"Sending order {orderId} confirmation to {recipient}");
        }
        else
        {
            Console.WriteLine($"Sending welcome email to {recipient}");
        }

        return Task.CompletedTask;
    }
}

[DisallowConcurrentExecution]
public sealed class ReportJob : IJob
{
    public const string ReportDateKey = "reportDate";

    public Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var dateStr = data.GetString(ReportDateKey);
        var reportDate = string.IsNullOrEmpty(dateStr) ? DateTime.Today : DateTime.Parse(dateStr);

        Console.WriteLine($"Generating daily report for {reportDate:yyyy-MM-dd}");
        return Task.CompletedTask;
    }
}

public sealed class WeeklyDigestJob : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        Console.WriteLine("Generating weekly digest");
        return Task.CompletedTask;
    }
}

[DisallowConcurrentExecution]
public sealed class DataSyncJob : IJob
{
    public const string SourceKey = "source";

    public Task Execute(IJobExecutionContext context)
    {
        var source = context.MergedJobDataMap.GetString(SourceKey) ?? "unknown";
        Console.WriteLine($"Syncing data from {source}");
        return Task.CompletedTask;
    }
}

public sealed class PublishSyncResultJob : IJob
{
    public const string SourceKey = "source";
    public const string SuccessKey = "success";

    public Task Execute(IJobExecutionContext context)
    {
        var data = context.MergedJobDataMap;
        var source = data.GetString(SourceKey) ?? "unknown";
        var success = data.GetBoolean(SuccessKey);
        Console.WriteLine($"Publishing sync result for {source}: {(success ? "OK" : "FAILED")}");
        return Task.CompletedTask;
    }
}
