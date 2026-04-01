# Quick Start Guide

Get from zero to wrapped in under five minutes. Pick the path that fits
your workflow -- all three produce the same result: your code talks to
generated interfaces instead of raw vendor types, and library upgrades
become version-number bumps.

## Prerequisites

- .NET 10 SDK (or later)
- A third-party library you want to wrap (we'll use [Shouldly](https://github.com/shouldly/shouldly) as the running example)

---

## Path A: Zero-Touch MSBuild (Recommended)

The fastest path. Add packages, declare what to wrap, build. Done.

### 1. Add WrapGod packages

```bash
dotnet add package WrapGod.Generator
dotnet add package WrapGod.Analyzers
dotnet add package WrapGod.Targets
```

### 2. Declare the package to wrap

Open your `.csproj` and add a `WrapGodPackage` item for the library you
want to wrap:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <!-- WrapGod packages -->
    <PackageReference Include="WrapGod.Generator"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <PackageReference Include="WrapGod.Analyzers"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <PackageReference Include="WrapGod.Targets"
                      Version="0.1.0-alpha"
                      PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <!-- The library to wrap -->
    <PackageReference Include="Shouldly" Version="4.3.0" />
    <WrapGodPackage Include="Shouldly" />
  </ItemGroup>
</Project>
```

### 3. Build

```bash
dotnet build
```

That's it. Three MSBuild targets fire automatically:

| Target | What it does |
|--------|--------------|
| `WrapGodRestore` | Resolves the Shouldly NuGet package into a local cache |
| `WrapGodExtract` | Reads the Shouldly DLL and produces `manifest.wrapgod.json` |
| `WrapGodGenerate` | Registers the manifest as an `AdditionalFile` so the Roslyn source generator emits wrappers |

### 4. See what was generated

After the build, look in your `obj/` directory:

```
obj/
  generated/
    WrapGod.Generator/
      WrapGod.Generator.WrapGodIncrementalGenerator/
        IWrappedShould.g.cs          # Wrapper interface
        ShouldFacade.g.cs            # Facade that delegates to the real type
        IWrappedShouldlyExtensions.g.cs
        ShouldlyExtensionsFacade.g.cs
```

Each wrapped type gets two files:
- **`IWrapped*.g.cs`** -- an interface your code programs against
- **`*Facade.g.cs`** -- a concrete class that delegates every call to the
  original Shouldly type

### 5. See the analyzer in action

If your code uses Shouldly directly, you'll see warnings:

```
warning WG2001: Direct usage of 'Shouldly.Should' which has a generated
               wrapper interface 'IWrappedShould'. Use the wrapper instead.
warning WG2002: Direct method call on 'Shouldly.Should.Equal' which has a
               generated facade 'ShouldFacade'. Use the facade instead.
```

Fix them all at once:

```bash
dotnet format analyzers --diagnostics WG2001 WG2002
```

Every `Shouldly.Should` reference is now `IWrappedShould`. Next time
Shouldly ships a breaking change, you update the version number, rebuild,
and the wrappers regenerate. Your code never notices.

---

## Path B: CLI Extraction + Build-Time Generation

More control over the extraction step. Useful when you want to inspect or
version-control the manifest before wiring it into the build.

### 1. Install the CLI

```bash
dotnet tool install --global WrapGod.Cli
```

### 2. Extract a manifest

```bash
wrap-god extract --nuget Shouldly@4.3.0 -o shouldly.wrapgod.json
```

Output:

```
Extracting: Shouldly 4.3.0
  Source: nuget.org
  Framework: net10.0
  Types: 12
  Members: 87
Written: shouldly.wrapgod.json (SHA-256: a1b2c3...)
```

The manifest is a JSON file describing every public type, method,
property, and generic constraint in Shouldly's API surface. You can
inspect it, diff it between versions, or check it into source control.

### 3. Add the manifest to your project

```xml
<ItemGroup>
  <!-- The extracted manifest -->
  <AdditionalFiles Include="shouldly.wrapgod.json" />
</ItemGroup>

<ItemGroup>
  <!-- Source generator + analyzers -->
  <PackageReference Include="WrapGod.Generator"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
  <PackageReference Include="WrapGod.Analyzers"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

### 4. Build and migrate

```bash
dotnet build
```

Same result as Path A -- interfaces and facades are generated, analyzers
flag direct usage, code fixes rewrite your call sites.

### 5. Multi-version extraction

Need to support Shouldly 3.x and 4.x simultaneously? Extract both:

```bash
wrap-god extract --nuget Shouldly@3.0.0 --nuget Shouldly@4.3.0 -o shouldly.wrapgod.json
```

The CLI merges both versions into a single manifest with
`introducedIn`/`removedIn` metadata on each member. The generator uses
this to produce version-aware wrappers. See [COMPATIBILITY.md](COMPATIBILITY.md)
for LCD, Targeted, and Adaptive modes.

---

## Path C: Programmatic API

For advanced users who want to integrate extraction into custom tooling,
CI pipelines, or code-generation workflows.

### Extract

```csharp
using WrapGod.Extractor;
using WrapGod.Manifest;

// Extract from a DLL on disk
ApiManifest manifest = AssemblyExtractor.Extract(@"path\to\Shouldly.dll");
```

### Serialize

```csharp
string json = ManifestSerializer.Serialize(manifest);
File.WriteAllText("shouldly.wrapgod.json", json);
```

### Multi-version extract

```csharp
var result = MultiVersionExtractor.Extract(new[]
{
    new MultiVersionExtractor.VersionInput("3.0.0", @"v3\Shouldly.dll"),
    new MultiVersionExtractor.VersionInput("4.3.0", @"v4\Shouldly.dll"),
});

ApiManifest merged = result.MergedManifest;
VersionDiff diff = result.Diff; // Added, removed, changed members
```

### Configure with the Fluent DSL

```csharp
using WrapGod.Fluent;

var plan = WrapGodConfiguration.Create()
    .ForAssembly("Shouldly")
    .WrapType("Shouldly.Should")
        .As("IShouldAssertions")
        .WrapAllPublicMembers()
    .ExcludeType("Shouldly.Internal*")
    .Build();
```

The manifest JSON and `GenerationPlan` are the same data structures used
by the source generator and MSBuild targets -- the programmatic API gives
you direct access to them.

---

## What Just Happened?

Regardless of which path you chose, WrapGod executed the same four-stage
pipeline:

```
1. EXTRACT        2. PLAN           3. GENERATE       4. ANALYZE + FIX
   Assembly DLL      Merge configs     Emit source       Flag direct usage
   ──────────►       ──────────►       ──────────►       ──────────►
   ApiManifest       TypeMappingPlan   IWrapped*.g.cs    WG2001 / WG2002
   (JSON)            GenerationPlan    *Facade.g.cs      Code-fix rewrites
```

**Stage 1 -- Extract.** WrapGod reads the public API surface of the
target assembly using `MetadataLoadContext` (no code is executed). Every
public type, member, parameter, and generic constraint is captured in a
deterministic JSON manifest with a SHA-256 hash for drift detection.

**Stage 2 -- Plan.** Configuration from JSON files, `[WrapType]`
attributes, and the Fluent DSL is merged through `ConfigMergeEngine`.
Conflicts are resolved by precedence rules and reported as diagnostics.
The output is a `TypeMappingPlan` and `GenerationPlan` that describe
exactly what to generate.

**Stage 3 -- Generate.** The Roslyn incremental source generator reads
the manifest from `AdditionalFiles` and emits `IWrapped*` interfaces and
`*Facade` proxy classes. Generated code is partitioned per type, so only
changed types trigger regeneration. Output lands in the
`WrapGod.Generated` namespace.

**Stage 4 -- Analyze + Fix.** The Roslyn analyzer scans your code for
direct references to the original types (`WG2001`) and direct method
calls (`WG2002`). Each diagnostic includes an automatic code fix that
rewrites the reference to use the generated wrapper. Apply them all with
`dotnet format` or one at a time in your IDE.

The net effect: your application code depends only on generated
interfaces. The vendor library is an implementation detail behind the
facade. When the library changes, you update the version, rebuild, and
the pipeline regenerates everything. Your code stays the same.

---

## Next Steps

- [CONFIGURATION.md](CONFIGURATION.md) -- JSON, attribute, and Fluent DSL configuration
- [COMPATIBILITY.md](COMPATIBILITY.md) -- LCD, Targeted, and Adaptive compatibility modes
- [AUTOMATION.md](AUTOMATION.md) -- end-to-end automation for upgrades and library swaps
- [ANALYZERS.md](ANALYZERS.md) -- full diagnostics reference (WG2001--WG6004)
- [MSBUILD-INTEGRATION.md](MSBUILD-INTEGRATION.md) -- MSBuild targets deep dive
- [MANIFEST.md](MANIFEST.md) -- manifest schema reference
