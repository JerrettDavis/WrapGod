# Migration Engine

The WrapGod Migration Engine automates the mechanical part of upgrading a .NET codebase when a library ships breaking changes. Given a [migration schema](schema.md) that describes what changed — renames, moves, removals, structural restructurings — the engine walks your source files, applies the rules via Roslyn syntax rewriting, and writes the results back atomically.

The engine is syntax-only (no compilation required). It handles broken code gracefully, preserves all whitespace and comments, and injects missing `using` directives automatically.

## Quick Start

```bash
# Generate a draft schema from two NuGet versions
wrap-god migrate generate --package MudBlazor --from 6.0.0 --to 7.0.0

# Preview the changes
wrap-god migrate apply --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json --dry-run

# Apply for real
wrap-god migrate apply --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json

# Check progress
wrap-god migrate status --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json

# Correlate compiler errors to migration rules
wrap-god migrate verify --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json
```

## Documentation

### For Migration Consumers

| Page | What you'll find |
|------|-----------------|
| [Applying Migrations](applying.md) | End-to-end consumer workflow: dry-run, apply, status, verify; glob filtering, CI integration |
| [Migration State](state.md) | State file format, SHA-256 hash semantics, idempotent re-runs, corruption recovery |
| [Verifying a Migration](verifying.md) | Post-apply build correlation: attribution algorithm, baseline workflow, JSON output |

### For Migration Pack Authors

| Page | What you'll find |
|------|-----------------|
| [Authoring a Migration Schema](authoring.md) | All 11 rule kinds with JSON examples and before/after C# snippets; confidence guidelines; authoring workflow; common gotchas |
| [Migration Schema Reference](schema.md) | Canonical JSON format, field reference, serialization API, JSON Schema validation |
| [Schema Generation](schema-generation.md) | `MigrationSchemaGenerator.FromDiff` — diff-to-schema mapping, similarity thresholds, deterministic IDs |

### Engine Internals

| Page | What you'll find |
|------|-----------------|
| [Migration Engine](engine.md) | Architecture diagram, full lifecycle, public API (`MigrationEngine`, `StatefulMigrationEngine`, `IRuleRewriter`, `MigrationResult`), performance, extension points |
| [Rewriters](rewriters.md) | All 11 concrete `IRuleRewriter` implementations with per-rewriter contracts and before/after examples |

### Strategy and Context

| Page | What you'll find |
|------|-----------------|
| [Migration Playbook](playbook.md) | Strategy selection, safety model, validation checklist |
| [Automation Guide](automation.md) | End-to-end automation for eliminating library tech debt |
| [Examples](examples.md) | End-to-end, CI-validated examples (Serilog v2 → v3 upgrade walkthrough + bidirectional comparisons) |

## CLI Reference

The `wrap-god` CLI exposes four `migrate` subcommands:

| Command | Purpose |
|---------|---------|
| [`migrate generate`](../guide/cli.md#migrate-generate) | Generate a draft schema from two library versions (NuGet or local DLLs) |
| [`migrate apply`](../guide/cli.md#migrate-apply) | Apply a schema to a project directory, with dry-run support |
| [`migrate status`](../guide/cli.md#migrate-status) | Read-only progress report from the state file |
| [`migrate verify`](../guide/cli.md#migrate-verify) | Build the project and correlate compiler diagnostics to migration rules |
