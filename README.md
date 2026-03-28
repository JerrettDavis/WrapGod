# WrapGod

**Generate a protective layer between your code and third-party .NET APIs -- so vendor upgrades never break your build again.**

[![CI](https://github.com/JerrettDavis/WrapGod/actions/workflows/ci.yml/badge.svg)](https://github.com/JerrettDavis/WrapGod/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JerrettDavis/WrapGod/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/JerrettDavis/WrapGod/actions/workflows/codeql-analysis.yml)
[![Examples](https://github.com/JerrettDavis/WrapGod/actions/workflows/examples.yml/badge.svg)](https://github.com/JerrettDavis/WrapGod/actions/workflows/examples.yml)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

## The Problem

You upgrade a NuGet package and suddenly half your solution is red. Method signatures changed, types moved namespaces, a property you relied on quietly disappeared. Your team spends the next two weeks hunting down every call site, updating tests, and praying nothing slips through to production.

This isn't a one-time pain. It's a recurring tax. Every third-party library your codebase touches is a liability -- Serilog, AutoMapper, MediatR, your HTTP client, your ORM. Each one has its own release cadence, its own breaking-change philosophy, and its own upgrade timeline. Multiply that across microservices and you're looking at version drift that turns a routine upgrade into a multi-sprint project.

The root cause is simple: your application code is coupled directly to vendor APIs. Every `new JsonSerializer()`, every `ILogger.LogInformation()` call, every extension method -- they're all hard dependencies on someone else's type system. When that type system changes, your code breaks.

## The Solution

WrapGod generates thin wrapper interfaces and facade classes between your code and vendor libraries. Your application talks to `IWrappedJsonSerializer` instead of `JsonSerializer` directly. When the vendor ships a breaking change, you regenerate the wrappers -- your code stays untouched.

This isn't another abstraction layer you have to write and maintain by hand. WrapGod extracts the full public API surface of any .NET assembly into a manifest, then a Roslyn source generator emits the wrapper code at build time. The wrappers delegate straight through to the real types -- zero runtime overhead, zero boilerplate to maintain. You configure what to wrap, what to rename, what to exclude, and the generator handles the rest.

The entire pipeline -- extraction, configuration, generation, analysis -- runs as part of your normal `dotnet build`. Add the MSBuild targets package, declare which vendor packages to wrap, and forget about it. When you bump a version number, the next build regenerates everything automatically.

## How It Works

```
Assembly DLL(s)
      |
      v
  [ Extract ] ── ApiManifest (JSON)
      |
      v
  [ Configure ] ── WrapGodConfig (JSON / Attributes / Fluent DSL)
      |
      v
  [ Generate ] ── IWrapped*.g.cs + *Facade.g.cs (Roslyn source gen)
      |
      v
  [ Analyze ] ── WG2001/WG2002 diagnostics on direct vendor usage
      |
      v
  [ Fix ] ── Auto-migrate call sites via code fixes
```

## Quick Start

### Path A: MSBuild (zero-touch, recommended)

Add the targets package and declare which vendor packages to wrap:

```xml
<ItemGroup>
  <PackageReference Include="WrapGod.Targets" Version="0.1.0-alpha" PrivateAssets="all" />
</ItemGroup>

<ItemGroup>
  <WrapGodPackage Include="Newtonsoft.Json" />
</ItemGroup>
```

Then build:

```bash
dotnet build
```

That's it. Extraction, generation, and analysis happen automatically. See the [MSBuild Integration Guide](docs/MSBUILD-INTEGRATION.md) for configuration options.

### Path B: CLI

```bash
# Install the CLI tool
dotnet tool install -g WrapGod.Cli

# Extract a manifest from any .NET assembly
wrap-god extract --assembly path/to/Vendor.Lib.dll --output vendor.wrapgod.json

# Analyze for direct vendor usage
wrap-god analyze vendor.wrapgod.json
```

### Path C: Programmatic

```csharp
using WrapGod.Extractor;
using WrapGod.Manifest;

// Extract the full public API surface
ApiManifest manifest = AssemblyExtractor.Extract("path/to/Vendor.Lib.dll");

// Serialize to a manifest file
string json = ManifestSerializer.Serialize(manifest);
File.WriteAllText("vendor.wrapgod.json", json);
```

Add the manifest as an `AdditionalFile` in your `.csproj` and the Roslyn generator picks it up on the next build. See the [Quick Start Guide](docs/QUICKSTART.md) for the full walkthrough.

## Key Features

- **Manifest extraction** -- interrogate any .NET assembly and get a complete JSON map of its public API surface. Know exactly what you depend on before you wrap it.
- **Multi-version support** -- extract multiple versions of the same library, merge them into one manifest with version presence metadata, and get a machine-readable diff of breaking changes. Handle upgrades with data, not guesswork.
- **Three configuration surfaces** -- define wrapper rules via JSON config files, C# attributes (`[WrapType]`, `[WrapMember]`), or a fluent DSL. Use whichever fits your workflow; combine them with configurable merge precedence.
- **Incremental source generation** -- a Roslyn incremental generator emits `IWrapped{Type}` interfaces and `{Type}Facade` delegating classes at build time. Thin wrappers, zero runtime cost, nothing to maintain by hand.
- **Compatibility modes** -- control the generated surface with LCD (lowest common denominator for max portability), Targeted (pinned to a single version), or Adaptive (runtime version guards for multi-version deployments).
- **Analyzers and code fixes** -- Roslyn analyzers detect direct usage of wrapped vendor types (`WG2001`) and methods (`WG2002`), then auto-migrate call sites with one-click or Fix All support. Migration at the speed of `dotnet format`.
- **Type mapping** -- plan source-to-destination type transformations for generated wrapper signatures, with custom converter support for complex type translations.
- **MSBuild automation** -- the `WrapGod.Targets` package wires extraction and generation into your build pipeline. Declare packages, build, done.

## The Automation Story

The real power of WrapGod is what happens after initial setup. Once you've added `WrapGod.Targets` to your project and declared which vendor packages to wrap, the entire workflow is automated. There's no CLI to remember, no scripts to maintain, no manual steps in your pipeline.

Your CI builds extract manifests, generate wrappers, and run analyzers on every commit. When a vendor releases a new version, you bump the version number in your `.csproj` and rebuild. The extraction picks up the new API surface, the generator emits updated wrappers, and the analyzers flag any call sites that need attention. What used to be a multi-week migration becomes a version bump and a build.

For teams managing multiple vendor dependencies across microservices, this is the difference between dreading upgrades and treating them as routine. The migration packs in the examples directory show real-world patterns -- Serilog to NLog, AutoMapper to Mapster, EF Core to Dapper -- all with bidirectional facades and parity tests.

## Examples

The [`examples/`](examples/) directory contains runnable demos:

| Example | What it shows |
|---------|---------------|
| [WorkflowDemo](examples/WrapGod.WorkflowDemo/) | End-to-end pipeline: extract, configure, generate, analyze, fix |
| [Serilog/NLog](examples/migrations/serilog-nlog-bidirectional/) | Bidirectional logging framework migration with parity tests |
| [AutoMapper/Mapster](examples/migrations/automapper-mapster-bidirectional/) | Bidirectional object-mapping migration with graph parity tests |
| [EF Core/Dapper](examples/migrations/efcore-dapper-bidirectional/) | Service-boundary ORM migration with parity tests |
| [MediatR/MassTransit](examples/migrations/mediatr-masstransit-mediator-bidirectional/) | Mediator pattern migration with request/notification/pipeline parity |
| [NuGet Version Matrix](examples/migrations/nuget-version-matrix/) | Version-divergence packs with compatibility reports and CI drift guards |

Run the workflow demo from the repo root:

```bash
dotnet run --project examples/WrapGod.WorkflowDemo/WrapGod.WorkflowDemo.csproj
```

## Documentation

- [Quick Start Guide](docs/QUICKSTART.md) -- end-to-end onboarding walkthrough
- [MSBuild Integration](docs/MSBUILD-INTEGRATION.md) -- zero-touch build automation
- [Manifest Format Reference](docs/MANIFEST.md) -- schema, type/member nodes, version metadata
- [Configuration Guide](docs/CONFIGURATION.md) -- JSON, attributes, fluent DSL, merge precedence
- [Compatibility Modes](docs/COMPATIBILITY.md) -- LCD, Targeted, and Adaptive modes
- [Analyzer Diagnostics Reference](docs/ANALYZERS.md) -- WG2001, WG2002, code fixes, suppression
- [Migration Playbook](docs/MIGRATION-PLAYBOOK.md) -- strategy selection, authoring mappings, safety model
- [Architecture](docs/ARCHITECTURE.md) -- pipeline internals and design decisions
- [Engineering](docs/ENGINEERING.md) -- SDK, build conventions, contribution guidelines

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
| `WrapGod.Cli` | Command-line tool (`wrap-god extract`, `wrap-god analyze`) |
| `WrapGod.Targets` | MSBuild targets for zero-touch build integration |
| `WrapGod.Runtime` | Optional runtime helpers and adapters |
| `WrapGod.Tests` | Unit, integration, and snapshot tests |

## Contributing

Contributions are welcome. See [ENGINEERING.md](docs/ENGINEERING.md) for build setup, coding conventions, and the test strategy. The project uses .NET 10 SDK and targets `net10.0` for the host tooling.

## License

[MIT](LICENSE) -- Copyright (c) 2026 Jerrett Davis
