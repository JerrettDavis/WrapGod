# Quick Start Guide

This guide walks you through the core WrapGod workflow: extracting an API
manifest, configuring wrappers, generating code, and migrating existing
call sites with the built-in analyzers.

## Prerequisites

- .NET 10 SDK (or later, per `global.json` rollForward policy)
- The third-party assembly you want to wrap (e.g. `Vendor.Lib.dll`)

## 1. Bootstrap baseline files (optional but recommended)

```bash
# Creates wrapgod.root.json, wrapgod.project.json, wrapgod-types.txt, and docs/wrapgod-init.md
wrap-god init

# Preview only
wrap-god init --dry-run
```

The command is idempotent: existing files are not overwritten, so you can rerun it safely.

## 2. Install packages

Add the WrapGod packages to your project. All packages share a version and
are published from this repository.

```bash
# Core manifest + extractor
dotnet add package WrapGod.Manifest
dotnet add package WrapGod.Extractor

# Source generator (added as an analyzer)
dotnet add package WrapGod.Generator

# Analyzers + code fixes for migration
dotnet add package WrapGod.Analyzers

# Optional: fluent configuration DSL
dotnet add package WrapGod.Fluent

# Optional: attribute-based configuration
dotnet add package WrapGod.Abstractions
```

## 2. Extract a manifest from an assembly

Use `AssemblyExtractor` to produce a JSON manifest describing the public
API surface of a third-party assembly.

```csharp
using WrapGod.Extractor;
using WrapGod.Manifest;

// Single-version extraction
ApiManifest manifest = AssemblyExtractor.Extract(@"path\to\Vendor.Lib.dll");

// Serialize to JSON
string json = ManifestSerializer.Serialize(manifest);
File.WriteAllText("vendor-lib.wrapgod.json", json);
```

The manifest captures every public type, member, parameter, and generic
constraint. See [MANIFEST.md](MANIFEST.md) for the full schema reference.

### Multi-version extraction

When you need to support multiple versions of the same library, extract
each version and merge them into a single manifest with version presence
metadata:

```csharp
using WrapGod.Extractor;

var result = MultiVersionExtractor.Extract(new[]
{
    new MultiVersionExtractor.VersionInput("1.0.0", @"v1\Vendor.Lib.dll"),
    new MultiVersionExtractor.VersionInput("2.0.0", @"v2\Vendor.Lib.dll"),
});

ApiManifest merged = result.MergedManifest;
VersionDiff  diff  = result.Diff;
```

The merged manifest annotates each type and member with `presence` metadata
(`introducedIn`, `removedIn`) so the generator knows which API elements
are version-specific. The `VersionDiff` report lists added, removed, and
changed members plus classified breaking changes.

## 3. Configure wrappers

WrapGod supports three configuration surfaces that can be used independently
or combined (see [CONFIGURATION.md](CONFIGURATION.md) for full details):

### JSON configuration

Create a `wrapgod.config.json` alongside your project:

```json
{
  "types": [
    {
      "sourceType": "Vendor.Lib.HttpClient",
      "include": true,
      "targetName": "IHttpClient",
      "members": [
        { "sourceMember": "SendAsync", "include": true, "targetName": "SendRequestAsync" },
        { "sourceMember": "Dispose", "include": false }
      ]
    }
  ]
}
```

Load it at build time with:

```csharp
using WrapGod.Manifest.Config;

WrapGodConfig config = JsonConfigLoader.LoadFromFile("wrapgod.config.json");
```

### Attribute-based configuration

Annotate your wrapper contracts directly in C#:

```csharp
using WrapGod.Abstractions.Config;

[WrapType("Vendor.Lib.HttpClient", TargetName = "IHttpClient")]
public interface IHttpClientWrapper
{
    [WrapMember("SendAsync", TargetName = "SendRequestAsync")]
    Task<Response> SendRequestAsync(Request request);
}
```

Read attribute config with:

```csharp
WrapGodConfig attrConfig = AttributeConfigReader.ReadFromAssembly(typeof(IHttpClientWrapper).Assembly);
```

### Fluent DSL

Build configuration programmatically:

```csharp
using WrapGod.Fluent;

var plan = WrapGodConfiguration.Create()
    .ForAssembly("Vendor.Lib")
    .WrapType("Vendor.Lib.HttpClient")
        .As("IHttpClient")
        .WrapMethod("SendAsync").As("SendRequestAsync")
        .WrapProperty("Timeout")
        .ExcludeMember("Dispose")
    .WrapType("Vendor.Lib.Logger")
        .As("ILogger")
        .WrapAllPublicMembers()
    .MapType("Vendor.Lib.Config", "MyApp.Config")
    .ExcludeType("Vendor.Lib.Internal*")
    .Build();
```

## 5. Generate wrappers

The source generator runs automatically during the build. It reads
`*.wrapgod.json` manifest files included as **AdditionalFiles** in your
project.

### Project setup

Add the manifest to your `.csproj`:

```xml
<ItemGroup>
  <!-- The extracted manifest -->
  <AdditionalFiles Include="vendor-lib.wrapgod.json" />
</ItemGroup>

<ItemGroup>
  <!-- Reference the generator -->
  <PackageReference Include="WrapGod.Generator"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

Build the project:

```bash
dotnet build
```

The generator emits two files per wrapped type:

- `IWrapped{TypeName}.g.cs` -- the wrapper interface
- `{TypeName}Facade.g.cs` -- a facade class that delegates to the original type

Generated code lands in the `WrapGod.Generated` namespace.

## 5. Run analyzers to find direct usage

The WrapGod analyzer package detects call sites that reference the
original third-party type directly instead of the generated wrapper.

### Mapping file

Create a `*.wrapgod-types.txt` file listing the type mappings. Each line
uses the format:

```
Vendor.Lib.HttpClient -> IWrappedHttpClient, HttpClientFacade
```

Include it as an additional file:

```xml
<ItemGroup>
  <AdditionalFiles Include="vendor-lib.wrapgod-types.txt" />
</ItemGroup>
```

### Diagnostics

| ID     | Severity | Description |
|--------|----------|-------------|
| WG2001 | Warning  | Direct usage of a type that has a generated wrapper interface |
| WG2002 | Warning  | Direct method call on a type that has a generated facade |

See [ANALYZERS.md](ANALYZERS.md) for the full diagnostics reference.

## 6. Apply code fixes

Both diagnostics ship with automatic code fixes:

- **WG2001** -- replaces the type reference with the wrapper interface name
- **WG2002** -- replaces the receiver expression with the facade type

You can apply fixes one at a time or use **Fix All** (in your IDE or via
`dotnet format`) to migrate an entire project in one pass.

```bash
# Apply all WrapGod code fixes across the solution
dotnet format analyzers --diagnostics WG2001 WG2002
```

## 7. Validate environment health (`wrap-god doctor`)

Before wiring into CI, run doctor locally to verify prerequisites and setup:

```bash
wrap-god doctor
```

Machine-readable modes are available for pipeline integration:

```bash
wrap-god doctor --format json
wrap-god doctor --format sarif
```

## Next steps

- [MANIFEST.md](MANIFEST.md) -- manifest format reference
- [CONFIGURATION.md](CONFIGURATION.md) -- configuration guide (JSON, attributes, fluent, merge rules)
- [COMPATIBILITY.md](COMPATIBILITY.md) -- compatibility modes (LCD, Targeted, Adaptive)
- [ANALYZERS.md](ANALYZERS.md) -- analyzer diagnostics reference
