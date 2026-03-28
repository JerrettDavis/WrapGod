# Serilog NuGet Version-Matrix Example

Demonstrates how WrapGod handles API drift across **different major versions** of the same NuGet package. Unlike library-to-library migration examples (FluentAssertions to Shouldly), this example shows version-matrix divergence within a single package: **Serilog**.

## Versions Tracked

| Version | Package | Key Characteristics |
|---------|---------|---------------------|
| v2.12.0 | Serilog 2.12.0 | Long-lived stable. netstandard1.3+. Sync-only flush. No batching in core. |
| v3.1.1 | Serilog 3.1.1 | netstandard2.0 min. Async flush. IBatchedLogEventSink. TraceId/SpanId. Conditional enrichment. |
| v4.2.0 | Serilog 4.2.0 | Assembly version unpinned. BatchingOptions. CreateSink(). Case-insensitive overrides. |

## Directory Structure

```
serilog/
  README.md              ← this file
  diff-report.md         ← detailed API deltas across versions
  v2/
    manifest.wrapgod.json   ← Serilog 2.12.0 API surface
  v3/
    manifest.wrapgod.json   ← Serilog 3.1.1 API surface
  v4/
    manifest.wrapgod.json   ← Serilog 4.2.0 API surface
  strategies/
    lcd.wrapgod.config.json          ← lowest-common-denominator wrapper
    targeted-latest.wrapgod.config.json  ← v4-only wrapper
    adaptive.wrapgod.config.json     ← version-branching wrapper
  SampleApp/
    VersionMatrixDemo.cs   ← code showing version-specific patterns
```

## API Deltas

See [diff-report.md](diff-report.md) for the full analysis. Key changes:

| Delta | Versions | Impact |
|-------|----------|--------|
| `Log.CloseAndFlushAsync()` | v3.0.0+ | Async shutdown for hosted services |
| `LogEvent.TraceId` / `SpanId` | v3.1.0+ | First-class OpenTelemetry correlation |
| `IBatchedLogEventSink` | v3.0.0+ | Core batching without external package |
| `Enrich.When()` | v3.1.0+ | Built-in conditional enrichment |
| Assembly version unpinned | v4.0.0 | Binding redirect changes on .NET Framework |
| `LoggerSinkConfiguration.CreateSink()` | v4.0.0+ | Standalone sink construction |
| Case-insensitive level overrides | v4.0.0 | Behavioral change in `MinimumLevel.Override()` |
| `BatchingOptions` class | v4.0.0+ | Structured batching configuration |
| Target framework minimum raised | v3.0.0 | Drops netstandard1.x, .NET Framework < 4.6.2 |
| `Enrich.AtLevel()` tracking | v2.12.0+ | Late v2 addition, absent in older v2 releases |

## Strategies

### LCD (Lowest-Common-Denominator)

Generates wrappers using only APIs present in **all** tracked versions. Safe for projects that must support consumers on v2.x through v4.x simultaneously.

**Includes:** Core logging methods, `LoggerConfiguration` fluent chain, `ForContext`, `CloseAndFlush` (sync), `Enrich.WithProperty`, `MinimumLevel.Override`.

**Excludes:** Async flush, trace IDs, batched sinks, conditional enrichment, `CreateSink()`, `BatchingOptions`.

### Targeted-Latest

Generates wrappers leveraging the full v4.x API surface. Best for greenfield projects or those committed to Serilog v4.

**Includes:** Everything in LCD plus async flush, trace IDs, batched sinks with `BatchingOptions`, `Enrich.When()`, `CreateSink()`.

### Adaptive

Generates wrappers with compile-time `#if` directives that branch behavior based on the installed Serilog version. Compilation symbols:

| Symbol | Version Range |
|--------|---------------|
| `SERILOG_V2` | >=2.0.0 <3.0.0 |
| `SERILOG_V3` | >=3.0.0 <4.0.0 |
| `SERILOG_V4` | >=4.0.0 <5.0.0 |
| `SERILOG_V3_PLUS` | >=3.0.0 |
| `SERILOG_V3_1_PLUS` | >=3.1.0 |

This strategy maximizes feature coverage while maintaining backward compatibility. Each branching point is documented in the config with the specific version that introduced the capability.

## Config-Entry Semantic Changes

Several Serilog APIs maintain the same method signature across versions but change behavior:

1. **`MinimumLevel.Override(source, level)`** -- In v2/v3, `source` matching is case-sensitive. In v4, matching is case-insensitive. A WrapGod wrapper should normalize source context strings if cross-version behavioral consistency is required.

2. **`WriteTo.Sink(ILogEventSink, LogEventLevel)`** -- The `restrictedToMinimumLevel` parameter exists in all versions, but v3 added an additional overload accepting a `LoggingLevelSwitch` for dynamic level control. Code relying on the simpler overload works everywhere, but the `LevelSwitch` overload silently compiles on v2 if the wrong overload is resolved.

3. **`LogEvent` construction** -- The 5-parameter constructor is stable across all versions. The 7-parameter constructor (with `traceId`/`spanId`) exists only in v3.1+. Custom sink code constructing `LogEvent` instances directly must be version-aware.

4. **`Enrich.AtLevel()`** -- Present since v2.12.0, but absent in earlier v2 releases. Projects pinned to v2.8.0 or similar will not have this API despite being "v2".

5. **Assembly identity** -- v2/v3 pin the assembly version to `2.0.0.0` regardless of NuGet package version. v4 uses the actual package version as the assembly version. This is invisible at the API level but affects .NET Framework binding redirects and assembly loading.

## Usage

1. Compare manifests in `v2/`, `v3/`, and `v4/` to see exact API surface differences
2. Review [diff-report.md](diff-report.md) for detailed analysis of each delta
3. Choose a strategy from `strategies/` based on your version support requirements
4. See `SampleApp/VersionMatrixDemo.cs` for code examples of each version-specific pattern
