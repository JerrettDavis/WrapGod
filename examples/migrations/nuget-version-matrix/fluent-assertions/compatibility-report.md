# FluentAssertions Version Compatibility Report

API delta analysis across v5, v6, v8.

## Summary

- Baseline: `v5`
- Latest: `v8`
- Deltas: `3`
- Introduced members: `2`
- Removed members: `1`
- Changed members: `2`

---

## AssertionScope async behavior improvements (delta-1)

**Severity:** `review`

**Migration recommendation:** Prefer async-safe assertion scope usage and update helper wrappers when targeting v8.

### Introduced members
- `FluentAssertions.Execution.AssertionScope.DisposeAsync()` (method, introduced in v8)

### Removed members
- None

### Changed members
- `FluentAssertions.Execution.AssertionScope.Dispose()` (method, changed in v8): Async disposal path introduced alongside sync disposal semantics.

---

## Collection assertion API tightening (delta-2)

**Severity:** `breaking`

**Migration recommendation:** Adjust wrappers to the stricter generic constraints and expose LCD-only overloads for cross-version compatibility.

### Introduced members
- None

### Removed members
- `GenericCollectionAssertions<T>.ContainSingle(Expression<Func<T, bool>>)` (method, removed by v8)

### Changed members
- `GenericCollectionAssertions<T>.ContainSingle(...)` (method, changed in v6): Signature and overload shape evolved between v5-v8.

---

## Equivalency options extension points (delta-3)

**Severity:** `safe`

**Migration recommendation:** Expose new extension points in targeted/adaptive strategy while keeping LCD subset stable.

### Introduced members
- `EquivalencyOptions<T>.UsingRuntimeTyping()` (method, introduced in v6)

### Removed members
- None

### Changed members
- None
