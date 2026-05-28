# Migrating from Serilog v2 to v3 with WrapGod

This is the end-to-end example for WrapGod's migration engine (issue #203).
It demonstrates how to use `wrap-god migrate apply` against a real library upgrade
with a hand-authored schema covering three different rule kinds.

## Breaking Changes Demonstrated

| ID | Kind | Confidence | Description |
|----|------|-----------|-------------|
| `SERILOG-NS-001` | `renameNamespace` | **`verified`** (auto) | `using Serilog.Sinks.RollingFile;` → `using Serilog;` — the RollingFile sink package was removed in v3; rolling support moved into `Serilog.Sinks.File`. The engine's duplicate-using cleanup pass ensures the result is not `using Serilog; using Serilog;` when the new namespace already exists. |
| `SERILOG-RM-001` | `renameMember` | **`manual`** | `WriteTo.RollingFile(...)` → `WriteTo.File(..., rollingInterval: RollingInterval.Day)` — flagged for human review; the engine cannot safely resolve a fluent-chain receiver type from syntax alone. |
| `SERILOG-RX-001` | `removeMember` | **`manual`** | Illustrative audit pass: every `_logger.Debug(...)` call site is surfaced for human review so the team can decide which diagnostic-level calls survive the migration. The engine reports without removing. |

## Directory Layout

```
serilog-v2-to-v3/
├── README.md                                          ← this file
├── before/                                            ← Serilog 2.x source (fixture input)
│   ├── Serilog.V2.Sample.csproj
│   ├── Program.cs
│   └── Logging/MyLogger.cs
├── after/                                             ← Expected engine output (byte-equal)
│   ├── Serilog.V3.Sample.csproj
│   ├── Program.cs
│   └── Logging/MyLogger.cs
├── schema/
│   ├── serilog.2.x-to-3.x.wrapgod-migration.json              ← hand-authored schema (3 rules)
│   └── serilog.2.x-to-3.x.wrapgod-migration.json.state.json  ← committed post-apply state (sanitised)
├── scripts/
│   ├── run-migration.ps1                              ← PowerShell demo
│   └── run-migration.sh                               ← Bash demo
└── MigrationTests/                                    ← CI parity guard
    ├── MigrationTests.csproj
    └── MigrationParityTests.cs
```

The state file lives next to the schema (per plan §4.2 / §197). Runtime state files are gitignored globally; this one is force-included as a committed reference fixture.

## Before/After API Differences

**Before (v2):**
```csharp
using Serilog.Sinks.RollingFile;           // separate NuGet package

Log.Logger = new LoggerConfiguration()
    .WriteTo.RollingFile("logs/app-{Date}.log")  // removed in v3
    .CreateLogger();
```

**After engine run (automated part — what `after/` contains):**
```csharp
using Serilog;                              // namespace collapsed (was Serilog.Sinks.RollingFile)

Log.Logger = new LoggerConfiguration()
    .WriteTo.RollingFile("logs/app-{Date}.log")  // still present — manual step needed
    .CreateLogger();
```

**Final (after the manual SERILOG-RM-001 step is applied by a human):**
```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)  // fully migrated
    .CreateLogger();
```

## How to Run Locally

### Prerequisites
- .NET 10 SDK
- The WrapGod repo cloned locally

### Step 1 — (optional) Generate a schema draft

The committed schema in this directory was hand-authored so reviewers can see the
exact JSON conventions. If you want to regenerate it from real NuGet metadata,
run the following from the repo root (requires Serilog 2.x and 3.x to be reachable
via the configured NuGet feeds):

```bash
dotnet run --project WrapGod.Cli -- migrate generate \
    --package Serilog --from 2.12.0 --to 3.1.1 \
    --output examples/migrations/serilog-v2-to-v3/schema/serilog.2.x-to-3.x.wrapgod-migration.json
```

The generator emits `auto`-confidence rules; you'll typically edit the JSON afterward to
upgrade some rules to `verified` (when you've reviewed them) or `manual` (when they
cannot be applied safely without human judgement) and add `note` fields with context.

### Step 2 — Dry-run preview (no files changed)

**PowerShell:**
```powershell
./scripts/run-migration.ps1
```

**Bash:**
```bash
./scripts/run-migration.sh
```

### Step 3 — Apply

**PowerShell:**
```powershell
./scripts/run-migration.ps1 -Apply
```

**Bash:**
```bash
./scripts/run-migration.sh --apply
```

The scripts always work on a temp copy so `before/` is never mutated.

### Step 4 — Inspect the result

Both scripts print the temp directory at the end. Diff against `after/`:

```bash
git diff --no-index <temp-dir> examples/migrations/serilog-v2-to-v3/after
```

### Run the CLI steps manually (alternative)

```bash
# From the repo root:

# 1. Dry-run
dotnet run --project WrapGod.Cli -- migrate apply \
  --schema examples/migrations/serilog-v2-to-v3/schema/serilog.2.x-to-3.x.wrapgod-migration.json \
  --project-dir examples/migrations/serilog-v2-to-v3/before \
  --dry-run

# 2. Apply (to a COPY; never modify the committed fixture)
cp -r examples/migrations/serilog-v2-to-v3/before /tmp/serilog-test

dotnet run --project WrapGod.Cli -- migrate apply \
  --schema examples/migrations/serilog-v2-to-v3/schema/serilog.2.x-to-3.x.wrapgod-migration.json \
  --project-dir /tmp/serilog-test

# 3. Status
dotnet run --project WrapGod.Cli -- migrate status \
  --schema examples/migrations/serilog-v2-to-v3/schema/serilog.2.x-to-3.x.wrapgod-migration.json \
  --project-dir /tmp/serilog-test

# 4. Diff result against after/
git diff --no-index /tmp/serilog-test examples/migrations/serilog-v2-to-v3/after
```

### Run the CI parity test

```bash
dotnet test examples/migrations/serilog-v2-to-v3/MigrationTests/MigrationTests.csproj --nologo
```

Or the equivalent test inside the main test suite:

```bash
dotnet test WrapGod.Tests/WrapGod.Tests.csproj --filter "SerilogMigrationE2ETests" --nologo
```

## What the Engine Does (and Doesn't) Handle

### Automated (confidence: `verified`)

**Rule SERILOG-NS-001 — namespace rename:**

- Finds every `using Serilog.Sinks.RollingFile;` directive and replaces it with `using Serilog;`.
- Also rewrites any fully-qualified `Serilog.Sinks.RollingFile.Xyz` references in type-context positions.
- Safe to apply automatically: 1:1 syntax-only mapping.
- When the new namespace already appears earlier in the file (e.g. `using Serilog;` is already present), the engine's **duplicate-using cleanup post-pass** drops the duplicate that the rename would otherwise introduce.

### Manual (confidence: `manual`)

**Rule SERILOG-RM-001 — `WriteTo.RollingFile` → `WriteTo.File`:**

- Detects every `.RollingFile(...)` call and reports it for human review.
- Does **not** rewrite automatically: the receiver in a fluent builder chain (`WriteTo.RollingFile(...)`) cannot be type-resolved without semantic analysis. A future semantic (Roslyn workspace) rewriter would handle this.
- Manual replacement:
  ```csharp
  // Before
  .WriteTo.RollingFile("logs/app-{Date}.log")
  
  // After
  .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
  ```
  Note: the `{Date}` token in the path was a v2-specific interpolation; v3 appends the date automatically.

**Rule SERILOG-RX-001 — audit pass on diagnostic calls:**

- Demonstrative third rule kind. Many real Serilog v2→v3 migrations include an audit
  pass that flags every diagnostic-level call (Debug, Verbose) so teams can decide
  which to keep.
- The `removeMember` rewriter detects each match and surfaces it via the engine's
  Manual list — nothing is removed automatically.

## State File

After `migrate apply` runs, a state file is created alongside the schema:

```
schema/serilog.2.x-to-3.x.wrapgod-migration.json.state.json
```

The committed version is sanitised (file paths are relative, timestamps zeroed). It documents what a completed run looks like:

- 2 rewrites applied (both from `SERILOG-NS-001`, one per `.cs` file)
- 0 skipped
- 2 manual entries (`SERILOG-RM-001` + `SERILOG-RX-001`)

Subsequent runs of `migrate apply` are idempotent; already-applied rules are skipped via the schema-hash comparison.

## Known Limitations

1. **Fluent chain rewrites** — The engine's member-rename heuristic requires the receiver to be a locally declared variable or field with a known type. Fluent builder chains like `.WriteTo.RollingFile(...)` cannot be type-resolved from syntax alone, so they end up in the manual list. A future semantic (Roslyn workspace) rewriter would handle these.

2. **Path pattern conversion** — Changing `{Date}` to the v3 path format is a string-value transform; no rule kind currently handles literal string mutations.

3. **Package reference updates** — The `.csproj` files must be manually updated to remove `Serilog.Sinks.RollingFile` and add `Serilog.Sinks.File`. WrapGod operates on `.cs` files only.

4. **NuGet restore** — The `after/` project references Serilog 3.x packages. Building `after/` directly requires both the v3 NuGet packages and the manual `WriteTo.File` step from SERILOG-RM-001 to be applied; without it the project will not compile because v3 has no `RollingFile` extension. The committed `after/` snapshot is the **pre-manual** state (what the engine produces) — see the "After engine run" code block above.

5. **Build matrix** — The `before/` and `after/` projects are intentionally **not** added to `examples/WrapGod.Examples.slnx`. They are snapshot fixtures, not compilation targets; `after/` will not build against Serilog v3 until the SERILOG-RM-001 manual step is taken. The standalone `MigrationTests` project IS in the solution and IS exercised by CI on every push.
