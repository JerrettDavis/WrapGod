# Diagnostics and Manual Migration Guidance

Use diagnostics to separate automatable transforms from architecture-level refactors.

## Suggested diagnostics

1. **WG3601: EF tracking-dependent logic detected**
   - Trigger when business logic depends on tracked entity state transitions.
   - Manual path: introduce explicit update commands and state transitions for Dapper.

2. **WG3602: LINQ query cannot be deterministically translated**
   - Trigger for dynamic LINQ/projection patterns without a clear SQL equivalent.
   - Manual path: author vetted SQL + regression tests.

3. **WG3603: Unit-of-work boundary mismatch**
   - Trigger when multiple repositories rely on implicit `SaveChanges` semantics.
   - Manual path: define explicit transaction boundaries at service layer.

4. **WG3604: Migration/tooling dependency detected**
   - Trigger where EF migration pipeline is assumed at runtime/deploy time.
   - Manual path: separate schema deployment strategy from data-access runtime migration.
