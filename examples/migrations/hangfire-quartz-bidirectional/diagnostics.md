# Diagnostics and Manual Migration Guidance

Automated migration should flag the following patterns for manual review.

## Semantic gaps: Hangfire -> Quartz.NET

1. **Job continuations have no Quartz equivalent**
   - Hangfire `BackgroundJob.ContinueJobWith(parentId, ...)` chains jobs declaratively.
   - Quartz.NET has no built-in continuation primitive. Must implement via `IJobListener.JobWasExecuted()` that schedules the follow-up job.
   - Recommendation: emit manual-review diagnostic. Provide a listener template in generated code.

2. **Retry policy is declarative in Hangfire, imperative in Quartz**
   - Hangfire `[AutomaticRetry(Attempts = 3)]` is a method-level attribute.
   - Quartz retry must be implemented as try/catch + `JobExecutionException(refireImmediately: true)` or an `IJobListener` that re-schedules on failure.
   - Recommendation: emit diagnostic with retry count and generate a listener stub.

3. **Named queues vs numeric priority**
   - Hangfire `[Queue("critical")]` routes jobs to named processing queues.
   - Quartz uses `ITrigger.Priority` (integer, default 5) for ordering within a single thread pool.
   - These are fundamentally different concurrency models. Named queues provide isolation; priority only affects ordering.
   - Recommendation: emit review diagnostic. Document that Quartz priority is not equivalent to queue isolation.

4. **Cron expression format mismatch**
   - Hangfire uses 5-field cron: `minute hour day month weekday`
   - Quartz uses 6-7 field cron: `second minute hour day month weekday [year]`
   - Migration must prepend `0 ` (seconds field) to every Hangfire cron expression.
   - Recommendation: automated transformation is safe, but emit info diagnostic.

5. **Method parameters vs JobDataMap**
   - Hangfire serializes method parameters directly from the `Enqueue(() => job.Method(arg1, arg2))` expression.
   - Quartz passes data via `JobDataMap` key-value pairs on `IJobDetail`.
   - Parameter-to-key mapping must be established during migration. Type mismatches are possible.
   - Recommendation: emit review diagnostic for each parameterized job.

## Semantic gaps: Quartz.NET -> Hangfire

1. **Misfire instructions are richer in Quartz**
   - Quartz offers `WithMisfireHandlingInstructionFireAndProceed`, `DoNothing`, `IgnoreMisfirePolicy`, etc.
   - Hangfire automatically retries missed recurring jobs with no configurable misfire strategy.
   - Recommendation: emit diagnostic when migrating jobs with non-default misfire instructions.

2. **Per-job concurrency control does not exist in Hangfire**
   - Quartz `[DisallowConcurrentExecution]` prevents overlapping executions of the same job key.
   - Hangfire controls concurrency at the server level (`WorkerCount`), not per job.
   - Hangfire.Pro offers `[DisableConcurrentExecution(timeout)]` but it requires the paid license.
   - Recommendation: emit manual-review diagnostic. Suggest Hangfire.Pro or application-level locking.

3. **Job groups have no Hangfire equivalent**
   - Quartz organizes jobs into groups via `JobKey("name", "group")`.
   - Hangfire uses flat string IDs for recurring jobs and auto-generated IDs for one-off jobs.
   - Recommendation: encode group information into Hangfire job ID (e.g., `"group:name"`) or drop it.

4. **Calendar-based exclusions**
   - Quartz supports `ICalendar` for excluding dates (holidays, maintenance windows).
   - Hangfire has no calendar concept; exclusions require application-level checks.
   - Recommendation: emit manual-review diagnostic when Quartz calendars are in use.

5. **Trigger listeners and job listeners**
   - Quartz `ITriggerListener` and `IJobListener` provide lifecycle hooks (before execute, vetoed, completed).
   - Hangfire has `IElectStateFilter`, `IApplyStateFilter`, and server filters, but the hook model is different.
   - Recommendation: emit manual-review diagnostic for each listener. Map to appropriate Hangfire filter.

## Manual path checklist

- Inventory all job classes and their scheduling patterns (fire-and-forget, delayed, recurring, continuation)
- Map retry policies: count `[AutomaticRetry]` attributes (Hangfire) or listener-based retry logic (Quartz)
- Identify queue/priority assignments and document isolation requirements
- Port simple scheduling first (fire-and-forget, delayed, recurring)
- Add focused parity tests for continuation chains and retry behavior
- Validate cron expression translation (5-field to 6-field and back)
- Verify concurrency behavior under load in the target scheduler
