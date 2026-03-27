# WrapGod

WrapGod is a .NET platform for generating wrapper interfaces, facades, adapters, and migration tooling over third-party APIs.

## Vision

WrapGod helps teams:

- extract full API manifests from external assemblies (including multiple versions)
- define mapping rules via attributes, fluent config, and JSON
- source-generate wrappers/facades/proxies onto internal contracts
- auto-migrate direct third-party usages via Roslyn analyzers + code fixes
- map third-party types to internal types with generated mappers

## Solution Structure

- `WrapGod.Abstractions` - public attributes and contracts
- `WrapGod.Manifest` - manifest schema + version/diff models
- `WrapGod.Extractor` - assembly interrogation + manifest generation
- `WrapGod.Generator` - incremental source generator for wrappers/facades
- `WrapGod.TypeMap` - type mapping contracts and mapping planner
- `WrapGod.Analyzers` - diagnostics and code fixes for migrations
- `WrapGod.Fluent` - fluent configuration DSL
- `WrapGod.Runtime` - optional runtime helpers/adapters
- `WrapGod.Tests` - unit/integration/snapshot tests

## Engineering

- SDK + build conventions: `docs/ENGINEERING.md`
- Initial milestones: `docs/PLAN.md` and `docs/WORK-BREAKDOWN.md`
