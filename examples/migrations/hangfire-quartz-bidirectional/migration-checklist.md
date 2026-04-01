# Migration Checklist — Operational Behavior Parity

Step-by-step checklist for migrating between Hangfire and Quartz.NET while
preserving operational behavior.

## Phase 1: Inventory

- [ ] List all job classes and their scheduling mode (fire-and-forget, delayed, recurring, continuation)
- [ ] Document retry policies per job (attribute or listener-based)
- [ ] Document queue assignments (Hangfire) or priority values (Quartz)
- [ ] Document concurrency requirements (per-job vs global worker count)
- [ ] Document cron expressions and their intended schedules
- [ ] Identify jobs with continuations or chained dependencies
- [ ] Identify jobs using misfire instructions (Quartz) or custom state filters (Hangfire)
- [ ] Identify jobs using calendar exclusions (Quartz only)

## Phase 2: Infrastructure

- [ ] **Hangfire -> Quartz**: configure Quartz scheduler (RAMJobStore for dev, AdoJobStore for production)
- [ ] **Quartz -> Hangfire**: configure Hangfire storage (SQL Server, PostgreSQL, Redis, or InMemory)
- [ ] Set up DI registration for the target scheduler
- [ ] Configure connection strings and persistence options
- [ ] Set up dashboard/monitoring (Hangfire Dashboard or Quartz health checks)

## Phase 3: Job class migration

- [ ] **Hangfire -> Quartz**: convert POCO job methods to `IJob.Execute(IJobExecutionContext)` implementations
- [ ] **Quartz -> Hangfire**: convert `IJob` classes to POCO classes with public methods
- [ ] Map method parameters to `JobDataMap` keys (H->Q) or vice versa (Q->H)
- [ ] Translate `[AutomaticRetry(Attempts = N)]` to listener-based retry (H->Q)
- [ ] Translate listener-based retry to `[AutomaticRetry]` attribute (Q->H)
- [ ] Translate `[DisallowConcurrentExecution]` to application-level locking or Hangfire.Pro (Q->H)

## Phase 4: Scheduling migration

- [ ] Migrate fire-and-forget jobs (safest — direct equivalent in both)
- [ ] Migrate delayed jobs (convert relative TimeSpan to absolute DateTimeOffset for Quartz)
- [ ] Migrate recurring jobs (translate cron format: prepend `0 ` for H->Q, strip seconds for Q->H)
- [ ] Migrate continuations (H->Q: implement `IJobListener`; Q->H: use `ContinueJobWith`)
- [ ] Migrate queue/priority assignments (document that isolation semantics differ)
- [ ] Migrate misfire handling (Q->H: Hangfire auto-retries; verify acceptable behavior)

## Phase 5: Verification

- [ ] All projects build cleanly (`dotnet build`)
- [ ] Parity tests pass (`dotnet test`)
- [ ] Fire-and-forget jobs execute within expected latency
- [ ] Delayed jobs execute at the correct time (within scheduler polling interval)
- [ ] Recurring jobs fire on the expected cron schedule
- [ ] Continuation chains complete in correct order
- [ ] Retry policies trigger the correct number of attempts on failure
- [ ] Concurrent execution constraints are respected
- [ ] Misfire behavior is acceptable (jobs missed during downtime are handled)
- [ ] Job data is passed correctly (no serialization/deserialization issues)
- [ ] Dashboard/monitoring shows correct job state

## Phase 6: Cutover

- [ ] Run both schedulers in parallel during transition (dual-write if feasible)
- [ ] Monitor error rates and job completion times in both systems
- [ ] Drain the old scheduler's queue before decommissioning
- [ ] Remove old scheduler's storage tables/collections after successful migration
- [ ] Update alerting and monitoring to point to the new scheduler
