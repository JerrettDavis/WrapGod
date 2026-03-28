# Hangfire <-> Quartz.NET Mapping Matrix

| Concern | Hangfire Pattern | Quartz.NET Pattern | Direction | Safety | Notes |
|---|---|---|---|---|---|
| Fire-and-forget | `BackgroundJob.Enqueue(() => job.Method())` | `IScheduler.ScheduleJob(job, trigger.StartNow())` | both | safe | Direct equivalent; Quartz requires explicit job+trigger pair |
| Delayed execution | `BackgroundJob.Schedule(() => job.Method(), TimeSpan)` | `TriggerBuilder.Create().StartAt(DateTimeOffset)` | both | safe | Convert relative delay to absolute time for Quartz |
| Recurring (cron) | `RecurringJob.AddOrUpdate(id, () => job.Method(), cronExpr)` | `TriggerBuilder.Create().WithCronSchedule(cronExpr)` | both | review | Hangfire uses 5-field cron; Quartz uses 6-7 field (prepend seconds) |
| Continuation/chaining | `BackgroundJob.ContinueJobWith(parentId, () => next())` | `IJobListener` + manual trigger of follow-up job | H->Q | manual | Quartz has no built-in continuation; requires listener or workflow |
| Continuation/chaining | `IJobListener` + manual trigger | `BackgroundJob.ContinueJobWith(parentId, () => next())` | Q->H | safe | Hangfire continuation is simpler than Quartz listener pattern |
| Retry policy | `[AutomaticRetry(Attempts = N)]` on method | `IJobListener` re-scheduling or try/catch in `Execute()` | H->Q | manual | No attribute equivalent in Quartz; must implement retry logic |
| Retry policy | `IJobListener` / manual retry | `[AutomaticRetry(Attempts = N)]` | Q->H | safe | Hangfire retry is declarative and simpler |
| Queue assignment | `[Queue("critical")]` attribute on method | `TriggerBuilder.WithPriority(N)` (higher = first) | both | review | Hangfire queues are named; Quartz uses numeric priority. Not 1:1. |
| Concurrency control | Server-level worker count (`WorkerCount`) | `[DisallowConcurrentExecution]` attribute on job class | both | review | Hangfire controls concurrency globally; Quartz controls per-job-key |
| Misfire handling | Automatic retry on missed schedules | `WithMisfireHandlingInstructionFireAndProceed()` etc. | both | review | Quartz offers fine-grained misfire strategies; Hangfire is simpler |
| Job identity | `RecurringJob.AddOrUpdate("job-id", ...)` string ID | `JobBuilder.Create<T>().WithIdentity("name", "group")` | both | safe | Quartz uses name+group pair; map Hangfire ID to Quartz name |
| Job data passing | Method parameters serialized by Hangfire | `JobDataMap` key-value pairs on `IJobDetail` | both | review | Parameter names must be mapped to data map keys manually |
| Job removal | `RecurringJob.RemoveIfExists(id)` | `IScheduler.DeleteJob(JobKey)` | both | safe | Direct equivalent |
| Manual trigger | `RecurringJob.TriggerJob(id)` | `IScheduler.TriggerJob(JobKey)` | both | safe | Direct equivalent |
| Job state monitoring | `IMonitoringApi` (Dashboard) | `IScheduler.GetJobDetail()` / `GetTriggersOfJob()` | both | review | Different monitoring APIs; dashboard vs programmatic |
| Batch operations | Hangfire.Pro `BatchJob` (paid) | `IScheduler.ScheduleJobs(IDictionary)` | both | manual | Hangfire batches require Pro license; Quartz has native multi-schedule |
