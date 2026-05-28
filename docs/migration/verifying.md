# Verifying a Migration

After running `migrate apply`, the `migrate verify` command optionally compiles the migrated project and correlates each compiler diagnostic back to the migration rule that most likely caused it.

Verification is always **informational** — it never blocks `apply` and never modifies any file.

> **See also:** [CLI reference: migrate verify](../guide/cli.md#migrate-verify)

---

## When to Run Verify

Run `verify` after a successful `apply` when:

- You want to know whether the migration introduced any new compiler errors.
- You need to decide which rules still need manual remediation.
- You are in a CI pipeline and want a structured JSON report of migration health.

You can safely skip `verify` if your build already passes; it adds the attribution layer on top of a build you would run anyway.

---

## Attribution Algorithm

A compiler diagnostic at `(file F, line L)` is **attributed** to a migration rewrite `R` when:

1. `R.File == F` (case-insensitive comparison on Windows; case-sensitive on Linux/macOS), AND
2. `|R.Line − L| ≤ 3`

This ±3 line window accounts for the fact that a rewrite shifts surrounding lines slightly and that the compiler may report the error on a nearby line rather than the exact rewrite location.

### Tiebreak

When multiple rewrites fall within 3 lines of the same diagnostic:

1. The one with the **smallest absolute distance** wins.
2. If two rewrites are equidistant, the one appearing **later** in the state file (i.e. most recently applied) wins.

### Unattributed diagnostics

Diagnostics that do not match any applied rewrite within ±3 lines are reported as **unattributed**. These are likely:

- Pre-existing errors that existed before the migration.
- Errors caused by something outside the scope of the applied rules.

### Pre-existing diagnostics (baseline)

Pass `--baseline <path>` with a JSON file capturing diagnostics from a pre-migration build. Any diagnostic that matches the baseline by `(file, line, code)` tuple is classified as **pre-existing** and excluded from the attribution counts.

---

## Baseline Workflow

1. **Before** running `migrate apply`, capture the current build diagnostics:

   ```bash
   dotnet build --nologo 2>&1 | grep -E '\(([0-9]+),[0-9]+\): (error|warning)' > pre-migration.txt
   ```

   Or, if your tooling produces Cobertura/SARIF, export that instead. The `--baseline` flag expects a JSON array with the shape:

   ```json
   [
     { "filePath": "src/Foo.cs", "line": 42, "column": 5, "severity": "error", "code": "CS0103", "message": "..." },
     ...
   ]
   ```

2. Apply the migration:

   ```bash
   wrap-god migrate apply --schema schema.json --project-dir ./src
   ```

3. Verify with the baseline:

   ```bash
   wrap-god migrate verify \
     --schema schema.json \
     --project-dir ./src \
     --baseline pre-migration.json
   ```

   Diagnostics present in both the current build and the baseline are reported as **pre-existing** rather than attributed to a rule.

---

## `--no-build` Mode

Pass `--no-build` to skip the `dotnet build` invocation entirely. In this mode, `verify` only reports the state-file summary (applied / skipped / manual counts) without any diagnostic correlation.

This is useful in CI pipelines where the build runs as a dedicated parallel job and you want the verify step to remain cheap.

---

## Graceful Degradation

| Situation | Behaviour |
|-----------|-----------|
| `dotnet` not on PATH | Exits 0; stderr note: "dotnet build not found; skipping verify." |
| No state file found | Exits 0; stderr note: "No migration state; nothing to verify." |
| `--baseline` path does not exist | Exits 1 with error message. |
| Project does not compile at all | Exits 0; reports the full diagnostic list with whatever attribution is possible. |
| Schema has changed since last apply | Prints a warning banner; continues with attribution using current state. |

---

## JSON Output

Pass `--json` to emit a machine-readable report:

```json
{
  "schema": "mudblazor.6.0-to-7.0.wrapgod-migration.json",
  "projectDir": "./src",
  "schemaChanged": false,
  "noBuild": false,
  "build": {
    "exitCode": 1,
    "launched": true,
    "errors": 50,
    "warnings": 3
  },
  "baselineDiagnosticsLoaded": 0,
  "state": {
    "applied": 47,
    "skipped": 6,
    "manual": 3
  },
  "attribution": [
    {
      "ruleId": "MUD-003",
      "errors": 38,
      "warnings": 0,
      "diagnostics": [
        {
          "file": "src/Dialogs/ConfirmDialog.cs",
          "line": 42,
          "code": "CS1501",
          "message": "No overload for method 'Show' takes 2 arguments",
          "severity": "error"
        }
      ]
    }
  ],
  "unattributed": [
    {
      "file": "src/Other.cs",
      "line": 100,
      "code": "CS0618",
      "message": "deprecated",
      "severity": "warning"
    }
  ]
}
```

### Key fields

| Field | Description |
|-------|-------------|
| `build.exitCode` | Raw `dotnet build` exit code (0 = success) |
| `attribution[]` | Errors/warnings grouped by the rule that likely caused them |
| `attribution[].diagnostics[]` | Per-diagnostic details for `--verbose`-level inspection |
| `unattributed[]` | Diagnostics not matched to any rewrite within ±3 lines |
| `baselineDiagnosticsLoaded` | Count of pre-existing diagnostics loaded from `--baseline` |

---

## Common Gotchas

### `--no-restore` assumption

`verify` passes `--no-restore` to `dotnet build` to keep the invocation fast. In a fresh CI checkout, ensure a `dotnet restore` step runs before `verify`.

### Schema drift

If you edit the schema JSON after running `apply`, the stored state file's hash will no longer match. `verify` warns about this with:

```
WARNING: Schema has changed since last apply. Attribution may be inaccurate.
```

Re-run `migrate apply` to regenerate the state before running `verify` again.

### Case-sensitive paths on Linux

The ±3-line match normalises path separators (`\` → `/`) and uses case-insensitive comparison on Windows. On Linux and macOS the comparison is case-sensitive. Ensure the paths recorded in the state file match the actual filesystem casing — this is most commonly an issue when the schema was authored on Windows and the build runs on Linux.

---

> **Back to:** [Migration index](./index.md) · [State file format](./state.md) · [CLI reference](../guide/cli.md#migrate-verify)
