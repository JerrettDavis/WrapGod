# Applying Migrations

`wrap-god migrate apply` runs a [MigrationSchema](schema.md) against a target codebase,
rewrites matching source files, and persists run state so that subsequent invocations are
idempotent.

> **Authoring a schema?** See [Authoring a Migration Schema](authoring.md). This page is for consumers.

---

## Consumer Workflow

The recommended end-to-end workflow is:

```
migrate generate → review schema → migrate apply --dry-run → migrate apply → migrate status → migrate verify
```

### Step 1 — Obtain or generate a schema

If the library author shipped a migration schema (as a file download, NuGet content package, or in their repository), download it. Otherwise, generate a draft:

```bash
wrap-god migrate generate \
  --package MudBlazor \
  --from 6.0.0 --to 7.0.0 \
  --output mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json
```

See [CLI Reference: migrate generate](../guide/cli.md#migrate-generate) for all options.

### Step 2 — Review the schema

Open the schema JSON and check:

- Are the rule IDs meaningful? (Rename them if the auto-generated names are not descriptive.)
- Do any `manual` rules have a useful `note` explaining what to do? If not, add one before running.
- Are there spurious `auto` rules that look risky? Downgrade them to `manual` or delete them.

### Step 3 — Dry run

Preview what would change without writing any files:

```bash
wrap-god migrate apply \
  --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json \
  --project-dir ./src \
  --dry-run
```

The output shows:
- Which files would be modified
- A unified-diff preview for each file (truncated at 20 lines; full diff written to `.wrapgod/dryrun-<timestamp>.diff`)
- Any `SkippedRewrite` entries that represent ambiguous matches
- Any `Manual` rules that list the files they matched

Review skipped rules: if a rule you expected to apply was skipped, check the reason and either fix the schema or note it for manual remediation.

### Step 4 — Apply

```bash
wrap-god migrate apply \
  --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json \
  --project-dir ./src
```

Files are rewritten atomically. A state file is written next to the schema recording all applied, skipped, and manual entries.

**Tip:** Run this with `--verbose` to see per-file rewrite details.

### Step 5 — Handle manual rules

After `apply`, check the output for `Manual:` entries. These are rules that the engine identified matching files for but never applied automatically. For each:

1. Open the listed files.
2. Read the rule's `note` field for guidance.
3. Make the change by hand.
4. Re-run `migrate apply` — the manual rule will again list its matched files until you've addressed them (manual rules are never auto-applied).

### Step 6 — Check status

```bash
wrap-god migrate status \
  --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json
```

This reads the state file and shows progress: how many rules have been applied, how many were skipped, and which manual rules remain.

Exit code `2` means manual-confidence rules are still present. Exit code `0` means no manual rules remain (or no state file exists).

### Step 7 — Verify

```bash
wrap-god migrate verify \
  --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json \
  --project-dir ./src
```

This optionally builds the project and correlates each compiler diagnostic back to the migration rule that most likely caused it. Use this to triage build errors after the migration.

See [Verifying a Migration](verifying.md) for details.

---

## Prerequisites

1. A schema JSON file produced by [`wrap-god migrate generate`](../guide/cli.md#migrate-generate) or provided by the library author.
2. The target project directory containing `.cs` source files.

---

## Basic Usage

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

---

## Options Reference

| Option | Required | Default | Description |
|--------|----------|---------|-------------|
| `--schema`, `-s` | Yes | — | Path to the migration schema JSON |
| `--project-dir`, `-p` | No | Current dir | Root directory for glob resolution |
| `--include` | No | `**/*.cs` | Glob pattern for files to include (repeatable) |
| `--exclude` | No | `**/bin/**`, `**/obj/**`, `**/.wrapgod/**` | Glob pattern for files to exclude (repeatable) |
| `--dry-run` | No | `false` | Preview mode — no file writes, no state |
| `--json` | No | `false` | Emit JSON summary instead of human-readable text |
| `--verbose`, `-v` | No | `false` | Extra diagnostic output |

---

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `1` | Runtime error (schema not found, parse error, IO failure) |
| `2` | Bad arguments (required flag missing) |

---

## Glob Filtering

Files are discovered using `Microsoft.Extensions.FileSystemGlobbing`. The defaults include all `.cs` files while excluding build output:

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

**Tip:** Start with a narrow `--include` filter (e.g., one subdirectory) to validate the schema on a subset of your codebase before running on the whole project.

---

## Idempotent Re-runs

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

---

## Handling Manual Rules

Rules with `confidence: "manual"` are **never applied automatically**. They are collected
and shown in the output as requiring human intervention:

```
Manual:
  MANUAL-001 Parameters restructured -- requires manual mapping
    matched in: src/Dialogs/Confirm.cs, src/Dialogs/Edit.cs
```

Use this information to guide manual edits after the automated apply step. Re-running `migrate apply` will re-list the same manual rules until you've removed the matching patterns (or explicitly deleted the rule from the schema).

---

## Handling Skipped Rules

A `SkippedRewrite` means a rule was evaluated but not applied:

```
Skipped:
  MUD-017 src/Dialogs/Confirm.cs:42  Ambiguous: two overloads of Show() in scope
```

Common skip reasons:

| Reason | What it means | What to do |
|--------|---------------|-----------|
| `Ambiguous: …` | Receiver type could not be inferred syntactically | Add explicit type annotation at the call site, or change the rule to `confidence: "manual"` |
| `no rewriter for kind '…'` | Rule kind not recognized by the engine version | Update to a version of WrapGod that supports the rule kind |
| `type change requires semantic conversion` | `changeParameter` found a type-change that requires value conversion | Handle manually |
| Return value consumed | `splitMethod` call site returns a value | Handle manually |
| Chained call | `splitMethod` or `moveMember` call is chained | Handle manually |

---

## State File Lifecycle

The state file records a history of applied rewrites. Key behaviors:

- **`applied` list** — append-only, de-duplicated by `(ruleId, file)`. Persists across runs.
- **`skipped` list** — replaced wholesale on each run. Reflects the most recent run only.
- **`manual` list** — replaced wholesale on each run. Reflects the current schema's manual rules.

See [Migration State](state.md) for the full state file format and hash semantics.

### State file corruption recovery

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

---

## JSON Output Mode

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

---

## Dry-run Diff Preview

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

---

## CI Integration

```yaml
# Example GitHub Actions snippet — dry-run check
- name: Check migration (dry-run)
  run: |
    wrap-god migrate apply \
      --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json \
      --project-dir ./src \
      --dry-run \
      --json > migration-result.json
    cat migration-result.json

- name: Fail if manual rules remain
  run: |
    MANUAL=$(jq '.manual' migration-result.json)
    if [ "$MANUAL" -gt "0" ]; then
      echo "Manual rules remain — human intervention required"
      exit 1
    fi
```

For CI pipelines where you want to apply the migration and verify the build:

```yaml
- name: Apply migration
  run: wrap-god migrate apply --schema schema.json --project-dir ./src

- name: Verify migration
  run: wrap-god migrate verify --schema schema.json --project-dir ./src --json
```

---

## See Also

- [Migration Schema](schema.md) — schema model and rule kinds
- [Authoring a Migration Schema](authoring.md) — for library maintainers
- [Migration State](state.md) — state file format and hash semantics
- [Verifying a Migration](verifying.md) — post-apply build correlation
- [CLI Reference: migrate apply](../guide/cli.md#migrate-apply) — full flag reference
- [CLI Reference: migrate generate](../guide/cli.md#migrate-generate) — how to produce the schema
- [Back to Migration index](./index.md)
