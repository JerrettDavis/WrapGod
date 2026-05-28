# Migrating from Serilog v2 to v3 with WrapGod

This is the end-to-end example for WrapGod's migration engine (issue #203).
It demonstrates how to use `wrap-god migrate apply` against a real library upgrade
with a hand-authored schema.

## Breaking Changes Demonstrated

| ID | Kind | Automated? | Description |
|----|------|-----------|-------------|
| `SERILOG-NS-001` | `renameNamespace` | **Auto** (Verified) | `using Serilog.Sinks.RollingFile;` → `using Serilog;` — the RollingFile sink package was removed; rolling support moved into `Serilog.Sinks.File`. |
| `SERILOG-RM-001` | `renameMember` | **Manual** | `WriteTo.RollingFile(...)` → `WriteTo.File(..., rollingInterval: RollingInterval.Day)` — the engine flags this for human review because it cannot safely resolve the receiver type in a fluent chain. |

## Directory Layout

```
serilog-v2-to-v3/
├── README.md                   ← this file
├── before/                     ← Serilog 2.x code (source of truth for migration input)
│   ├── Serilog.V2.Sample.csproj
│   ├── Program.cs
│   └── Logging/MyLogger.cs
├── after/                      ← Expected engine output (byte-equal to what migrate apply produces)
│   ├── Serilog.V3.Sample.csproj
│   ├── Program.cs
│   └── Logging/MyLogger.cs
├── schema/
│   └── serilog.2.x-to-3.x.wrapgod-migration.json   ← hand-authored migration schema
├── state/
│   └── serilog.2.x-to-3.x.wrapgod-migration.json.state.json   ← post-apply state snapshot
├── scripts/
│   ├── run-migration.ps1       ← PowerShell demo script
│   └── run-migration.sh        ← Bash demo script
└── MigrationTests/
    ├── MigrationTests.csproj
    └── MigrationParityTests.cs ← CI test: applies schema to before/, diffs against after/
```

## Before/After API Differences

**Before (v2):**
```csharp
using Serilog.Sinks.RollingFile;           // separate NuGet package

Log.Logger = new LoggerConfiguration()
    .WriteTo.RollingFile("logs/app-{Date}.log")  // removed in v3
    .CreateLogger();
```

**After engine run (automated part):**
```csharp
using Serilog;                              // namespace collapsed (was Serilog.Sinks.RollingFile)

Log.Logger = new LoggerConfiguration()
    .WriteTo.RollingFile("logs/app-{Date}.log")  // still present — manual step needed
    .CreateLogger();
```

**Final (after manual step):**
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

### Dry-run preview (no files changed)

**PowerShell:**
```powershell
./scripts/run-migration.ps1
```

**Bash:**
```bash
./scripts/run-migration.sh
```

### Run the CLI steps manually

From the repo root:

```bash
# 1. Dry-run — preview what the engine would change
dotnet run --project WrapGod.Cli -- migrate apply \
  --schema examples/migrations/serilog-v2-to-v3/schema/serilog.2.x-to-3.x.wrapgod-migration.json \
  --project-dir examples/migrations/serilog-v2-to-v3/before \
  --dry-run

# 2. Apply to a COPY (never modify the committed fixture directly)
cp -r examples/migrations/serilog-v2-to-v3/before /tmp/serilog-test

dotnet run --project WrapGod.Cli -- migrate apply \
  --schema examples/migrations/serilog-v2-to-v3/schema/serilog.2.x-to-3.x.wrapgod-migration.json \
  --project-dir /tmp/serilog-test

# 3. Check migration status
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

Or run the equivalent test in the main test suite:

```bash
dotnet test WrapGod.Tests/WrapGod.Tests.csproj \
  --filter "SerilogMigrationE2ETests" \
  --nologo
```

## What the Engine Does (and Doesn't) Handle

### Automated (confidence: `verified`)

**Rule SERILOG-NS-001 — Namespace rename:**
- Finds every `using Serilog.Sinks.RollingFile;` directive and replaces it with `using Serilog;`.
- Also rewrites any fully-qualified `Serilog.Sinks.RollingFile.Xyz` references in code.
- Safe to apply automatically because the mapping is 1:1 and syntax-only.

### Manual (confidence: `manual`)

**Rule SERILOG-RM-001 — WriteTo.RollingFile → WriteTo.File:**
- The engine detects calls to `.RollingFile(...)` and reports them for human review.
- It does **not** rewrite them automatically because the receiver in a fluent builder
  chain (`WriteTo.RollingFile(...)`) cannot be type-resolved without semantic analysis.
- You must manually change:
  ```csharp
  // Before
  .WriteTo.RollingFile("logs/app-{Date}.log")
  
  // After
  .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
  ```
  Note: The `{Date}` token in the path was a v2-specific interpolation;
  v3 appends the date automatically.

## State File

After `migrate apply` runs, a state file is created alongside the schema:

```
schema/serilog.2.x-to-3.x.wrapgod-migration.json.state.json
```

The committed version in `state/` shows what a completed run looks like:
- 1 rule applied (`SERILOG-NS-001`)
- 1 rule in manual review (`SERILOG-RM-001`)

Subsequent runs of `migrate apply` are idempotent; already-applied rules are skipped.

## Known Limitations

1. **Fluent chain rewrites** — The engine's member-rename heuristic requires the receiver to be a locally declared variable or field with a known type. Fluent builder chains like `.WriteTo.RollingFile(...)` cannot be type-resolved from syntax alone, so they end up in the manual list. A future semantic (Roslyn workspace) rewriter would handle these.

2. **Path pattern conversion** — Changing `{Date}` to the v3 path format is a string-value transform; no rule kind currently handles literal string mutations.

3. **Package reference updates** — The `.csproj` files must be manually updated to remove `Serilog.Sinks.RollingFile` and add `Serilog.Sinks.File`. WrapGod operates on `.cs` files only.

4. **NuGet restore** — The `after/` project references Serilog 3.x packages. `dotnet build after/` requires an internet connection or a populated NuGet cache.
