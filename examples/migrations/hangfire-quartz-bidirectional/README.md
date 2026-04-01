# Hangfire <-> Quartz.NET Bidirectional Integration Pack

This migration pack demonstrates bidirectional scheduling coverage between
[Hangfire](https://www.hangfire.io/) and [Quartz.NET](https://www.quartz-scheduler.net/)
job schedulers.

## Layout

- `HangfireApp/` — Hangfire scheduling abstraction and job definitions
- `QuartzApp/` — Quartz.NET scheduling abstraction and IJob implementations
- `ParityTests/` — API surface and behavioral parity checks for both directions
- `mapping-matrix.md` — schedule/retry/misfire/concurrency mapping table
- `diagnostics.md` — semantic gaps and unsafe transformation guidance
- `migration-checklist.md` — operational behavior parity steps

## Scope covered

- Fire-and-forget scheduling (`Enqueue` / `StartNow` trigger)
- Delayed scheduling (`Schedule` / `StartAt` trigger)
- Recurring schedules (`RecurringJob.AddOrUpdate` / `WithCronSchedule`)
- Job continuations (`ContinueJobWith` / durable job + listener)
- Retry policies (`[AutomaticRetry]` / `IJobListener` re-schedule)
- Queue assignment (`[Queue("critical")]` / trigger priority)
- Concurrency control (server workers / `[DisallowConcurrentExecution]`)
- Misfire handling (Hangfire auto-retry / Quartz misfire instructions)
- Cron expression translation (5-field to 6/7-field)

## Run locally

```bash
dotnet test examples/migrations/hangfire-quartz-bidirectional/ParityTests/ParityTests.csproj
```
