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
