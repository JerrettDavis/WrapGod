# Diagnostics and Guidance for Semantic Shift Areas

## Suggested diagnostics

1. **WG3611: Transport-only concern detected in mediator flow**
   - Trigger when retry/dead-letter/topology assumptions are embedded in handlers.
   - Guidance: isolate transport concerns behind policies/infrastructure adapters.

2. **WG3612: Pipeline order dependency detected**
   - Trigger when behavior/filter ordering is required for correctness.
   - Guidance: explicitly document and assert order in tests.

3. **WG3613: Registration convention mismatch**
   - Trigger when implicit assembly scanning differs across stacks.
   - Guidance: switch to explicit registrations for deterministic startup.

4. **WG3614: Notification fan-out semantic drift risk**
   - Trigger when handlers depend on ordering/short-circuit assumptions.
   - Guidance: avoid ordering assumptions; assert idempotency and side-effect boundaries.
