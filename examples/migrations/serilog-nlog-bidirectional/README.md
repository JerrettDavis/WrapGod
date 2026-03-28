# Serilog <-> NLog Bidirectional Integration Pack

This migration pack demonstrates bidirectional API coverage between
[Serilog](https://serilog.net/) and [NLog](https://nlog-project.org/),
including structured-property behavior and parity tests.

## Layout

- `SerilogApp/` — source-side sample implementation with Serilog APIs
- `NLogApp/` — destination-side equivalent implementation with NLog APIs
- `ParityTests/` — CI-friendly parity tests to verify equivalent log output
- `mapping-matrix.md` — API mapping table for both migration directions
- `diagnostics.md` — unsupported/unsafe patterns and manual migration paths

## Scope covered

- Logger acquisition and level methods (`Debug/Info/Error`)
- Structured property capture (`OrderId`, `Total`, global `service` property)
- Exception logging parity in failure paths
- Runtime config caveats for sinks/targets

## Run locally

```bash
dotnet test examples/migrations/serilog-nlog-bidirectional/ParityTests/ParityTests.csproj
```

## Notes

This pack intentionally focuses on portable logging semantics.
Advanced sink/target behavior (buffering, batching, async wrappers,
layout/rendering differences) is tracked as analyzer/manual-review guidance
in `diagnostics.md`.
