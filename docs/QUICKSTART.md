# Quick Start Guide

Get from zero to generated wrappers in under five minutes. Pick the path
that fits your workflow -- they all produce the same output.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or later)
- A third-party library you want to wrap (we'll use Newtonsoft.Json as our example)

---

## Path A: CLI Fast Track (recommended)

The fastest way to see WrapGod in action.

### 1. Install the CLI tool

```bash
dotnet tool install -g WrapGod.Cli
```

### 2. Extract a manifest from a NuGet package

```bash
wrap-god extract --nuget Newtonsoft.Json@13.0.3 -o newtonsoft.wrapgod.json
```

Expected output:

```
Extracting Newtonsoft.Json 13.0.3...
  Types: 37
Written to newtonsoft.wrapgod.json
```

The manifest is a JSON file describing every public type, method, property,
and generic constraint in the package.

### 3. Create a project and add WrapGod.Generator

```bash
dotnet new classlib -n MyApp
cd MyApp
dotnet add package WrapGod.Generator
```

### 4. Wire the manifest into your build

Add the manifest as an `AdditionalFiles` item in `MyApp.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="..\newtonsoft.wrapgod.json" />
</ItemGroup>
```

### 5. Build and see generated code

```bash
dotnet build
```

The source generator reads the manifest at compile time and emits:

- `IWrapped{TypeName}.g.cs` -- a wrapper interface for each public type
- `{TypeName}Facade.g.cs` -- a facade that delegates to the original type

Generated code lands in the `WrapGod.Generated` namespace.

### 6. Use the generated types

```csharp
using WrapGod.Generated;

// Program against the interface -- your code is decoupled from Newtonsoft
IWrappedJsonConvert converter = new JsonConvertFacade();
string json = converter.SerializeObject(new { Name = "WrapGod" });
```

---

## Path B: MSBuild Zero-Touch

The "I don't want to think about it" path. No CLI, no manual extraction --
just declare what to wrap and build.

### 1. Add WrapGod.Targets

```bash
dotnet add package WrapGod.Targets
```

### 2. Declare packages to wrap

In your `.csproj`:

```xml
<ItemGroup>
  <WrapGodPackage Include="Newtonsoft.Json" />
</ItemGroup>
```

### 3. Build

```bash
dotnet build
```

That's it. The MSBuild targets automatically:

1. **Restore** -- resolve the NuGet package
2. **Extract** -- produce the API manifest
3. **Generate** -- feed the manifest to the source generator

You get the same `IWrapped*` and `*Facade` files as Path A, with zero
manual steps. Incremental builds skip extraction when inputs haven't changed.

---

## Path C: Programmatic API

For tools, scripts, or custom pipelines that need to extract manifests in code:

```csharp
using WrapGod.Extractor;
using WrapGod.Manifest;

ApiManifest manifest = AssemblyExtractor.Extract(@"path\to\Vendor.Lib.dll");

string json = ManifestSerializer.Serialize(manifest);
File.WriteAllText("vendor-lib.wrapgod.json", json);
```

Multi-version extraction works the same way:

```csharp
var result = MultiVersionExtractor.Extract(new[]
{
    new MultiVersionExtractor.VersionInput("12.0.3", @"v12\Newtonsoft.Json.dll"),
    new MultiVersionExtractor.VersionInput("13.0.3", @"v13\Newtonsoft.Json.dll"),
});

ApiManifest merged = result.MergedManifest;   // annotated with version presence
VersionDiff  diff  = result.Diff;              // added, removed, changed members
```

---

## Path D: NuGet Multi-Version

Extract and diff multiple versions of a package in one command:

```bash
wrap-god extract --nuget Newtonsoft.Json@12.0.3 --nuget Newtonsoft.Json@13.0.3 \
  -o newtonsoft-multi.wrapgod.json
```

The merged manifest annotates every type and member with `introducedIn` /
`removedIn` metadata so the generator knows what's version-specific. The
CLI also prints a diff summary of breaking changes.

---

## What Just Happened?

Whichever path you chose, WrapGod ran a three-stage pipeline:

1. **Extract** -- scanned the vendor assembly and produced a JSON manifest of
   its entire public API surface
2. **Generate** -- a Roslyn source generator read that manifest at build time
   and emitted wrapper interfaces (`IWrapped*`) and facade classes (`*Facade`)
3. **Compile** -- your project compiled against the generated types, fully
   decoupled from the vendor's concrete classes

Your code now depends on generated abstractions, not vendor internals. When
the vendor ships a new version, WrapGod re-extracts, regenerates, and your
code either compiles cleanly or the analyzer tells you exactly what changed.

---

## Next Steps

- [CONFIGURATION.md](CONFIGURATION.md) -- JSON, attribute, and fluent
  configuration options
- [COMPATIBILITY.md](COMPATIBILITY.md) -- LCD, Targeted, and Adaptive
  compatibility modes for multi-version support
- [ANALYZERS.md](ANALYZERS.md) -- diagnostics that catch direct vendor usage
  and offer automated code fixes
- [AUTOMATION.md](AUTOMATION.md) -- how WrapGod eliminates vendor-upgrade
  tech debt across your organization
