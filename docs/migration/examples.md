# Migration Examples

End-to-end, CI-validated examples of `wrap-god migrate` applied to real-world library upgrades.

## Library upgrade migrations

| Example | From → To | Rule kinds | Status |
|---------|-----------|-----------|--------|
| [Serilog v2 → v3](https://github.com/JerrettDavis/WrapGod/tree/main/examples/migrations/serilog-v2-to-v3) | Serilog 2.12.0 → 3.1.1 | `renameNamespace`, `renameMember`, `removeMember` | Verified |

### Serilog v2 → v3

The first end-to-end example demonstrating the migration engine pipeline (generate → review → dry-run → apply → status → verify) on a real library upgrade. It exercises the engine's namespace-rename rewriter, its member-rewrite reporting for fluent-chain receivers (manual confidence), and its removal-audit reporting (manual confidence).

**Directory:** [`examples/migrations/serilog-v2-to-v3/`](https://github.com/JerrettDavis/WrapGod/tree/main/examples/migrations/serilog-v2-to-v3)

**What it demonstrates:**

- The `RenameNamespace` rewriter collapses `using Serilog.Sinks.RollingFile;` onto an existing `using Serilog;` directive — exercising the engine's post-pass duplicate-using cleanup (added in PR #218).
- The `RenameMember` rewriter correctly defers fluent-chain receivers (`WriteTo.RollingFile`) to the manual list when the receiver type cannot be syntactically resolved.
- The `RemoveMember` rule kind illustrates an audit pass — every match is surfaced for human review without being automatically removed.
- A standalone `MigrationTests` project under the example runs the engine in CI on every push, copying `before/` to a temp dir, applying the schema, and asserting parity against `after/` (byte-equal modulo LF normalisation).

**Workflow:**

```bash
cd examples/migrations/serilog-v2-to-v3

# 1. Generate a draft schema (skipped here — the committed schema was hand-authored
#    so reviewers can see authoring conventions explicitly; uncomment to re-generate):
# dotnet run --project ../../../WrapGod.Cli -- migrate generate \
#     --package Serilog --from 2.12.0 --to 3.1.1 \
#     --output schema/serilog.2.x-to-3.x.wrapgod-migration.json

# 2. Dry-run preview
dotnet run --project ../../../WrapGod.Cli -- migrate apply \
    --schema schema/serilog.2.x-to-3.x.wrapgod-migration.json \
    --project-dir ./before \
    --dry-run

# 3. Apply (operates on a temp copy in the example's scripts; never mutates before/)
./scripts/run-migration.ps1 -Apply       # PowerShell
./scripts/run-migration.sh --apply       # Bash
```

**CI integration:** see `.github/workflows/examples.yml`, job `build-examples`, step *"Test Serilog v2-to-v3 migration parity"*.

**Read the full walkthrough:** [`examples/migrations/serilog-v2-to-v3/README.md`](https://github.com/JerrettDavis/WrapGod/blob/main/examples/migrations/serilog-v2-to-v3/README.md)

## Cross-library bidirectional examples

These earlier examples compare two libraries side-by-side rather than demonstrating a version upgrade. They are listed here for completeness.

| Example | Direction | Folder |
|---------|-----------|--------|
| Serilog ↔ NLog | bidirectional | `examples/migrations/serilog-nlog-bidirectional/` |
| AutoMapper ↔ Mapster | bidirectional | `examples/migrations/automapper-mapster-bidirectional/` |
| EF Core ↔ Dapper | bidirectional | `examples/migrations/efcore-dapper-bidirectional/` |
| MediatR ↔ MassTransit Mediator | bidirectional | `examples/migrations/mediatr-masstransit-mediator-bidirectional/` |

## State file reference

A representative state file is checked in at [`docs/migration/examples/sample.state.json`](examples/sample.state.json). The Serilog example's committed state file lives next to its schema at [`examples/migrations/serilog-v2-to-v3/schema/serilog.2.x-to-3.x.wrapgod-migration.json.state.json`](https://github.com/JerrettDavis/WrapGod/blob/main/examples/migrations/serilog-v2-to-v3/schema/serilog.2.x-to-3.x.wrapgod-migration.json.state.json) — see [State](state.md) for the file format.
