# Serilog Version-Matrix Diff Report

API surface comparison across Serilog v2.12.0, v3.1.1, and v4.2.0.

## Summary

| Metric | v2 -> v3 | v3 -> v4 |
|--------|----------|----------|
| Types added | 1 | 1 |
| Types removed | 0 | 0 |
| Members added | 6 | 2 |
| Members removed | 0 | 0 |
| Members changed | 0 | 1 |
| Target framework changes | Yes | No |

---

## Delta 1: `Log.CloseAndFlushAsync()` added in v3.0.0

**Kind:** Member Added
**Type:** `Serilog.Log`
**Versions:** absent in v2.x, present in v3.0.0+

```csharp
// v2 — synchronous only
Log.CloseAndFlush();

// v3+ — async variant available
await Log.CloseAndFlushAsync();
```

**Impact:** Applications using `async Main()` or hosted service shutdown can now flush asynchronously using `ValueTask`. The synchronous `CloseAndFlush()` remains available in all versions.

**WrapGod handling:** LCD strategy wraps synchronous call only. Adaptive strategy emits `#if` branch preferring async on v3+.

---

## Delta 2: `LogEvent.TraceId` / `LogEvent.SpanId` added in v3.1.0

**Kind:** Members Added
**Type:** `Serilog.Events.LogEvent`
**Versions:** absent in v2.x/v3.0.x, present in v3.1.0+

```csharp
// v2/v3.0 — trace context must be added manually as properties
logEvent.AddOrUpdateProperty(
    new LogEventProperty("TraceId", new ScalarValue(Activity.Current?.TraceId)));

// v3.1+ — first-class properties
ActivityTraceId? traceId = logEvent.TraceId;
ActivitySpanId? spanId = logEvent.SpanId;
```

**Impact:** OpenTelemetry-style distributed tracing correlation is built into the log event model. Sinks that support structured output (Seq, Elasticsearch) automatically receive trace/span IDs.

A new 7-parameter `LogEvent` constructor was also added:
```csharp
new LogEvent(timestamp, level, exception, messageTemplate, properties, traceId, spanId)
```

**WrapGod handling:** LCD strategy excludes these members. Adaptive strategy exposes nullable wrappers returning `null` on v2.

---

## Delta 3: `IBatchedLogEventSink` interface added in v3.0.0

**Kind:** Type Added
**Type:** `Serilog.Core.IBatchedLogEventSink`
**Versions:** absent in v2.x, present in v3.0.0+

```csharp
// v2 — batching required separate PeriodicBatchingSink NuGet package
// Serilog.Sinks.PeriodicBatching provided PeriodicBatchingSink base class

// v3+ — first-class batching interface in core Serilog
public interface IBatchedLogEventSink
{
    ValueTask EmitBatchAsync(IReadOnlyCollection<LogEvent> batch);
    ValueTask OnEmptyBatchAsync();
}
```

**Impact:** Sink authors no longer need the separate `Serilog.Sinks.PeriodicBatching` package. The batching infrastructure moved into core Serilog, backed by `System.Threading.Channels`. In v4.0.0, a corresponding `BatchingOptions` class was added to configure batch size, queue limits, and buffering.

**WrapGod handling:** LCD strategy does not wrap batched sinks. Targeted-latest uses v4 `BatchingOptions`. Adaptive strategy branches across all three versions.

---

## Delta 4: `Enrich.When()` conditional enrichment added in v3.1.0

**Kind:** Member Added
**Type:** `Serilog.Configuration.LoggerEnrichmentConfiguration`
**Versions:** absent in v2.x/v3.0.x, present in v3.1.0+

```csharp
// v2 — conditional enrichment required a custom ILogEventEnricher
public class ConditionalEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory factory)
    {
        if (logEvent.Level >= LogEventLevel.Warning)
            logEvent.AddOrUpdateProperty(factory.CreateProperty("Alert", true));
    }
}

// v3.1+ — built-in conditional enrichment
.Enrich.When(
    evt => evt.Level >= LogEventLevel.Warning,
    e => e.WithProperty("Alert", true))
```

**Impact:** Simplifies conditional enrichment patterns. Combined with `Enrich.AtLevel()` (available since v2.12.0), provides a complete conditional enrichment API.

**WrapGod handling:** LCD strategy omits `When()`. Adaptive strategy wraps with a custom enricher on v2.

---

## Delta 5: Assembly version unpinned in v4.0.0

**Kind:** Binary Compatibility Change
**Versions:** v2.x and v3.x pin assembly version to `2.0.0.0`; v4.x uses package version as assembly version

**Impact:** .NET Framework applications using binding redirects must update them when upgrading to v4. The pinned `2.0.0.0` assembly version in v2/v3 allowed in-place package upgrades without redirect changes. In v4, each package version has a distinct assembly version, requiring binding redirect updates.

This does not affect .NET Core / .NET 5+ applications which do not use assembly binding redirects.

**WrapGod handling:** Not an API surface change. Documented as a deployment consideration for .NET Framework consumers.

---

## Delta 6: `LoggerSinkConfiguration.CreateSink()` added in v4.0.0

**Kind:** Static Member Added
**Type:** `Serilog.Configuration.LoggerSinkConfiguration`
**Versions:** absent in v2.x/v3.x, present in v4.0.0+

```csharp
// v4+ — build a sink from a configuration callback
ILogEventSink sink = LoggerSinkConfiguration.CreateSink(
    lsc => lsc.Console());
```

**Impact:** Utility method for constructing `ILogEventSink` instances from configuration lambdas without creating a full logger. Useful for testing and advanced sink composition.

**WrapGod handling:** LCD strategy excludes. Targeted-latest wraps directly. Adaptive provides a polyfill building a temporary logger on v2/v3.

---

## Delta 7: Case-insensitive level overrides in v4.0.0

**Kind:** Behavioral Change
**Type:** `Serilog.Configuration.LoggerMinimumLevelConfiguration.Override()`
**Versions:** case-sensitive in v2.x/v3.x, case-insensitive in v4.x

```csharp
// v2/v3 — exact case match required
.MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
// Only matches sources starting with exactly "Microsoft.AspNetCore"

// v4 — case-insensitive matching
.MinimumLevel.Override("microsoft.aspnetcore", LogEventLevel.Warning)
// Matches "Microsoft.AspNetCore", "MICROSOFT.ASPNETCORE", etc.
```

**Impact:** Source context names are now matched case-insensitively when evaluating level overrides. This is a subtle behavioral change that could cause different filtering behavior if source contexts use inconsistent casing across the codebase.

**WrapGod handling:** Adaptive strategy normalizes source context strings to avoid cross-version behavioral drift. Flagged as `guided` safety.

---

## Delta 8: `BatchingOptions` class added in v4.0.0

**Kind:** Type Added
**Type:** `Serilog.Configuration.BatchingOptions`
**Versions:** absent in v2.x/v3.x, present in v4.0.0+

```csharp
// v4 — structured batching configuration
.WriteTo.Sink(myBatchedSink, new BatchingOptions
{
    BufferingTimeLimit = TimeSpan.FromSeconds(2),
    BatchSizeLimit = 1000,
    EagerlyEmitFirstEvent = true,
    QueueLimit = 10000
});
```

**Impact:** Replaces ad-hoc constructor parameters on `PeriodicBatchingSink` with a dedicated options class. Provides `QueueLimit` for backpressure control via `System.Threading.Channels`.

**WrapGod handling:** LCD excludes. Targeted-latest wraps directly. Adaptive branches between `BatchingOptions` (v4), direct `IBatchedLogEventSink` (v3), and `PeriodicBatchingSink` (v2).

---

## Delta 9: .NET target framework minimum raised in v3.0.0

**Kind:** Platform Change
**Versions:** v2.x supports netstandard1.3+; v3.x requires netstandard2.0 minimum

```
v2.12.0 targets: netstandard1.3, netstandard2.0, netstandard2.1, net45, net46
v3.1.1 targets:  netstandard2.0, netstandard2.1, net462, net471, net5.0, net6.0, net7.0
v4.2.0 targets:  netstandard2.0, netstandard2.1, net462, net471, net6.0, net8.0
```

**Impact:** Projects targeting .NET Standard 1.x or .NET Framework < 4.6.2 cannot upgrade beyond Serilog v2. Dropped: .NET Standard 1.x, .NET Framework 4.5, .NET Core 1.x.

**WrapGod handling:** The LCD strategy documents this as a constraint. Wrapper generation should validate target framework compatibility before recommending an upgrade path.

---

## Delta 10: `Enrich.AtLevel()` presence tracking

**Kind:** Member Presence Clarification
**Type:** `Serilog.Configuration.LoggerEnrichmentConfiguration`
**Versions:** present in v2.12.0+

```csharp
// Available since v2.12.0 — scoped enrichment by level
.Enrich.AtLevel(LogEventLevel.Error, e => e.WithProperty("ErrorContext", true))
```

**Impact:** While `AtLevel()` predates v3, it was added late in the v2 lifecycle (v2.12.0). Projects pinned to earlier v2 releases (e.g., v2.8.0) will not have this API. The manifest correctly marks `introducedIn: "2.12.0"`.

**WrapGod handling:** All strategies include `AtLevel()` since the minimum tracked version is v2.12.0. Projects on older v2 releases should be flagged during analysis.
