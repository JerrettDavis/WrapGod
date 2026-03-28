# EF Core <-> Dapper Staged Coexistence Checklist

## Phase 1: Boundary stabilization

- Define service/repository contracts independent of ORM specifics.
- Add parity tests over representative query + command flows.
- Ensure return DTOs and ordering semantics are deterministic.

## Phase 2: Coexistence rollout

- Keep EF Core path active while introducing Dapper implementation behind feature flag.
- Mirror writes in lower environments and compare object-graph outputs.
- Validate transaction boundaries for multi-operation workflows.

## Phase 3: Cutover + hardening

- Promote Dapper path for selected workloads.
- Keep parity tests and diagnostics in CI.
- Retain manual review gates for tracking-heavy and complex projection scenarios.

## Verification checklist

- [ ] Bidirectional service-boundary scenarios pass parity tests
- [ ] Query and command behavior produce equivalent outputs
- [ ] Transaction semantics documented and reviewed
- [ ] Unsupported direct mappings are explicitly diagnosed
- [ ] CI validates sample behavior continuously
