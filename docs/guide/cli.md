# CLI Reference

The `wrap-god` CLI extracts API manifests, analyzes migrations, generates wrappers, and validates project health.

```
wrap-god [command] [options]
```

**Global behavior:** All commands return exit code `0` on success unless documented otherwise.

---

## Commands

- [`init`](#init) -- Bootstrap a new WrapGod project
- [`extract`](#extract) -- Extract API manifest from an assembly or NuGet package
- [`generate`](#generate) -- Show build-time generation instructions
- [`analyze`](#analyze) -- Analyze a manifest and report diagnostics
- [`doctor`](#doctor) -- Validate environment setup and project health
- [`explain`](#explain) -- Show traceability info for a type or member
- [`migrate init`](#migrate-init) -- Analyze a project and generate a migration plan
- [`migrate generate`](#migrate-generate) -- Generate a draft migration schema from two library versions
- [`migrate apply`](#migrate-apply) -- Apply a migration schema to a codebase (with --dry-run support)
- [`migrate status`](#migrate-status) -- Report migration progress from the state file
- [`migrate verify`](#migrate-verify) -- Optionally build the project and correlate compiler diagnostics to migration rules
- [`ci bootstrap`](#ci-bootstrap) -- Generate recommended CI workflow files
- [`ci parity`](#ci-parity) -- Compare CI config against the recommended baseline

---

### `init`

**Synopsis:** Bootstrap a new WrapGod project in the current directory.

**Usage:**
```
wrap-god init [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--source`, `-s` | Assembly source: local file path, NuGet package (`<id>@<version>`), or `@self` | Interactive prompt |
| `--output`, `-o` | Output config file name | `wrapgod.config.json` |

**Behavior:**

Creates a `wrapgod.config.json` config template and a `.wrapgod-cache/` directory (with its own `.gitignore`). If `--source` is omitted, an interactive prompt offers three choices:

1. Local assembly path
2. NuGet package (e.g., `Newtonsoft.Json@13.0.3`)
3. `@self` (wrap in-project types)

Fails if the config file already exists.

**Examples:**

```bash
# Interactive setup
wrap-god init

# Non-interactive with NuGet source
wrap-god init --source "Serilog@4.0.0"

# Custom config file name
wrap-god init --source "@self" --output my-wrapgod.config.json
```

---

### `extract`

**Synopsis:** Extract an API manifest from a .NET assembly or NuGet package.

This is the most-used command. It inspects public API surface and writes a JSON manifest describing every type and member.

**Usage:**
```
wrap-god extract [assembly-path] [options]
```

**Arguments:**

| Argument | Description | Required |
|----------|-------------|----------|
| `assembly-path` | Path to a local `.dll` assembly | No (required if `--nuget` not used) |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--output`, `-o` | Output path for the manifest | `manifest.json` |
| `--nuget` | NuGet package in `<packageId>@<version>` format. Repeatable for multi-version extraction. | -- |
| `--tfm` | Target framework moniker override (e.g., `net8.0`, `netstandard2.0`) | Auto-detected |
| `--source` | Private NuGet feed URL | nuget.org |

**Behavior:**

- **Local assembly mode:** Reads the assembly directly via reflection and writes the manifest.
- **Single NuGet mode:** Downloads the package, resolves the best TFM, extracts and writes the manifest.
- **Multi-version mode:** When multiple `--nuget` flags reference the same package ID with different versions, extracts all versions, merges them into a single manifest, and reports breaking changes between versions.

**Examples:**

```bash
# Extract from a local assembly
wrap-god extract bin/Release/net8.0/MyLib.dll -o mylib.wrapgod.json

# Extract from a NuGet package
wrap-god extract --nuget Serilog@4.0.0 -o serilog.wrapgod.json

# Multi-version extraction (detect breaking changes)
wrap-god extract \
  --nuget FluentAssertions@6.12.0 \
  --nuget FluentAssertions@7.0.0 \
  -o fluent.wrapgod.json

# Override target framework
wrap-god extract --nuget Newtonsoft.Json@13.0.3 --tfm netstandard2.0

# Extract from a private feed
wrap-god extract --nuget MyCompany.Core@2.1.0 \
  --source https://pkgs.dev.azure.com/myorg/_packaging/myfeed/nuget/v3/index.json
```

**Output:**

```
Resolving NuGet package Serilog@4.0.0...
Manifest written to serilog.wrapgod.json
  Types: 42
  Members: 387
```

For multi-version extraction, the output also reports breaking changes:

```
Merged manifest written to fluent.wrapgod.json
  Types: 156
  Members: 1203
  Breaking changes: 23
```

---

### `analyze`

**Synopsis:** Analyze a manifest and report diagnostic information.

**Usage:**
```
wrap-god analyze <manifest-path> [options]
```

**Arguments:**

| Argument | Description | Required |
|----------|-------------|----------|
| `manifest-path` | Path to the WrapGod manifest JSON file | Yes |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--config`, `-c` | Path to the WrapGod config JSON file | -- |
| `--warnings-as-errors` | Treat warning diagnostics as gate failures (exit code 3) | `false` |

**Exit codes:**

| Code | Constant | Meaning |
|------|----------|---------|
| `0` | `Success` | No errors or warnings |
| `1` | `RuntimeFailure` | Manifest not found or deserialization failed |
| `2` | `DiagnosticsError` | Error-level diagnostics detected |
| `3` | `WarningsAsErrors` | Warning diagnostics with `--warnings-as-errors` enabled |

**Behavior:**

Prints a full type breakdown of the manifest (assembly name, version, type count, member count, and per-type details). For Roslyn-level diagnostics (`WG2001`, `WG2002`), add `WrapGod.Analyzers` to your project and build instead.

**Examples:**

```bash
# Basic analysis
wrap-god analyze manifest.wrapgod.json

# With config validation
wrap-god analyze manifest.wrapgod.json --config wrapgod.config.json

# CI gate: fail on warnings
wrap-god analyze manifest.wrapgod.json --warnings-as-errors
```

**Output:**

```
WrapGod Analyzer
----------------------------------------

Assembly:    Serilog
Version:     4.0.0.0
Types:       42
Members:     387

Type breakdown:
  Serilog.ILogger
    Kind: Interface, Members: 12
      - Information (Method)
      - Warning (Method)
      ...
```

---

### `generate`

**Synopsis:** Show instructions for build-time wrapper generation.

**Usage:**
```
wrap-god generate <manifest-path> [options]
```

**Arguments:**

| Argument | Description | Required |
|----------|-------------|----------|
| `manifest-path` | Path to the WrapGod manifest JSON file | Yes |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--config`, `-c` | Path to the WrapGod config JSON file | -- |
| `--output-dir`, `-o` | Output directory (informational only) | `./generated` |

**Behavior:**

This command is **informational**. WrapGod generation is handled by a Roslyn incremental source generator at compile time, not by the CLI. Running this command prints setup instructions:

1. Add `WrapGod.Generator` package
2. Place the manifest as an `AdditionalFile` in the `.csproj`
3. Build the project

**Examples:**

```bash
wrap-god generate manifest.wrapgod.json
wrap-god generate manifest.wrapgod.json --config wrapgod.config.json
```

---

### `doctor`

**Synopsis:** Validate environment setup and project health.

**Usage:**
```
wrap-god doctor [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--project-dir`, `-p` | Project directory to check | Current directory |

**Checks performed:**

| Check | Pass condition | Fix suggestion |
|-------|---------------|----------------|
| .NET SDK | `dotnet --version` succeeds | Install from https://dot.net/download |
| Config file | `wrapgod.config.json` exists and is valid JSON | Run `wrap-god init` |
| Manifest files | At least one `*.wrapgod.json` exists and deserializes | Run `wrap-god extract` |
| Generator reference | `.csproj` contains `WrapGod.Generator` | `dotnet add package WrapGod.Generator` |
| Cache directory | `.wrapgod-cache/` exists | Run `wrap-god init` or create manually |

**Examples:**

```bash
# Check current directory
wrap-god doctor

# Check a specific project
wrap-god doctor --project-dir src/MyProject
```

**Output:**

```
WrapGod Doctor
----------------------------------------

  [PASS] .NET SDK installed: 10.0.100
  [PASS] Config file valid: wrapgod.config.json
  [PASS] Manifest valid: serilog.wrapgod.json (42 types)
  [FAIL] WrapGod.Generator not referenced in any project file
         Fix: Add the generator: dotnet add package WrapGod.Generator
  [PASS] Cache directory exists: .wrapgod-cache/

----------------------------------------
Results: 4 passed, 1 failed

Fix the issues above and run 'wrap-god doctor' again.
```

---

### `explain`

**Synopsis:** Show traceability info for a type or member in the manifest.

**Usage:**
```
wrap-god explain <symbol> [options]
```

**Arguments:**

| Argument | Description | Required |
|----------|-------------|----------|
| `symbol` | Type or member name to look up (e.g., `HttpClient`, `ILogger.LogInformation`) | Yes |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--manifest`, `-m` | Path to the manifest file | Auto-detects `*.wrapgod.json` in current directory |
| `--config`, `-c` | Path to the config file | Auto-detects `wrapgod.config.json` in current directory |

**Behavior:**

Searches the manifest for the symbol by short name, full name, or stable ID (case-insensitive). Shows:

- For **types**: kind, assembly, version, stable ID, member count, generics info, version presence (introduced/removed/changed), generated wrapper names (`IWrapped{Type}` / `{Type}Facade`), and any config overrides.
- For **members**: kind, return type, parameters, assembly, version, and version presence.

If the symbol matches a member name without a type prefix, searches across all types.

**Examples:**

```bash
# Look up a type by short name
wrap-god explain HttpClient

# Look up by fully qualified name
wrap-god explain Serilog.ILogger

# Look up a member
wrap-god explain ILogger.LogInformation

# Explicit manifest path
wrap-god explain HttpClient --manifest serilog.wrapgod.json
```

**Output (type):**

```
WrapGod Explain
----------------------------------------

Type: Serilog.ILogger
  Kind:       Interface
  Assembly:   Serilog
  Version:    4.0.0.0
  StableId:   Serilog.ILogger
  Members:    12

  Generated wrapper:
    Interface: IWrappedILogger
    Facade:    ILoggerFacade
```

**Output (member):**

```
Member: Serilog.ILogger.Information
  Kind:       Method
  Return:     void
  Assembly:   Serilog
  Version:    4.0.0.0
  Parameters:
    String messageTemplate
    Object[] propertyValues
```

---

### `migrate init`

**Synopsis:** Analyze a project and generate a migration plan for adopting WrapGod wrappers.

**Usage:**
```
wrap-god migrate init [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--project-dir`, `-p` | Project directory to analyze | Current directory |
| `--manifest`, `-m` | Path to the manifest file | Auto-detects `*.wrapgod.json` |
| `--output`, `-o` | Output path for the migration plan | `migration-plan.json` |

**Behavior:**

Scans all `.cs` files (excluding `obj/` and `bin/`) for direct usage of types defined in the manifest. Each usage is classified into one of three categories:

| Category | Meaning | Examples |
|----------|---------|----------|
| `safe` | Simple declarations, parameters, fields -- auto-fixable | `ILogger logger = ...` |
| `assisted` | Inheritance, generic constraints -- needs review | `class Foo : HttpClient` |
| `manual` | Reflection, `typeof()`, dynamic usage -- requires human judgment | `typeof(HttpClient)` |

Outputs a `migration-plan.json` with a summary and per-file action items.

**Examples:**

```bash
# Analyze current project
wrap-god migrate init

# Explicit paths
wrap-god migrate init \
  --project-dir src/MyApp \
  --manifest serilog.wrapgod.json \
  --output my-migration.json
```

**Output:**

```
WrapGod Migration Wizard
----------------------------------------

Auto-detected manifest: serilog.wrapgod.json
Loaded 42 types from manifest.
Scanning project: C:\src\MyApp
Found 156 source files to analyze.

Migration Plan Summary
----------------------------------------
  Types to wrap:       18
  Safe auto-fixes:     94
  Assisted fixes:      12
  Manual review needed: 3
  Total actions:       109

Plan written to: migration-plan.json

Next steps:
  1. Review migration-plan.json for categorized actions
  2. Add WrapGod.Analyzers to your project for code fix suggestions
  3. Run 'dotnet build' to see WG2001/WG2002 diagnostics
```

---

---

### `migrate generate`

**Synopsis:** Generate a draft `MigrationSchema` JSON file by extracting and diffing two versions of a NuGet package (or two local assemblies).

**Usage:**
```
wrap-god migrate generate [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--package` | NuGet package ID (e.g., `MudBlazor`). Mutually exclusive with `--from-assembly`/`--to-assembly`. | -- |
| `--from` | Source version (e.g., `6.0.0`). **Required.** | -- |
| `--to` | Target version (e.g., `7.0.0`). **Required.** | -- |
| `--from-assembly` | Path to the baseline DLL. Mutually exclusive with `--package`. | -- |
| `--to-assembly` | Path to the target DLL. Mutually exclusive with `--package`. | -- |
| `--output`, `-o` | Output path for the schema JSON | `{library}.{from}-to-{to}.wrapgod-migration.json` |
| `--source` | Private NuGet feed URL | nuget.org |
| `--tfm` | Target framework moniker override (e.g., `net8.0`) | Auto-detected |
| `--rule-id-prefix` | Prefix for generated rule IDs (e.g., `MUD` → `MUD-001`) | Derived from library name |
| `--no-rename-detection` | Disable rename detection; all removed types/members emit `RemoveMemberRule` | `false` |
| `--json` | Emit the final summary as JSON to stdout | `false` |

**Modes (mutually exclusive):**

- **NuGet mode:** Supply `--package`, `--from`, and `--to`. WrapGod resolves both versions from NuGet and diffs them.
- **Local assembly mode:** Supply `--from-assembly` and `--to-assembly` (plus `--from` and `--to` as version labels).

**Exit codes:**

| Code | Meaning |
|------|---------|
| `0` | Schema written successfully (may have 0 rules — a warning is printed) |
| `1` | Runtime failure (missing files, network error, invalid version, output file already exists) |
| `2` | Input validation error (conflicting flags, missing required mode) |

**Behavior:**

1. Resolves both assemblies (NuGet download or local path validation).
2. Extracts API manifests via `AssemblyExtractor`.
3. Diffs both manifests via `MultiVersionExtractor`.
4. Feeds the diff into `MigrationSchemaGenerator.FromDiff()` to produce a draft schema.
5. Serializes the schema via `MigrationSchemaSerializer` and writes it to `--output`.
6. Prints a summary (or JSON summary if `--json` is set).

If the two versions have identical APIs, a schema with 0 rules is written and a warning is printed to stderr. The exit code is still `0`.

**Examples:**

```bash
# NuGet mode: diff MudBlazor 6.0.0 → 7.0.0
wrap-god migrate generate \
  --package MudBlazor \
  --from 6.0.0 --to 7.0.0

# Local assembly mode
wrap-god migrate generate \
  --from-assembly ./lib/v6/MudBlazor.dll \
  --to-assembly ./lib/v7/MudBlazor.dll \
  --from 6.0.0 --to 7.0.0 \
  --output mudblazor-migration.wrapgod-migration.json

# Custom rule-id prefix
wrap-god migrate generate \
  --package Serilog \
  --from 2.12.0 --to 3.1.1 \
  --rule-id-prefix SLG

# JSON summary output
wrap-god migrate generate \
  --package FluentValidation \
  --from 10.4.0 --to 11.0.0 \
  --json

# Disable rename detection (all removals become manual rules)
wrap-god migrate generate \
  --package Newtonsoft.Json \
  --from 12.0.3 --to 13.0.3 \
  --no-rename-detection
```

**Output (default human-readable):**

```
WrapGod migrate generate
----------------------------------------
Library:  MudBlazor
From:     6.0.0
To:       7.0.0
Rules:    47 total
  auto:      31
  verified:  9
  manual:    7
Output:   mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json
```

**Output (`--json`):**

```json
{
  "library": "MudBlazor",
  "from": "6.0.0",
  "to": "7.0.0",
  "outputPath": "mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json",
  "rules": {
    "total": 47,
    "byConfidence": {
      "auto": 31,
      "verified": 9,
      "manual": 7
    }
  }
}
```

---

### `migrate apply`

**Synopsis:** Apply a `MigrationSchema` to a project directory, rewriting source files according to the schema rules and persisting state for idempotent re-runs.

**Usage:**
```
wrap-god migrate apply [options]
```

**Options:**

| Option | Required | Description | Default |
|--------|----------|-------------|---------|
| `--schema`, `-s` | Yes | Path to migration schema JSON produced by `migrate generate` | — |
| `--project-dir`, `-p` | No | Root directory for glob resolution and state file location | Current directory |
| `--include` | No | Glob pattern for files to include (repeatable) | `**/*.cs` |
| `--exclude` | No | Glob pattern for files to exclude (repeatable) | `**/bin/**`, `**/obj/**`, `**/.wrapgod/**` |
| `--dry-run` | No | Preview changes without modifying files or persisting state | `false` |
| `--json` | No | Emit summary as JSON to stdout | `false` |
| `--verbose`, `-v` | No | Print extra diagnostic information | `false` |

**Exit codes:**

| Code | Meaning |
|------|---------|
| `0` | Success — all applicable rules processed |
| `1` | Runtime error (schema not found, parse error, IO failure) |
| `2` | Bad arguments (required flag absent) |

**Behavior:**

1. Load and validate the schema JSON from `--schema`.
2. Resolve files by walking `--project-dir` and applying include/exclude glob patterns.
3. Build `MigrationEngine.CreateDefault()` and wrap in `StatefulMigrationEngine`.
4. Call `DryRunWithState` (with `--dry-run`) or `ApplyWithState` (without).
5. Print a structured summary; in dry-run mode, annotate that no files were modified.
6. Persist updated state file next to the schema (unless `--dry-run`).

**Idempotence:** The command uses `StatefulMigrationEngine` from `#197`. On subsequent runs with the same schema, already-applied `(ruleId, file)` pairs are skipped, producing a no-op. If the schema file changes (schema hash differs), all rules are re-evaluated.

**State file:** Written to `{schemaPath}.state.json` in the same directory as the schema. Intended to be committed alongside the schema for team visibility.

**Corruption recovery:** If the state file is corrupt (invalid JSON), it is archived to `{schemaPath}.state.json.bak` and the run proceeds from scratch. The recovery is surfaced in the output as a warning.

**Examples:**

```bash
# Dry-run preview
wrap-god migrate apply \
  --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json \
  --project-dir ./src \
  --dry-run

# Apply for real
wrap-god migrate apply \
  --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json \
  --project-dir ./src

# Limit to specific directory, JSON output
wrap-god migrate apply \
  --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json \
  --project-dir ./src \
  --include "**/Components/**" \
  --exclude "**/Generated/**" \
  --json

# Verbose mode for extra diagnostic output
wrap-god migrate apply \
  --schema mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json \
  --project-dir ./src \
  --verbose
```

**Human-readable output:**
```
WrapGod migrate apply [DRY-RUN]
--------------------------------
Schema:    mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json (47 rules)
Files:     128 scanned, 22 modified
Applied:   38 rewrites
Skipped:   6
Manual:    3 rules require human intervention

Skipped:
  MUD-017 src/Dialogs/Confirm.cs:42  Ambiguous: two overloads of Show() in scope

Manual:
  MANUAL-001 Parameters restructured -- requires manual mapping
    matched in: src/Dialogs/Confirm.cs, src/Dialogs/Edit.cs

(no files were modified)
```

**JSON output (`--json`):**
```json
{
  "dryRun": true,
  "filesScanned": 128,
  "filesModified": 22,
  "applied": 38,
  "skipped": 6,
  "manual": 3
}
```

---

### `migrate status`

**Synopsis:** Report migration progress from the state file without running any migration.

**Usage:**
```
wrap-god migrate status --schema <path> [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--schema`, `-s` | Path to the migration schema JSON file. The state file is the sibling `<schema>.state.json`. | **Required** |
| `--project-dir`, `-p` | Project directory used to resolve a relative `--schema` path | Current directory |
| `--json` | Emit output as JSON instead of human-readable text | `false` |
| `--verbose`, `-v` | Include per-rule details and per-file applied lists in human-readable mode | `false` |

**Exit codes:**

| Code | Meaning |
|------|---------|
| `0` | State file exists and has no manual-confidence entries (or state file is missing) |
| `1` | Schema file not found, or state file is corrupt and could not be parsed |
| `2` | State has manual-confidence entries present — human review required |

**Behavior:**

1. Resolves `<schema>.state.json` as the sibling of `--schema`.
2. If the state file does not exist, prints a friendly message and exits 0 — this is not an error.
3. If the state file is corrupt, the `MigrationStateStore` archives it to `<schema>.state.json.bak` and exits 1 with a message referencing the backup path.
4. Computes the current schema hash and warns if the schema has changed since the last `apply` run.
5. Highlights any `<state>` synthetic `SkippedRewrite` entries, which indicate a corruption-recovery run.
6. Exits 2 when any `Manual`-confidence entries are present (signal that human work remains).

**Examples:**

```bash
# Human-readable status
wrap-god migrate status --schema mudblazor.6.0-to-7.0.wrapgod-migration.json

# With project directory (for relative schema path)
wrap-god migrate status \
  --schema mudblazor.6.0-to-7.0.wrapgod-migration.json \
  --project-dir ./src

# JSON output (useful for tooling / CI)
wrap-god migrate status \
  --schema mudblazor.6.0-to-7.0.wrapgod-migration.json \
  --json

# Verbose: per-rule applied breakdown, matched files for manual rules
wrap-god migrate status \
  --schema mudblazor.6.0-to-7.0.wrapgod-migration.json \
  --verbose
```

**Output (default human-readable):**

```
WrapGod migrate status
----------------------------------------
Migration: MudBlazor 6.0.0 -> 7.0.0
Schema:     mudblazor.6.0-to-7.0.wrapgod-migration.json
Started:    2026-04-01 12:00:00 UTC
Last run:   2026-04-02 09:14:33 UTC

Progress: 38 / 47 rules applied (81%)

Schema hash: sha256:ab12cd34... (matches current schema)

Applied:     38   (across 22 file(s))
Skipped:      6
Manual:       3

Skipped rules:
  Ambiguous: two overloads of Show() in scope: 4
  Conflict with existing declaration: 2

Manual rules (require human intervention):
  MUD-003  Parameters restructured — requires manual mapping
  MUD-007  RemoveMember (obj.Deprecated): review and remove call sites
  MUD-012  Namespace moved — update using directives
```

**Output (`--json`):**

```json
{
  "library": "MudBlazor",
  "from": "6.0.0",
  "to": "7.0.0",
  "schema": "mudblazor.6.0-to-7.0.wrapgod-migration.json",
  "schemaHash": "sha256:ab12cd34...",
  "schemaChanged": false,
  "startedAt": "2026-04-01T12:00:00+00:00",
  "lastRunAt": "2026-04-02T09:14:33+00:00",
  "totalRules": 47,
  "appliedRules": 38,
  "progressPct": 0.81,
  "summary": {
    "total": 47,
    "applied": 38,
    "skipped": 6,
    "manual": 3
  },
  "applied": [
    { "ruleId": "MUD-001", "fileCount": 8 },
    { "ruleId": "MUD-002", "fileCount": 4 }
  ],
  "skipped": [
    { "reason": "Ambiguous: two overloads of Show() in scope", "count": 4 }
  ],
  "manual": [
    {
      "ruleId": "MUD-003",
      "note": "Parameters restructured — requires manual mapping",
      "matchedFiles": ["src/Dialogs/ConfirmDialog.cs", "src/Dialogs/EditDialog.cs"]
    }
  ],
  "stateRecoveryOccurred": false
}
```

> **See also:** [Migration state file format](../migration/state.md)

---

### `migrate verify`

**Synopsis:** Optionally build the migrated project and correlate compiler diagnostics back to migration rules via ±3-line proximity. Always informational — never blocks `migrate apply`.

**Usage:**
```
wrap-god migrate verify [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--schema`, `-s` | Path to the migration schema JSON. The state file is the sibling `<schema>.state.json`. When omitted, auto-detected from `--project-dir`. | Auto-detected |
| `--project-dir`, `-p` | Project root directory for build and auto-detection | Current directory |
| `--no-build` | Skip `dotnet build`; only report state-file summary | `false` |
| `--json` | Emit output as JSON instead of human-readable text | `false` |
| `--verbose`, `-v` | Include per-diagnostic details in human-readable mode | `false` |
| `--build-config` | Build configuration passed to `dotnet build` | `Debug` |
| `--baseline` | Pre-migration diagnostic snapshot JSON for net-new computation | (none) |

**Exit codes:**

| Code | Meaning |
|------|---------|
| `0` | Verify ran (even when attributed errors exist — verify is non-gating) |
| `1` | IO error (baseline file not found, etc.) |
| `2` | Bad arguments |

**Behavior:**

1. Locates the state file (sibling of `--schema`, or auto-detected via `*.wrapgod-migration.json.state.json` in `--project-dir`). If absent, exits 0 with a note.
2. Checks the schema hash and warns when the schema has changed since the last `apply` run.
3. (Unless `--no-build`) Spawns `dotnet build --nologo --no-restore` and captures combined stdout + stderr.
4. Parses diagnostics using the Roslyn/MSBuild format: `path(line,col): error|warning CODE: message`.
5. Attributes each diagnostic to the nearest `AppliedRewrite` in the state file within ±3 lines of the same file. Tiebreak: smallest distance; if equal, the rewrite appearing latest in the state wins.
6. Loads `--baseline` (if provided) and classifies matching diagnostics as pre-existing.
7. Reports: attributed errors per rule, unattributed errors, and pre-existing counts.

**Graceful degradation:**
- `dotnet` not on PATH → exits 0 with stderr note; no build attempted.
- No state file found → exits 0 with "no migration state; nothing to verify".
- `--baseline` path does not exist → exits 1 with error.

**Attribution algorithm:**

A diagnostic at `(file F, line L)` is attributed to rewrite `R` when:
- `R.File == F` (case-insensitive on Windows), AND
- `|R.Line - L| ≤ 3`

When multiple rewrites match, the one closest to the diagnostic line wins; ties are broken by the rewrite appearing later in the state file (i.e. most recently applied).

**Examples:**

```bash
# Human-readable verification (auto-detects schema from project dir)
wrap-god migrate verify --project-dir ./src

# Explicit schema + verbose per-diagnostic listing
wrap-god migrate verify \
  --schema mudblazor.6.0-to-7.0.wrapgod-migration.json \
  --verbose

# JSON output for CI tooling
wrap-god migrate verify \
  --schema mudblazor.6.0-to-7.0.wrapgod-migration.json \
  --json

# Skip build (build ran as a separate CI job)
wrap-god migrate verify \
  --schema mudblazor.6.0-to-7.0.wrapgod-migration.json \
  --no-build

# Compute net-new errors vs. a pre-migration baseline snapshot
wrap-god migrate verify \
  --schema mudblazor.6.0-to-7.0.wrapgod-migration.json \
  --baseline pre-migration-diagnostics.json
```

**Output (human-readable):**

```
WrapGod migrate verify
----------------------
Project:  ./src
Schema:   mudblazor.6.0-to-7.0.wrapgod-migration.json
Baseline: (none)

Build:    FAILED (50 error(s), 3 warning(s))

Attribution:
  MUD-003   38 error(s), 0 warning(s)
  MUD-017   12 error(s), 0 warning(s)
  Unattributed: 0 error(s), 3 warning(s) (likely pre-existing or unrelated)
```

**Output (`--json`):**

```json
{
  "schema": "mudblazor.6.0-to-7.0.wrapgod-migration.json",
  "projectDir": "./src",
  "schemaChanged": false,
  "noBuild": false,
  "build": { "exitCode": 1, "launched": true, "errors": 50, "warnings": 3 },
  "baselineDiagnosticsLoaded": 0,
  "state": { "applied": 47, "skipped": 6, "manual": 3 },
  "attribution": [
    {
      "ruleId": "MUD-003",
      "errors": 38,
      "warnings": 0,
      "diagnostics": [
        { "file": "src/Dialogs/ConfirmDialog.cs", "line": 42, "code": "CS1501", "message": "...", "severity": "error" }
      ]
    }
  ],
  "unattributed": [
    { "file": "src/Other.cs", "line": 100, "code": "CS0618", "message": "deprecated", "severity": "warning" }
  ]
}
```

**Common gotchas:**

- **`--no-restore`**: `verify` passes `--no-restore` to `dotnet build`, so a `dotnet restore` must have been run previously. In fresh CI checkouts, add a restore step before `verify`.
- **Schema drift**: If you edit the schema after `apply`, the attribution results may not align. The command warns when it detects this.
- **Case-sensitive paths on Linux**: The ±3-line match is case-insensitive on Windows but case-sensitive on Linux/macOS. Ensure paths in the state file match the actual filesystem casing.

> **See also:** [Attribution semantics](../migration/verifying.md) · [State file format](../migration/state.md)

---

### `ci bootstrap`

**Synopsis:** Generate recommended CI workflow files for a WrapGod project.

**Usage:**
```
wrap-god ci bootstrap [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--output`, `-o` | Output directory for workflow files | `.github/workflows` |
| `--force` | Overwrite existing workflow files | `false` |

**Exit codes:**

| Code | Meaning |
|------|---------|
| `0` | Workflow generated successfully |
| `1` | Workflow file already exists (use `--force`) |

**Behavior:**

Generates a `wrapgod-ci.yml` GitHub Actions workflow that includes:

- Checkout, .NET SDK setup, restore, build
- Test with code coverage collection
- `wrap-god extract`, `generate`, and `analyze` steps
- Coverage artifact upload

**Examples:**

```bash
# Generate default workflow
wrap-god ci bootstrap

# Custom output directory
wrap-god ci bootstrap --output .gitlab/ci

# Overwrite existing
wrap-god ci bootstrap --force
```

---

### `ci parity`

**Synopsis:** Compare current CI configuration against the recommended WrapGod baseline.

**Usage:**
```
wrap-god ci parity [options]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--workflow-dir`, `-w` | Directory containing CI workflow files | `.github/workflows` |

**Exit codes:**

| Code | Meaning |
|------|---------|
| `0` | Full parity -- all baseline steps present |
| `1` | Workflow directory or files not found |
| `2` | Missing steps detected |

**Baseline steps checked:**

| Step ID | Description | Search pattern |
|---------|-------------|----------------|
| `checkout` | Checkout repository | `actions/checkout` |
| `setup-dotnet` | Setup .NET SDK | `actions/setup-dotnet` |
| `restore` | Restore NuGet packages | `dotnet restore` |
| `build` | Build solution | `dotnet build` |
| `test` | Run tests | `dotnet test` |
| `coverage` | Collect code coverage | `XPlat Code Coverage` |
| `wg-extract` | WrapGod extract step | `wrap-god extract` |
| `wg-generate` | WrapGod generate step | `wrap-god generate` |
| `wg-analyze` | WrapGod analyze step | `wrap-god analyze` |

**Examples:**

```bash
# Check default location
wrap-god ci parity

# Check custom workflow directory
wrap-god ci parity --workflow-dir .github/workflows
```

**Output:**

```
WrapGod CI Parity Report
----------------------------------------

Scanning 1 workflow file(s) in .github/workflows

Baseline steps found:
  [PASS] Checkout repository (checkout)
  [PASS] Setup .NET SDK (setup-dotnet)
  [PASS] Restore NuGet packages (restore)
  [PASS] Build solution (build)
  [PASS] Run tests (test)
  [PASS] Collect code coverage (coverage)
  [PASS] WrapGod extract step (wg-extract)
  [MISS] WrapGod generate step (wg-generate)
  [MISS] WrapGod analyze step (wg-analyze)

Missing or outdated steps:
  [MISS] WrapGod generate step (wg-generate)
  [MISS] WrapGod analyze step (wg-analyze)

Parity: 7/9 steps (77%)

Run 'wrap-god ci bootstrap --force' to regenerate the recommended workflow.
```

---

## Common Workflows

### New project setup

```bash
wrap-god init --source "Serilog@4.0.0"
wrap-god extract --nuget Serilog@4.0.0 -o serilog.wrapgod.json
wrap-god doctor
dotnet add package WrapGod.Generator
dotnet build
```

### Migration analysis

```bash
wrap-god extract --nuget MyLib@3.0.0 -o mylib.wrapgod.json
wrap-god analyze mylib.wrapgod.json
wrap-god migrate init --manifest mylib.wrapgod.json
# Review migration-plan.json, then add analyzers for code fixes
dotnet add package WrapGod.Analyzers
dotnet build
```

### Version upgrade analysis

```bash
wrap-god extract \
  --nuget FluentAssertions@6.12.0 \
  --nuget FluentAssertions@7.0.0 \
  -o fluent.wrapgod.json
wrap-god analyze fluent.wrapgod.json
wrap-god explain ShouldBeEquivalentTo  # check removed APIs
```

### CI validation

```bash
wrap-god ci bootstrap
wrap-god ci parity
# Fix any missing steps, then commit
```

### Symbol investigation

```bash
wrap-god explain HttpClient
wrap-god explain ILogger.LogInformation
wrap-god explain "Microsoft.Extensions.Logging.ILogger"
```
