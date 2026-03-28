# Hangfire <-> Quartz.NET Schedule Mapping Matrix

| Concern | Hangfire Pattern | Quartz.NET Pattern | Direction | Safety | Notes |
|---|---|---|---|---|---|
| One-off job | `Job.Enqueue(Job.FromType)` | `IScheduler.ScheduleJob(CronTriggerBuilder...)` | both | review | Translation requires intent verification |
| Recurring job | `RecurringJob.AddOrUpdate` | `TriggerBuilder.CronSchedule(...)` | both | review | Cron expression translation needed |
| Retry policy | `Job.WithConcurrency` + `JobState` | `TriggerWithIdentity.MaxAttempts` | both | manual | Misfire handling differs by policy |
| Job parameters | `Job.Parameter` | `Trigger.Key` or constructor args | both | safe | Keep parameter serialization deterministic |
| Job completion | `JobResult.Finished` | `Trigger.Finished` | both | safe | Outcome semantics are consistent |
| Scheduler state | `GetJobsByType` | `GetTriggersByGroup` | Hangfire->Quartz | review | Job-to-trigger mapping needs explicit rules |
