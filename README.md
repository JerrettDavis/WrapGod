# WrapGod

**Stop rewriting code when libraries change.** WrapGod generates type-safe wrapper interfaces over third-party .NET APIs so your application code never touches vendor types directly. Upgrade, swap, or support multiple versions — without refactoring a single line.

[![CI](https://github.com/JerrettDavis/WrapGod/actions/workflows/ci.yml/badge.svg)](https://github.com/JerrettDavis/WrapGod/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JerrettDavis/WrapGod/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/JerrettDavis/WrapGod/actions/workflows/codeql-analysis.yml)
[![Examples](https://github.com/JerrettDavis/WrapGod/actions/workflows/examples.yml/badge.svg)](https://github.com/JerrettDavis/WrapGod/actions/workflows/examples.yml)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## The Problem

Every .NET team has been burned by this: you upgrade a NuGet package and half your solution lights up red. Method signatures changed, types moved namespaces, a property you relied on quietly disappeared. Your team spends the next two weeks hunting down every call site, updating tests, and hoping nothing slips through to production.

This isn't a one-time pain — it's a recurring tax. Every third-party library your codebase touches is a liability. Serilog, AutoMapper, MediatR, your HTTP client, your ORM. Each one has its own release cadence, its own breaking-change philosophy, and its own upgrade timeline. Multiply that across microservices and a routine upgrade becomes a multi-sprint project.

The root cause is simple: your application code is coupled directly to vendor APIs. Every `new JsonSerializer()`, every `ILogger.LogInformation()` call, every extension method — they're all hard dependencies on someone else's type system. When that type system changes, your code breaks.

## How WrapGod Solves It

WrapGod generates thin wrapper interfaces and facade classes between your code and vendor libraries. Your application talks to `IWrappedJsonSerializer` instead of `JsonSerializer` directly. When the vendor ships a breaking change, you regenerate the wrappers — your code stays untouched.

This isn't another abstraction layer you write and maintain by hand. WrapGod extracts the full public API surface of any .NET assembly into a manifest, then a Roslyn source generator emits the wrapper code at build time. The wrappers delegate straight through to the real types — zero runtime overhead, zero boilerplate to maintain.

The entire pipeline runs as part of your normal `dotnet build`. You configure once, and the build system handles everything. Upgrading a vendor library is changing a version number.

## Quick Start

The fastest path from nothing to generated wrappers:

```bash
dotnet add package WrapGod.Generator
dotnet add package WrapGod.Analyzers
```

Add your manifest as an `AdditionalFile` in your `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="vendor.wrapgod.json" />
</ItemGroup>
```

Then build:

```bash
dotnet build  # wrappers generated automatically
```

For the full walkthrough — extracting manifests, configuring rules, running analyzers — see the [Quick Start Guide](docs/guide/quickstart.md).

## CLI

| Command | Description |
|---------|-------------|
| `wrap-god extract` | Extract a manifest from any .NET assembly |
| `wrap-god analyze` | Detect direct vendor usage in your codebase |
| `wrap-god diff` | Compare two manifest versions and report breaking changes |
| `wrap-god merge` | Merge multiple version manifests into one |

Install: `dotnet tool install -g WrapGod.Cli`

See [CLI Reference](docs/guide/cli.md) for full usage and options.

## The Pipeline

WrapGod works in four stages, all wired into your build automatically:

```
  Assembly DLL(s)
       |
       v
  [ Extract ]  ──  Interrogate public API surface  ──>  ApiManifest (JSON)
       |
       v
  [ Configure ]  ──  JSON / Attributes / Fluent DSL  ──>  Wrapper rules
       |
       v
  [ Generate ]  ──  Roslyn incremental source gen  ──>  IWrapped*.g.cs + *Facade.g.cs
       |
       v
  [ Analyze ]  ──  Detect direct vendor usage  ──>  WG2001/WG2002 diagnostics + auto-fixes
```

**Extract** reads any .NET assembly and produces a complete JSON manifest of its public API surface — types, methods, properties, events, version metadata. **Configure** lets you define wrapper rules through JSON config files, C# attributes (`[WrapType]`, `[WrapMember]`), or a fluent DSL. **Generate** is a Roslyn incremental generator that emits `IWrapped{Type}` interfaces and `{Type}Facade` delegating classes at build time. **Analyze** catches any direct usage of wrapped vendor types and offers one-click code fixes to migrate call sites.

## Compatibility Modes

| Mode | What it does | When to use it |
|------|-------------|----------------|
| **LCD** | Lowest common denominator — generates wrappers using only the API surface shared across all extracted versions | Maximum portability across version ranges |
| **Targeted** | Pinned to a single version's API surface | You target one specific vendor version |
| **Adaptive** | Runtime version guards for members that differ between versions | Multi-version deployments where different services run different versions |

See [Compatibility Modes](docs/guide/compatibility.md) for configuration details.

## Examples

| Example | What it shows |
|---------|---------------|
| [WorkflowDemo](examples/WrapGod.WorkflowDemo/) | End-to-end pipeline: extract, configure, generate, analyze, fix |
| [Serilog/NLog](examples/migrations/serilog-nlog-bidirectional/) | Bidirectional logging framework migration with parity tests |
| [AutoMapper/Mapster](examples/migrations/automapper-mapster-bidirectional/) | Bidirectional object-mapping migration with parity tests |
| [EF Core/Dapper](examples/migrations/efcore-dapper-bidirectional/) | Service-boundary ORM migration with parity tests |
| [MediatR/MassTransit](examples/migrations/mediatr-masstransit-mediator-bidirectional/) | Mediator pattern migration with parity tests |
| [NuGet Version Matrix](examples/migrations/nuget-version-matrix/) | Version-divergence packs with compatibility reports and CI drift guards |

Run the workflow demo:

```bash
dotnet run --project examples/WrapGod.WorkflowDemo/WrapGod.WorkflowDemo.csproj
```

See [examples/README.md](examples/README.md) for the full catalog.

## Documentation

- [Quick Start Guide](docs/guide/quickstart.md) — end-to-end onboarding walkthrough
- [CLI Reference](docs/guide/cli.md) — command-line tool usage and options
- [MSBuild Integration](docs/guide/msbuild-integration.md) — zero-touch build automation
- [Manifest Format](docs/guide/manifest.md) — schema, type/member nodes, version metadata
- [Configuration Guide](docs/guide/configuration.md) — JSON, attributes, fluent DSL, merge precedence
- [Compatibility Modes](docs/guide/compatibility.md) — LCD, Targeted, and Adaptive modes
- [Analyzer Diagnostics](docs/guide/analyzers.md) — WG2001, WG2002, code fixes, suppression
- [Migration Playbook](docs/migration/playbook.md) — strategy selection, authoring mappings, safety model
- [Architecture](docs/guide/architecture.md) — pipeline internals and design decisions
- [Engineering](docs/engineering/engineering.md) — SDK, build conventions, contribution guidelines

## Solution Structure

| Project | Description |
|---------|-------------|
| `WrapGod.Abstractions` | Public attributes (`[WrapType]`, `[WrapMember]`) and configuration contracts |
| `WrapGod.Manifest` | Manifest schema, serialization, JSON config loader, attribute reader, merge engine |
| `WrapGod.Extractor` | Assembly interrogation, single- and multi-version manifest extraction |
| `WrapGod.Generator` | Roslyn incremental source generator for wrapper interfaces and facades |
| `WrapGod.TypeMap` | Type mapping contracts, planner, and mapper emitter |
| `WrapGod.Analyzers` | Roslyn diagnostics (WG2001, WG2002) and automatic code fixes |
| `WrapGod.Fluent` | Fluent configuration DSL |
| `WrapGod.Cli` | Command-line tool (`wrap-god extract`, `wrap-god analyze`, `wrap-god diff`) |
| `WrapGod.Targets` | MSBuild targets for zero-touch build integration |
| `WrapGod.Runtime` | Optional runtime helpers and adapters |
| `WrapGod.Tests` | Unit, integration, and snapshot tests |

## Contributing

Contributions are welcome. See [ENGINEERING.md](docs/engineering/engineering.md) for build setup, coding conventions, and the test strategy. The project uses .NET 10 SDK and targets `net10.0`.

Open an issue first for anything beyond trivial fixes — it helps coordinate effort and avoids wasted work.

## License

[MIT](LICENSE) — Copyright (c) 2025-2026 Jerrett Davis
