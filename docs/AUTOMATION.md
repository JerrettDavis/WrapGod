# End-to-End Automation: How WrapGod Eliminates Library Tech Debt

## The Promise

You didn't change your requirements. You didn't add features. You just
bumped a dependency version, and now 47 files have compile errors. That's
library tech debt -- and it's the most frustrating kind because you didn't
ask for it.

WrapGod's automation pipeline turns library upgrades into version-number
changes. You edit one line in your `.csproj`, run `dotnet build`, and the
pipeline re-extracts the API surface, regenerates your wrappers, flags
anything that broke, and offers code fixes for what it can resolve
automatically. No refactoring sprint. No "update Serilog" Jira epic that
lingers for three months.

The same pipeline handles library swaps -- moving from Moq to NSubstitute,
or from FluentAssertions to Shouldly. Same mechanism, bigger payoff.

---

## The Pipeline

Every `dotnet build` with WrapGod.Targets installed runs this pipeline
automatically:

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│  NuGet       │    │  Extract     │    │  Generate    │    │  Compile +   │    │  Code Fix    │
│  Restore     │───►│  Manifest    │───►│  Wrappers    │───►│  Analyze     │───►│  (optional)  │
│              │    │              │    │              │    │              │    │              │
│  Resolves    │    │  Reads DLL   │    │  Emits       │    │  WG2001:     │    │  dotnet      │
│  packages    │    │  via         │    │  IWrapped*   │    │  direct type │    │  format      │
│  to local    │    │  Metadata    │    │  interfaces  │    │  WG2002:     │    │  rewrites    │
│  cache       │    │  LoadContext │    │  + *Facade   │    │  direct call │    │  call sites  │
│              │    │              │    │  proxies     │    │              │    │              │
└──────────────┘    └──────────────┘    └──────────────┘    └──────────────┘    └──────────────┘
  WrapGodRestore      WrapGodExtract     WrapGodGenerate     Roslyn analyzer     dotnet format
  (before Restore)    (before Compile)   (before Compile)    (during Compile)    (manual or CI)
```

**No human intervention** for the first four stages. The fifth stage --
applying code fixes -- can be fully automated in CI or done interactively
in your IDE.

---

## Setting Up Zero-Touch Automation

Here's the complete `.csproj` with every WrapGod property explained:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>

    <!--
      EnableWrapGod (default: true)
      Set to false to disable all WrapGod targets without removing packages.
      Useful for temporarily bypassing extraction during debugging.
    -->
    <EnableWrapGod>true</EnableWrapGod>

    <!--
      WrapGodManifestPath (default: $(MSBuildProjectDirectory)\manifest.wrapgod.json)
      Where the extracted manifest is written. The source generator reads this
      as an AdditionalFile.
    -->
    <WrapGodManifestPath>$(MSBuildProjectDirectory)\manifest.wrapgod.json</WrapGodManifestPath>

    <!--
      WrapGodConfigPath (default: $(MSBuildProjectDirectory)\wrapgod.config.json)
      Optional configuration file for renaming types/members, excluding members,
      or overriding generation behavior. See CONFIGURATION.md.
    -->
    <WrapGodConfigPath>$(MSBuildProjectDirectory)\wrapgod.config.json</WrapGodConfigPath>

    <!--
      WrapGodCacheDir (default: $(MSBuildProjectDirectory)\.wrapgod-cache)
      Local cache for resolved packages and intermediate artifacts.
      The extract step hashes inputs and skips work when nothing changed.
    -->
    <WrapGodCacheDir>$(MSBuildProjectDirectory)\.wrapgod-cache</WrapGodCacheDir>
  </PropertyGroup>

  <ItemGroup>
    <!-- Source generator: reads manifest, emits IWrapped* and *Facade files -->
    <PackageReference Include="WrapGod.Generator"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />

    <!-- Analyzer: flags direct usage of wrapped types (WG2001, WG2002) -->
    <PackageReference Include="WrapGod.Analyzers"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />

    <!-- MSBuild targets: automates extract + generate pipeline -->
    <PackageReference Include="WrapGod.Targets"
                      Version="0.1.0-alpha"
                      PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <!-- Libraries to wrap: just the package name, WrapGod resolves the rest -->
    <PackageReference Include="Serilog" Version="3.1.0" />
    <WrapGodPackage Include="Serilog" />
  </ItemGroup>
</Project>
```

After this setup, every `dotnet build` automatically extracts, generates,
and analyzes. You never run a separate tool.

---

## The Upgrade Workflow

**Scenario:** You need to upgrade Serilog from v2.12 to v3.1.

### Step 1: Change the version

```xml
<!-- Before -->
<PackageReference Include="Serilog" Version="2.12.0" />

<!-- After -->
<PackageReference Include="Serilog" Version="3.1.0" />
```

One line. That's the only manual change.

### Step 2: Build

```bash
dotnet build
```

Here's what happens behind the scenes:

1. **WrapGodRestore** resolves Serilog 3.1.0 from NuGet into the local
   cache
2. **WrapGodExtract** detects that the input hash changed (different DLL),
   re-reads the Serilog 3.1.0 assembly, and writes a new
   `manifest.wrapgod.json`
3. **WrapGodGenerate** registers the updated manifest as an
   `AdditionalFile`
4. The **Roslyn source generator** compares the new `GenerationPlan`
   against the cached one. Changed types get new `IWrapped*.g.cs` and
   `*Facade.g.cs` files. Unchanged types are skipped (incremental
   caching).
5. The **Roslyn analyzer** scans your code and reports any new issues

### Step 3: Check the build output

If Serilog 3.1 is API-compatible with 2.12 for the members you use, the
build succeeds with zero warnings. You're done.

If Serilog 3.1 removed or renamed members you depend on, you'll see:

```
warning WG2001: Direct usage of 'Serilog.Log.CloseAndFlush' which has a
               generated wrapper interface 'IWrappedLog'. Use the wrapper
               instead.
```

### Step 4: Apply safe fixes

```bash
dotnet format analyzers --diagnostics WG2001 WG2002
```

This rewrites all direct references to use the generated wrappers. For
members that were removed in v3.1, the facade throws
`PlatformNotSupportedException` (in Adaptive mode) or the member is
simply absent from the interface (in LCD mode), giving you a compile
error that points directly at the call site that needs manual attention.

### Step 5: Review and commit

The only manual work is reviewing call sites that reference members that
genuinely changed between versions. WrapGod got you from "47 compile
errors scattered across the codebase" to "3 call sites that need your
judgment."

---

## The Library Swap Workflow

**Scenario:** You need to switch from Moq to NSubstitute.

This is a bigger change than a version bump, but WrapGod handles it with
the same pipeline. The key difference: you're swapping the underlying
library behind the wrappers rather than updating it.

### Step 1: Extract manifests for both libraries

```bash
wrap-god extract --nuget Moq@4.20.0 -o moq.wrapgod.json
wrap-god extract --nuget NSubstitute@5.1.0 -o nsub.wrapgod.json
```

### Step 2: Configure the mapping

Create a `wrapgod.config.json` that maps Moq concepts to NSubstitute
equivalents:

```json
{
  "types": [
    {
      "sourceType": "Moq.Mock`1",
      "include": true,
      "targetName": "IMockWrapper",
      "members": [
        { "sourceMember": "Setup", "include": true, "targetName": "Arrange" },
        { "sourceMember": "Verify", "include": true, "targetName": "AssertReceived" },
        { "sourceMember": "Object", "include": true, "targetName": "Instance" }
      ]
    }
  ]
}
```

### Step 3: Update your .csproj

```xml
<ItemGroup>
  <!-- Remove Moq, add NSubstitute -->
  <PackageReference Include="NSubstitute" Version="5.1.0" />
  <WrapGodPackage Include="NSubstitute" />
</ItemGroup>

<ItemGroup>
  <!-- Include both manifests for the swap -->
  <AdditionalFiles Include="moq.wrapgod.json" />
  <AdditionalFiles Include="nsub.wrapgod.json" />
</ItemGroup>
```

### Step 4: Build, analyze, fix

```bash
dotnet build
dotnet format analyzers --diagnostics WG2001 WG2002
```

The analyzer flags every Moq reference. The code fixer rewrites them to
the generated wrapper interface. Your tests now compile against wrapper
interfaces backed by NSubstitute instead of Moq.

For complete working examples of bidirectional library swaps, see the
[`examples/MoqToNSubstitute`](../examples/MoqToNSubstitute) and
[`examples/NSubstituteToMoq`](../examples/NSubstituteToMoq) example
packs, plus the bidirectional migration packs under
[`examples/migrations/`](../examples/migrations/).

---

## Multi-Version Support

Sometimes you can't upgrade everywhere at once. Maybe one service needs
Serilog 2.x for compatibility with a legacy sink, while another is ready
for 3.x. WrapGod's **Adaptive mode** lets you support both from the same
codebase.

### Extract both versions

```bash
wrap-god extract --nuget Serilog@2.12.0 --nuget Serilog@3.1.0 -o serilog.wrapgod.json
```

The merged manifest annotates each member with `introducedIn` and
`removedIn` metadata. Members present in both versions have no
annotations. Members added in 3.1 have `"introducedIn": "3.1.0"`.
Members removed in 3.1 have `"removedIn": "3.1.0"`.

### Choose a compatibility mode

| Mode | What gets generated | When to use |
|------|-------------------|-------------|
| **LCD** | Only members present in both 2.12 and 3.1 | Maximum safety -- code compiles against any version |
| **Targeted** | Members present in a single specified version | Pinned deployment -- you know exactly which version runs |
| **Adaptive** | All members, with runtime guards on version-specific ones | Runtime flexibility -- code detects the installed version |

### Adaptive mode in action

With Adaptive mode, the generated facade wraps version-specific members
with runtime checks:

```csharp
// Generated SerilogFacade.g.cs (simplified)
public void CloseAndFlush()
{
    if (!WrapGodVersionHelper.IsMemberAvailable("2.12.0", removedIn: "3.1.0"))
        throw new PlatformNotSupportedException(
            "Serilog.Log.CloseAndFlush is not available in the installed version.");

    _inner.CloseAndFlush();
}
```

Your code compiles and runs against either version. If it calls a member
that doesn't exist at runtime, it gets a clear exception instead of a
`MissingMethodException`.

See [COMPATIBILITY.md](COMPATIBILITY.md) for the full reference.

---

## CI Integration

WrapGod diagnostics are standard Roslyn analyzer warnings. They integrate
with any CI system that understands MSBuild output.

### Gating builds on WrapGod diagnostics

To fail the build when direct usage of wrapped types is detected, promote
WrapGod warnings to errors:

```xml
<PropertyGroup>
  <!-- Treat WG2001 and WG2002 as build errors -->
  <WarningsAsErrors>$(WarningsAsErrors);WG2001;WG2002</WarningsAsErrors>
</PropertyGroup>
```

### GitHub Actions workflow

```yaml
name: Build with WrapGod

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: global.json

      - name: Restore
        run: dotnet restore

      - name: Build (extracts manifests, generates wrappers, runs analyzers)
        run: dotnet build --no-restore --warnaserror

      - name: Test
        run: dotnet test --no-build
```

That's it. The `dotnet build` step runs the full WrapGod pipeline. If any
code references a wrapped type directly, the build fails. No extra CI
plugins or custom scripts needed.

### CLI analysis in CI

For richer diagnostics output (JSON or SARIF), use the CLI's `analyze`
command:

```yaml
      - name: WrapGod Analysis
        run: |
          wrap-god analyze manifest.wrapgod.json \
            --config wrapgod.config.json \
            --warnings-as-errors
```

The `analyze` command exits with a non-zero code when diagnostics exceed
the configured threshold, and can emit SARIF 2.1.0 for integration with
GitHub Code Scanning or other security tooling. See
[RFC-0054](rfc/0054-structured-diagnostics-contract-and-reporting.md) for
the diagnostics contract.

---

## Enterprise Scale

WrapGod's architecture is designed for one team to wrap a library and the
entire organization to consume the result.

### The Company.Assertions case study

Consider an enterprise with four legacy applications, each using
different assertion libraries:

| Application | Libraries |
|-------------|-----------|
| LegacyApp.A | FluentAssertions 6.12.0 |
| LegacyApp.B | FluentAssertions 8.3.0 + Shouldly 4.3.0 (mixed) |
| LegacyApp.C | Shouldly 4.1.0 |
| LegacyApp.D | FluentAssertions 5.10.3 + custom helpers |

One platform team creates a `Company.Assertions` package:

1. **Extract** Shouldly 4.3.0 as the backing implementation
2. **Configure** the wrapper to expose a unified `Company.Assertions`
   namespace
3. **Publish** `Company.Assertions` as an internal NuGet package

Each legacy app then migrates independently:

```bash
# In each app's directory:
dotnet add package Company.Assertions
dotnet build                    # WrapGod flags all direct FA/Shouldly usage
dotnet format analyzers --diagnostics WG2001 WG2002  # Rewrites to Company.Assertions
```

After migration, all four apps:
- Program against the same `Company.Assertions` interfaces
- Have zero direct references to FluentAssertions or Shouldly
- Can upgrade the backing library (or swap it entirely) by updating the
  `Company.Assertions` package -- no changes in any consuming app

The full case study with four migrated applications is at
[`examples/case-studies/company-assertions-migration`](../examples/case-studies/company-assertions-migration).

### Why this scales

- **One extraction, many consumers.** The manifest is extracted once and
  published as part of the wrapper package. Consuming teams never run the
  extractor.
- **Centralized upgrade path.** When the backing library releases a new
  version, the platform team updates the wrapper package. Consuming teams
  get new wrappers automatically on their next `dotnet restore`.
- **Incremental migration.** Each team migrates on their own schedule.
  The wrapper and the raw library can coexist during the transition.
- **Diagnostics as guardrails.** Promoting `WG2001`/`WG2002` to errors
  in CI prevents new direct usage from creeping in after migration.

---

## Further Reading

- [QUICKSTART.md](QUICKSTART.md) -- three paths from zero to wrapped
- [CONFIGURATION.md](CONFIGURATION.md) -- JSON, attribute, and Fluent DSL configuration
- [COMPATIBILITY.md](COMPATIBILITY.md) -- LCD, Targeted, and Adaptive modes
- [MSBUILD-INTEGRATION.md](MSBUILD-INTEGRATION.md) -- MSBuild targets deep dive
- [ANALYZERS.md](ANALYZERS.md) -- full diagnostics reference
- [ARCHITECTURE.md](ARCHITECTURE.md) -- pipeline internals and component reference
