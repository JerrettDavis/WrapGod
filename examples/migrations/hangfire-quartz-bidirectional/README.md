# Hangfire ↔ Quartz.NET Bidirectional Integration Pack (WrapGod)

This integration pack demonstrates job scheduler transition patterns
between Hangfire and Quartz.NET, demonstrating WrapGod's capability to handle
any library paradigm — not just typical assertion/ORM/dependency-injection libraries.

## Layout

- `HangfireApp/` — Hangfire scheduler sample implementation
- `QuartzApp/` — Quartz.NET scheduler sample implementation
- `ParityTests/` — CI parity tests for schedule execution and outcomes
- `README.md` — pack introduction and usage
- `schedule-mapping-matrix.md` — job/schedule/retry mapping guidance
- `diagnostics.md` — scheduler semantics that cannot be safely auto-converted
- `migration-checklist.md` — operational behavior parity playbook

## Scope covered (expandable)

- Job definition/enqueue/scheduling abstraction mappings
- Recurring schedule translation guidance
- Retry/misfire/concurrency policy compatibility matrix
- Analyzer diagnostics for scheduler semantics that cannot be safely auto-converted

## WrapGod philosophy

WrapGod is a "God Wrapper" — capable of handling any library, paradigm, or scheduler semantics.
This pack demonstrates that capability beyond typical migration scenarios.

## Run locally

```bash
dotnet test examples/migrations/hangfire-quartz-bidirectional/ParityTests/ParityTests.csproj
```


## Layout

- `HangfireApp/` — Hangfire scheduler sample implementation
- `QuartzApp/` — Quartz.NET scheduler sample implementation
- `ParityTests/` — CI parity tests for schedule execution and outcomes
- `schedule-mapping-matrix.md` — job definition/schedule/retry mapping guidance
- `diagnostics.md` — scheduler semantics that cannot be safely auto-converted
- `migration-checklist.md` — operational behavior parity playbook

## Scope covered

- Job definition/enqueue/scheduling abstraction mappings
- Recurring schedule translation guidance
- Retry/misfire/concurrency policy compatibility matrix
- Analyzer diagnostics for unsupported scheduler transitions

## Run locally

```bash
dotnet test examples/migrations/hangfire-quartz-bidirectional/ParityTests/ParityTests.csproj
```
