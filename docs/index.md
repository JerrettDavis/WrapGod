# WrapGod Documentation

WrapGod generates type-safe wrapper interfaces over third-party .NET APIs so your application code never touches vendor types directly. Upgrade, swap, or support multiple versions — without refactoring a single line.

## Getting Started

- [Quick Start](QUICKSTART.md) — zero to generated wrappers in 5 minutes
- [CLI Reference](CLI.md) — all 9 commands with options and examples
- [Configuration](CONFIGURATION.md) — JSON, attributes, fluent DSL, merge precedence
- [MSBuild Integration](MSBUILD-INTEGRATION.md) — zero-touch build pipeline with WrapGod.Targets

## Core Concepts

- [Architecture](ARCHITECTURE.md) — pipeline overview: Extract → Configure → Generate → Analyze
- [Manifest Format](MANIFEST.md) — API surface schema, type and member nodes
- [Manifest Schema](MANIFEST-SCHEMA.md) — JSON schema reference
- [Compatibility Modes](COMPATIBILITY.md) — LCD, Targeted, and Adaptive strategies
- [Analyzers & Code Fixes](ANALYZERS.md) — WG2001, WG2002 diagnostics and auto-migration

## Migration

- [Migration Playbook](MIGRATION-PLAYBOOK.md) — strategy selection, safety model, validation checklist
- [Automation Guide](AUTOMATION.md) — end-to-end automation for eliminating library tech debt

## API Reference

- [API Documentation](api/index.md) — generated API reference for all WrapGod packages

## Engineering

- [Engineering Guide](ENGINEERING.md) — SDK, build conventions, project structure
- [Coverage Policy](COVERAGE-POLICY.md) — 90% per-package line coverage gate
- [Quality Matrix](QUALITY-MATRIX.md) — quality metrics and standards
