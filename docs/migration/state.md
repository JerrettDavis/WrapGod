# Migration State

`WrapGod.Migration.Engine.State` implements persistent state tracking so that
`apply` runs are **idempotent** — re-running on the same codebase skips rules
that have already been applied.

## State file location

The state file sits next to the schema file and uses the schema filename with
`.state.json` appended:

```
migrations/
  mudblazor.6.0-to-7.0.wrapgod-migration.json          ← schema
  mudblazor.6.0-to-7.0.wrapgod-migration.json.state.json  ← state (auto-created)
```

The `MigrationStateStore.GetStatePath(schemaPath)` helper computes this path.

> **Git guidance:** commit the state file alongside the schema. This makes
> the migration history visible in pull requests and enables `migrate status`
> to show progress across branches.

## State schema (JSON)

```json
{
  "schema": "path/to/schema.json",
  "schemaHash": "sha256:a3f1c9...",
  "startedAt": "2025-06-01T00:00:00+00:00",
  "lastRunAt":  "2025-06-01T12:34:56+00:00",
  "summary": {
    "totalRules": 5,
    "applied": 3,
    "skipped": 1,
    "manual": 1
  },
  "applied": [
    {
      "ruleId": "MUD-001",
      "file": "src/Components/MyButton.razor.cs",
      "line": 14,
      "originalText": "MudButton",
      "replacedWith": "MudButtonEx"
    }
  ],
  "skipped": [
    {
      "ruleId": "MUD-002",
      "file": "src/Pages/Index.razor.cs",
      "line": 0,
      "reason": "no match found"
    }
  ],
  "manual": [
    {
      "ruleId": "MUD-003",
      "note": "Manually rename constructor parameter 'Color' to 'ButtonColor'",
      "matchedFiles": ["src/Pages/Custom.razor.cs"]
    }
  ]
}
```

### Field reference

| Field | Type | Description |
|-------|------|-------------|
| `schema` | string | Path to the schema file. Used for orphan detection. |
| `schemaHash` | string | SHA-256 hash of schema content (see [Hash semantics](#hash-semantics)). |
| `startedAt` | ISO-8601 | Timestamp of the first `apply` run for this schema. |
| `lastRunAt` | ISO-8601 | Timestamp of the most recent `apply` run. |
| `summary` | object | Aggregated counts from the last run. |
| `applied[]` | array | Rewrites applied across all runs (append-only, de-duped). |
| `skipped[]` | array | Rewrites skipped during the most recent run (replaced each run). |
| `manual[]` | array | Manual-confidence rules identified during the most recent run (replaced each run). |

## Hash semantics

Before hashing, schema content is normalised:

1. CRLF and bare CR line endings are replaced with LF.
2. Trailing whitespace is trimmed from each line.

This makes the hash insensitive to git's `autocrlf` setting and editor-introduced
trailing spaces.

**Important:** reordering rules in the schema **does** change the hash (the hash
is content-sensitive, not semantically aware). A changed hash causes all rules to
be re-evaluated on the next run.

```csharp
string hash = MigrationStateStore.ComputeSchemaHash(schemaJson);
// Returns: "sha256:a3f1c9..."
```

## Idempotency guarantees

On each `apply` run `StatefulMigrationEngine`:

1. Loads the state file (returns `null` if missing or corrupt).
2. Computes the current schema hash.
3. Compares hashes:
   - **Same hash** → skips `(ruleId, file)` pairs already in `applied`.
   - **Different hash** → re-evaluates all rules (schema changed).
4. Merges the new run result into the existing state:
   - `applied` — append-only, de-duplicated by `(ruleId, file)`.
   - `skipped` — replaced wholesale.
   - `manual` — replaced wholesale.
5. Writes the merged state atomically (`.tmp` file → rename).

## List semantics in detail

| List | Policy | Rationale |
|------|--------|-----------|
| `applied` | Append-only, de-dup by `(ruleId, file)` | Preserves history across partial runs. |
| `skipped` | Replaced each run | Skipped reasons can change as rewriters improve. |
| `manual` | Replaced each run | Reflects current schema's manual rules. |

## Recovery from corruption

If the state file contains invalid JSON (e.g. interrupted write from a previous
run), `MigrationStateStore.Load(schemaPath, out wasCorrupt, out backupPath)`:

1. Archives the corrupt file to `{name}.state.json.bak` (overwriting any prior
   `.bak`).
2. Sets `wasCorrupt = true` and `backupPath = <archived path>`.
3. Returns `null` so callers can re-run from scratch.

`StatefulMigrationEngine.ApplyWithState` consumes this and emits a synthetic
`SkippedRewrite` into the returned `MigrationResult`:

| Field | Value |
|-------|-------|
| `RuleId` | `"<state>"` |
| `File` | `"<state>"` |
| `Line` | `0` |
| `Reason` | `"State file was corrupt and archived to <bakpath>. Re-evaluating all rules."` |

This makes the recovery visible to downstream consumers such as
`migrate status` (#200), audit logs, and CI output.

The atomic write strategy (write to `.tmp`, then `File.Move`) minimises the
chance of corruption in the first place. If `File.Move` itself fails (e.g.
destination locked or replaced by a directory), the `.tmp` orphan is deleted on
a best-effort basis before the exception propagates so no stale `.tmp` files
accumulate on disk.

## Public API

```csharp
// Locate the state file
string statePath = MigrationStateStore.GetStatePath(schemaPath);

// Load (null if missing/corrupt)
MigrationState? state = MigrationStateStore.Load(schemaPath);

// Load with corruption-recovery signalling
MigrationState? state = MigrationStateStore.Load(
    schemaPath, out bool wasCorrupt, out string? backupPath);

// Save (atomic, creates parent dirs)
MigrationStateStore.Save(schemaPath, state);

// Compute hash
string hash = MigrationStateStore.ComputeSchemaHash(schemaJson);

// Check if a rule+file was already applied
bool skip = state.IsAlreadyApplied(ruleId, filePath);

// Check if schema changed
bool changed = state.SchemaHasChanged(currentHash);

// Produce updated state after a run
MigrationState updated = state.Merge(migrationResult, currentHash);

// High-level stateful engine
var stateful = new StatefulMigrationEngine(engine);
MigrationResult result = stateful.ApplyWithState(schemaPath, schema, files);
MigrationResult dryResult = stateful.DryRunWithState(schemaPath, schema, files);
```

## Example: sample state file

See [`docs/migration/examples/sample.state.json`](examples/sample.state.json) for a
representative state file.

## See also

- [Migration Engine](engine.md) — `MigrationEngine`, `IRuleRewriter`, `RewriteContext`
- [Migration Schema](schema.md) — schema model and rule kinds
- [`migrate status` CLI command](../guide/cli.md#migrate-status) — read-only progress report from the state file
