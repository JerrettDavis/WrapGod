# Moq Version Compatibility Report

API delta analysis across v4.10, v4.16, v4.20.

## Summary

- Baseline: `v4.10`
- Latest: `v4.20`
- Deltas: `3`
- Introduced members: `4`
- Removed members: `0`
- Changed members: `1`

---

## Protected mocking type-safety (delta-1)

**Severity:** `review`

**Migration recommendation:** Use Protected().As<TAnalog>() when minimum supported version is >= 4.15; keep string-based fallback for LCD.

### Introduced members
- `IProtectedMock<T>.As<TAnalog>()` (method, introduced in v4.15)

### Removed members
- None

### Changed members
- None

---

## Event subscription setup APIs (delta-2)

**Severity:** `review`

**Migration recommendation:** Gate SetupAdd/SetupRemove and VerifyAdd/VerifyRemove behind >= 4.13 in adaptive wrappers.

### Introduced members
- `Mock<T>.SetupAdd(...)` (method, introduced in v4.13)
- `Mock<T>.SetupRemove(...)` (method, introduced in v4.13)

### Removed members
- None

### Changed members
- None

---

## Verifiable Times overload (delta-3)

**Severity:** `safe`

**Migration recommendation:** Expose Verifiable(Times) only for targeted v4.20+ strategy and omit from LCD.

### Introduced members
- `ISetup.Verifiable(Times)` (method, introduced in v4.20)

### Removed members
- None

### Changed members
- `ISetup.Verifiable()` (method, changed in v4.20): New overload supports setup-time invocation count expectations.
