# Hangfire <-> Quartz.NET Staged Coexistence Checklist

## Phase 1: Boundary stabilization

- Define job contracts independent of scheduler-specific implementation.
- Add parity tests over representative job outcomes (enqueue, execute, completion, failure).

## Phase 2: Coexistence rollout

- Keep Hangfire path active while introducing Quartz implementation behind feature flag.
- Mirror job outcomes and compare execution outcomes in CI.
- Validate retry/misfire boundaries for multi-operation workflows.

## Phase 3: Cutover + hardening

- Promote Quartz path for selected workloads.
- Keep parity tests and diagnostics in CI.
- Retain manual review gates for distributed concurrency scenarios.

## Verification checklist

- [ ] Bidirectional job path scenarios pass parity tests
- [ ] Schedule behavior (frequency, misfire, timeout) is equivalent
- [ ] Retry/misfire semantics documented and verified
- [ ] Unsupported direct mappings are explicitly diagnosed
- [ ] CI validates sample behavior continuously
