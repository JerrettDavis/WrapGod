# Diagnostics and Scheduler Semantic Shift Guidance

Use diagnostics to separate automatable scheduler patterns from architecture-level refactors.

## Suggested diagnostics

1. **WG4001: Hangfire global queue assumption detected**
   - Trigger when Hangfire's default global queue is relied on.
   - Guidance: Quartz requires explicit trigger group/namespace mapping.

2. **WG4002: Hangfire job priority/timeout differences**
   - Trigger when Hangfire priority/timeout semantics are assumed.
   - Guidance: Quartz does not expose direct priority; translation to `Priority` via `TriggerBuilder` or custom store needed.

3. **WG4003: Misfire handling mismatch**
   - Trigger when Hangfire's misfire handling (allow misfires) is assumed.
   - Guidance: Quartz exposes `AllowMisfire` property explicitly.

4. **WG4004: Concurrency lock semantics divergence**
   - Trigger when Hangfire's distributed lock semantics are assumed.
   - Guidance: Quartz uses `ClusterAwareJobStore` or similar for distributed concurrency.
