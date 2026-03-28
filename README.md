# WrapGod

WrapGod is a .NET platform for generating wrapper interfaces, facades, adapters, and migration tooling over third-party APIs. It gives teams a systematic way to decouple application code from vendor libraries so that upgrades, swaps, and multi-version support become manageable.

## Features

- **Manifest extraction** -- interrogate any .NET assembly and produce a complete JSON manifest of its public API surface (types, members, generics, parameters).
- **Multi-version support** -- extract and merge manifests from multiple assembly versions with automatic diffing, breaking-change detection, and version presence metadata.
- **Three configuration surfaces** -- define wrapper rules via JSON config files, C# attributes (`[WrapType]`, `[WrapMember]`), or a fluent DSL, with configurable merge precedence when combining sources.
- **Incremental source generation** -- a Roslyn incremental generator reads `*.wrapgod.json` manifests and emits `IWrapped{Type}` interfaces and `{Type}Facade` delegating classes at build time.
- **Compatibility modes** -- control the generated surface with LCD (lowest common denominator), Targeted (single version), or Adaptive (runtime version guards) modes.
- **Roslyn analyzers and code fixes** -- detect direct usage of wrapped third-party types (`WG2001`) and methods (`WG2002`), then auto-migrate call sites with one-click or Fix All support.
- **Type mapping** -- plan source-to-destination type transformations for generated wrapper signatures.

## Quick start

```bash
# Install packages
dotnet add package WrapGod.Extractor
dotnet add package WrapGod.Generator

# Extract a manifest
# (see docs/QUICKSTART.md for the full C# walkthrough)
```

See the [Quick Start Guide](docs/QUICKSTART.md) for a complete walkthrough
covering extraction, configuration, generation, and migration.

## CLI diagnostics gate and exit codes

`wrap-god analyze` follows the RFC-0054 diagnostics gate policy:

- `0`: success (no effective errors)
- `1`: command/runtime failure (bad input, deserialize/IO failure, exception)
- `2`: diagnostics gate failed due to effective `error` diagnostics
- `3`: diagnostics gate failed due to effective `warning` diagnostics when `--warnings-as-errors` is enabled

Use `--warnings-as-errors` to promote warning diagnostics to a failing gate:

```bash
wrap-god analyze manifest.wrapgod.json --warnings-as-errors
```


## CLI doctor (setup and health validation)

`wrap-god doctor` validates repository health in three areas:

- SDK/tooling prerequisites (`dotnet --version`, `global.json` integrity)
- Source/config and lockfile state (`*.wrapgod.json`, `wrapgod.lock.json`)
- CI/workflow readiness (`.github/workflows` and build/test steps)

```bash
# Human-readable output
wrap-god doctor --path .

# CI-friendly output
wrap-god doctor --path . --format json

# Fail the gate on warnings
wrap-god doctor --path . --warnings-as-errors
```

During the current rollout, doctor emits dependency-tagged warnings linked to
[#123](https://github.com/JerrettDavis/WrapGod/issues/123) and
[#124](https://github.com/JerrettDavis/WrapGod/issues/124) when source discovery
and lockfile assumptions are not yet available.
## Solution structure

| Project | Description |
|---------|-------------|
| `WrapGod.Abstractions` | Public attributes (`[WrapType]`, `[WrapMember]`) and config contracts |
| `WrapGod.Manifest` | Manifest schema, serialization, JSON config loader, attribute reader, merge engine |
| `WrapGod.Extractor` | Assembly interrogation, single- and multi-version manifest extraction |
| `WrapGod.Generator` | Roslyn incremental source generator for wrapper interfaces and facades |
| `WrapGod.TypeMap` | Type mapping contracts and mapping planner |
| `WrapGod.Analyzers` | Roslyn diagnostics (WG2001, WG2002) and automatic code fixes |
| `WrapGod.Fluent` | Fluent configuration DSL |
| `WrapGod.Runtime` | Optional runtime helpers and adapters |
| `WrapGod.Tests` | Unit, integration, and snapshot tests |

## Documentation

- [Quick Start Guide](docs/QUICKSTART.md) -- end-to-end onboarding walkthrough
- [Manifest Format Reference](docs/MANIFEST.md) -- schema, type/member nodes, version metadata
- [Configuration Guide](docs/CONFIGURATION.md) -- JSON, attributes, fluent DSL, merge precedence
- [Compatibility Modes](docs/COMPATIBILITY.md) -- LCD, Targeted, and Adaptive modes
- [Analyzer Diagnostics Reference](docs/ANALYZERS.md) -- WG2001, WG2002, code fixes, suppression
- [Migration Playbook](docs/MIGRATION-PLAYBOOK.md) -- strategy selection, authoring mappings, safety model, validation checklist
- [Examples](examples/README.md) -- runnable end-to-end workflow sample (extract -> config -> generate -> analyze -> fix)

## Engineering

- SDK and build conventions: [`docs/ENGINEERING.md`](docs/ENGINEERING.md)
- Architecture: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- Initial milestones: [`docs/PLAN.md`](docs/PLAN.md) and [`docs/WORK-BREAKDOWN.md`](docs/WORK-BREAKDOWN.md)

## License

MIT
