# Applying Migrations

`wrap-god migrate apply` runs a [MigrationSchema](schema.md) against a target codebase,
rewrites matching source files, and persists run state so that subsequent invocations are
idempotent.

## Prerequisites

1. A schema JSON file produced by [`wrap-god migrate generate`](../guide/cli.md#migrate-generate).
2. The target project directory containing `.cs` source files.

## Basic usage

```bash
# Preview what would change (no files written, no state saved)
wrap-god migrate apply \
  --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json \
  --project-dir ./src \
  --dry-run

# Apply for real
wrap-god migrate apply \
  --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json \
  --project-dir ./src
```

## Options reference

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--schema`, `-s` | Yes | — | Path to the migration schema JSON |
| `--project-dir`, `-p` | No | Current dir | Root directory for glob resolution |
| `--include` | No | `**/*.cs` | Glob pattern for files to include (repeatable) |
| `--exclude` | No | `**/bin/**`, `**/obj/**`, `**/.wrapgod/**` | Glob pattern for files to exclude (repeatable) |
| `--dry-run` | No | `false` | Preview mode — no file writes, no state |
| `--json` | No | `false` | Emit JSON summary instead of human-readable text |
| `--verbose`, `-v` | No | `false` | Extra diagnostic output |

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Runtime error (schema not found, parse error, IO failure) |
| `2` | Bad arguments (required flag missing) |

## Glob filtering

Files are discovered using `Microsoft.Extensions.FileSystemGlobbing`. The defaults
include all `.cs` files while excluding build output:

```bash
# Include only files under a specific directory
wrap-god migrate apply \
  --schema myschema.json \
  --project-dir ./src \
  --include "**/Components/**"

# Exclude generated files
wrap-god migrate apply \
  --schema myschema.json \
  --project-dir ./src \
  --exclude "**/Generated/**"

# Both (multiple flags can be combined)
wrap-god migrate apply \
  --schema myschema.json \
  --project-dir ./src \
  --include "**/Components/**" \
  --include "**/Pages/**" \
  --exclude "**/Generated/**"
```

## Idempotent re-runs

The command is designed to be run multiple times safely. After each successful run, a
state file is written next to the schema:

```
mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json
mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json.state.json   ← written after apply
```

On subsequent runs, already-applied `(ruleId, file)` pairs are skipped. The second run
reports `0 modified`.

**Schema change detection:** The state file contains a SHA-256 hash of the schema
content. If you edit the schema (add/remove rules), the hash changes and all rules are
re-evaluated from scratch. Old applied entries that still match in the new run are
de-duplicated; entries that no longer match are dropped.

**Recommended workflow:** Commit the state file alongside the schema so your team has
visibility into which rules have been applied and which remain.

## Manual rules

Rules with `confidence: "manual"` are **never applied automatically**. They are collected
and shown in the output as requiring human intervention:

```
Manual:
  MANUAL-001 Parameters restructured -- requires manual mapping
    matched in: src/Dialogs/Confirm.cs, src/Dialogs/Edit.cs
```

Use this information to guide manual edits after the automated apply step.

## State file corruption recovery

If the state file contains invalid JSON (e.g., was truncated by an interrupted write), the
command automatically archives it to `{statePath}.bak` and performs a full re-run. The
recovery is **surfaced as a prominent banner** in human-readable output:

```
============================================================
WARNING: Prior state file was corrupt.
  Archived to: /path/to/schema.json.state.json.bak
  Re-evaluating all rules from scratch.
============================================================
```

In `--json` mode the recovery appears as a top-level `stateRecovered` object with an
`archivedTo` field — CI consumers can act on it without parsing the skipped array.

## JSON output mode

Pass `--json` to get a machine-readable summary suitable for CI reporting:

```jsonc
{
  "dryRun": false,
  "filesScanned": 128,
  "filesModified": 22,
  "applied": 38,
  "skipped": 6,
  "manual": 3,
  "stateRecovered": null,   // or { "archivedTo": "...", "note": "..." }
  "skippedDetails": [
    { "ruleId": "MUD-017", "file": "src/Dialogs/Confirm.cs", "line": 42, "reason": "Ambiguous: ..." }
  ],
  "manualDetails": [
    { "ruleId": "MUD-003", "note": "...", "matchedFiles": ["src/Dialogs/Confirm.cs"] }
  ],
  "appliedByRule": [
    { "ruleId": "MUD-001", "kind": "renameType", "fileCount": 8, "count": 12 }
  ],
  "dryRunDiff": null         // populated in --dry-run mode with inline + dump-file paths
}
```

## Dry-run diff preview

When `--dry-run` is set, the human-readable output prints a unified-diff-style preview for
every file that would be modified:

```diff
--- a/src/Components/Widget.cs
+++ b/src/Components/Widget.cs
-    OldWidget w = null;
+    NewWidget w = null;
```

Per-file inline output is truncated at 20 lines. The full per-file diff for the entire run
is written to `<projectRoot>/.wrapgod/dryrun-<UTC-timestamp>.diff` and the truncation hint
references it.

> The unified-diff is intentionally simple (line-level, no Myers/LCS hunking). For more
> precise diffs, point your favorite diff tool at the dump file. A follow-up issue tracks
> upgrading to a richer hunked diff format.

## See also

- [Migration Schema](schema.md) — schema model and rule kinds
- [Migration State](state.md) — state file format and hash semantics
- [CLI Reference: migrate apply](../guide/cli.md#migrate-apply) — full flag reference
- [CLI Reference: migrate generate](../guide/cli.md#migrate-generate) — how to produce the schema
