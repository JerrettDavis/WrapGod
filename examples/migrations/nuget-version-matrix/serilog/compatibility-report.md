# Serilog Version Compatibility Report

API delta analysis across v2.12.0, v3.1.1, v4.2.0.

## Summary

- Baseline: `v2.12.0`
- Latest: `v4.2.0`
- Deltas: `3`
- Introduced members: `3`
- Removed members: `0`
- Changed members: `1`

---

## Async shutdown API (delta-1)

**Severity:** `safe`

**Migration recommendation:** Prefer CloseAndFlushAsync() where available; keep CloseAndFlush() fallback for LCD compatibility.

### Introduced members
- `Serilog.Log.CloseAndFlushAsync()` (method, introduced in v3.0.0)

### Removed members
- None

### Changed members
- None

---

## Trace context members on LogEvent (delta-2)

**Severity:** `review`

**Migration recommendation:** Add nullable wrappers for TraceId/SpanId and branch behavior by runtime version.

### Introduced members
- `Serilog.Events.LogEvent.TraceId` (property, introduced in v3.1.0)
- `Serilog.Events.LogEvent.SpanId` (property, introduced in v3.1.0)

### Removed members
- None

### Changed members
- None

---

## Override matching behavior (delta-3)

**Severity:** `guided`

**Migration recommendation:** Normalize source context casing to avoid behavior drift between v2/v3 and v4.

### Introduced members
- None

### Removed members
- None

### Changed members
- `LoggerMinimumLevelConfiguration.Override(string, LogEventLevel)` (method, changed in v4.0.0): Source context matching became case-insensitive in v4.
