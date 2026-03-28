# WrapGod Architecture

WrapGod is a source-generation toolkit that creates strongly-typed wrapper
interfaces, facades, and mapper classes around third-party .NET libraries. Its
purpose is to decouple application code from vendor APIs so that library upgrades,
version-specific breaking changes, and cross-version compatibility can be managed
declaratively rather than through manual refactoring.

The system is organized as a multi-stage pipeline. Each stage consumes
well-defined inputs and produces well-defined outputs, keeping individual
components testable in isolation.

---

## Pipeline Overview

```
                               +-------------------+
                               | Assembly DLL(s)   |
                               +--------+----------+
                                        |
                               1. EXTRACT
                                        |
                                        v
                          +-------------+-------------+
                          | ApiManifest (JSON)        |
                          | + VersionDiff (if multi)  |
                          +-------------+-------------+
                                        |
      +----------------+    2. PLAN     |     +------------------+
      | JSON config    +--->---+--------+-<---+ [WrapType] attrs |
      | (.wrapgod.json)|       |              | (Abstractions)   |
      +----------------+       v              +------------------+
                        +------+------+
                        | Merged      |         +------------------+
                        | WrapGodConfig+--<-----+ Fluent DSL       |
                        +------+------+         | (WrapGodConfig)  |
                               |                +------------------+
                               v
                    +----------+----------+
                    | TypeMappingPlan      |
                    | GenerationPlan       |
                    +----------+----------+
                               |
                      3. GENERATE
                               |
                               v
               +---------------+---------------+
               | IWrapped* interfaces (.g.cs)  |
               | *Facade proxy classes (.g.cs) |
               | *Mapper static classes (.g.cs)|
               +---------------+---------------+
                               |
                      4. ANALYZE + FIX
                               |
                               v
               +---------------+---------------+
               | WG2001 / WG2002 diagnostics   |
               | Automated code-fix rewrites   |
               +-------------------------------+
```

---

## Component Dependency Graph

```
WrapGod.Abstractions          (netstandard2.0 -- attributes + config models)
    ^           ^
    |           |
    |     WrapGod.Manifest    (netstandard2.0 -- manifest models + config loaders)
    |           ^
    |           |
    |     WrapGod.Extractor   (net10.0 -- reflection-based extraction)
    |
    +-----WrapGod.TypeMap     (netstandard2.0 -- mapping plan + mapper emitter)
    |
    +-----WrapGod.Fluent      (netstandard2.0 -- fluent DSL builder)
    |
    +-----WrapGod.Generator   (netstandard2.0 -- Roslyn incremental generator)
    |
    +-----WrapGod.Analyzers   (netstandard2.0 -- Roslyn analyzer + code fix)
    |
    +-----WrapGod.Runtime     (net10.0 -- runtime helpers, e.g. version guards)
```

All generator and analyzer assemblies target `netstandard2.0` to be loadable
by any Roslyn host (VS, VS Code, `dotnet build`). The extractor, which uses
`MetadataLoadContext`, targets `net10.0`.

---

## Data Flow

The canonical data flow from raw assembly to generated source is:

1. **Assembly DLL** -- a compiled third-party library on disk.
2. **`ApiManifest` (JSON)** -- the complete public API surface serialized as a
   deterministic, schema-versioned JSON document.
3. **`WrapGodConfig` (merged)** -- the union of JSON overrides, `[WrapType]`/
   `[WrapMember]` attributes, and fluent DSL directives, resolved through
   `ConfigMergeEngine` with configurable precedence.
4. **`TypeMappingPlan` + `GenerationPlan`** -- the normalized, ready-to-emit
   representation consumed by the generator.
5. **Source output** -- `IWrapped*` interfaces, `*Facade` proxy classes, and
   `*Mapper` static mapping classes injected into the compilation.

---

## Component Reference

### WrapGod.Abstractions

**Purpose.** Shared contracts consumed by every other project. Contains the
attribute definitions and configuration model types.

| Key type | Role |
|---|---|
| `WrapTypeAttribute` | Marks a type for wrapper generation; sets `SourceType`, `Include`, `TargetName`. |
| `WrapMemberAttribute` | Marks a member for wrapper generation; sets `SourceMember`, `Include`, `TargetName`. |
| `WrapGodConfig` | Root config model: list of `TypeConfig` entries. |
| `TypeConfig` | Per-type configuration: `SourceType`, `Include`, `TargetName`, list of `MemberConfig`. |
| `MemberConfig` | Per-member configuration: `SourceMember`, `Include`, `TargetName`. |
| `ConfigMergeResult` | Output of merge: merged `WrapGodConfig` + list of `ConfigDiagnostic`. |
| `ConfigMergeOptions` | Controls which source (JSON or Attributes) takes precedence on conflict. |

**Inputs.** None (leaf dependency).
**Outputs.** Attribute types (consumed at compile time) and config model types (consumed at plan time).

---

### WrapGod.Extractor

**Purpose.** Reads the public API surface of one or more .NET assemblies and
produces an `ApiManifest`. Uses `System.Reflection.MetadataLoadContext` for
safe, reflection-only loading -- no assembly is ever executed.

| Key type | Role |
|---|---|
| `AssemblyExtractor` | Extracts a single assembly into an `ApiManifest`. Computes a SHA-256 `SourceHash` for drift detection. |
| `MultiVersionExtractor` | Accepts an ordered list of `(VersionLabel, AssemblyPath)` pairs, extracts each, then merges into a single manifest with `VersionPresence` metadata and a `VersionDiff` compatibility report. |
| `VersionDiff` | Machine-readable report: added/removed types, added/removed/changed members, classified `BreakingChange` entries. |

**Inputs.** One or more assembly DLLs on disk.
**Outputs.** `ApiManifest` (single-version) or `MultiVersionResult` (merged manifest + diff).

---

### WrapGod.Manifest

**Purpose.** Defines the `ApiManifest` model hierarchy and provides serialization,
config loading, and config merging.

| Key type | Role |
|---|---|
| `ApiManifest` | Root model: `SchemaVersion`, `GeneratedAt`, `Assembly` identity, `SourceHash`, list of `ApiTypeNode`. |
| `ApiTypeNode` | Public type: `StableId`, `FullName`, `Kind` (class/struct/interface/enum/delegate), modifiers, members, `VersionPresence`. |
| `ApiMemberNode` | Public member: `StableId`, `Kind` (method/property/field/event/constructor/indexer/operator), signature details, `VersionPresence`. |
| `VersionPresence` | Records `IntroducedIn`, `RemovedIn`, `ChangedIn` version labels. |
| `ManifestSerializer` | Deterministic JSON serialization (camelCase, enum-as-string, null-suppressed). |
| `JsonConfigLoader` | Loads a `WrapGodConfig` from a JSON file or string. |
| `AttributeConfigReader` | Scans an assembly for `[WrapType]`/`[WrapMember]` attributes and produces a `WrapGodConfig`. |
| `ConfigMergeEngine` | Merges JSON-sourced and attribute-sourced configs. On conflict, applies `ConfigMergeOptions.HigherPrecedence` and emits `ConfigDiagnostic` entries. |

**Inputs.** Assembly DLLs (via extractor), JSON config files, attribute-decorated assemblies.
**Outputs.** `ApiManifest` model, merged `WrapGodConfig`.

---

### WrapGod.Fluent

**Purpose.** Provides a programmatic, chainable API for defining wrapper
configuration in C# code rather than JSON or attributes.

| Key type | Role |
|---|---|
| `WrapGodConfiguration` | Fluent entry point. `Create()` / `ForAssembly()` / `WrapType()` / `MapType()` / `ExcludeType()` / `Build()`. |
| `TypeDirectiveBuilder` | Nested builder returned by `WrapType()`. Supports `.As()`, `.WrapMethod()`, `.WrapProperty()`, `.ExcludeMember()`, `.WrapAllPublicMembers()`. |
| `MemberDirectiveBuilder` | Nested builder returned by `WrapMethod()`. Supports `.As()` for rename. |
| `GenerationPlan` (Fluent) | The normalized output of the builder: `TypeDirectives`, `TypeMappings`, `ExclusionPatterns`, `CompatibilityMode`. |
| `TypeDirective` | Per-type directive: source type, target name, member directives, excluded members. |
| `MemberDirective` | Per-member directive: source name, target name, kind (Method/Property). |

**Inputs.** Developer code calling the fluent API.
**Outputs.** `GenerationPlan` (consumed by generation pipeline).

---

### WrapGod.TypeMap

**Purpose.** Builds a type-mapping plan from merged config, then emits static
mapper classes that convert between source and destination types.

| Key type | Role |
|---|---|
| `TypeMapping` | Maps a source type to a destination type with a `TypeMappingKind` and per-member mappings. |
| `TypeMappingKind` | Enum: `ObjectMapping`, `Enum`, `Collection`, `Nullable`, `Custom`. |
| `MemberMapping` | Source member to destination member, with optional `ConverterRef`. |
| `ConverterRef` | Reference to a user-defined converter (type name + optional method name). |
| `TypeMappingPlan` | Collection of `TypeMapping` entries with lookup helpers. |
| `TypeMappingPlanner` | Builds a `TypeMappingPlan` from `WrapGodConfig` + optional `TypeMappingOverride` list. |
| `TypeMappingOverride` | Explicit override: force mapping kind or converter for a source type. |
| `TypeMapperEmitter` | Generates C# static mapper classes from a `TypeMappingPlan`. |
| `MapperSourceBuilder` | Indentation-aware `StringBuilder` wrapper for well-formatted C# output. |

**Inputs.** Merged `WrapGodConfig`, optional `TypeMappingOverride` list.
**Outputs.** `TypeMappingPlan` model; generated `*Mapper` source text.

---

### WrapGod.Generator

**Purpose.** Roslyn incremental source generator that reads `*.wrapgod.json`
manifest files from `AdditionalFiles`, parses them into generation plans, and
emits wrapper interfaces and facade proxy classes.

| Key type | Role |
|---|---|
| `WrapGodIncrementalGenerator` | The `[Generator]` entry point. Implements `IIncrementalGenerator`. Filters `AdditionalTexts` to `*.wrapgod.json`, parses each into a `GenerationPlan`, then calls `SourceEmitter` to produce output. |
| `GenerationPlan` (Generator) | Lightweight, `IEquatable<T>` model parsed from manifest JSON. Drives incremental caching. |
| `TypePlan` | Per-type plan: `FullName`, `Name`, `Namespace`, `Members`, version metadata (`IntroducedIn`/`RemovedIn`), optional `TargetName`. |
| `MemberPlan` | Per-member plan: `Name`, `Kind`, `ReturnType`, `Parameters`, `GenericParameters`, version metadata. |
| `ParameterPlan` | Parameter descriptor: `Name`, `Type`, `Modifier`. |
| `SourceEmitter` | String-based emitter that produces `IWrapped*` interface source and `*Facade` proxy class source from `TypePlan` models. Supports an `AdaptiveMode` flag for version-guarded member access. |
| `CompatibilityMode` | Enum: `Lcd` (lowest common denominator), `Targeted` (single version), `Adaptive` (all members with runtime guards). |
| `CompatibilityFilter` | Filters a `GenerationPlan` based on `CompatibilityMode`, stripping members that do not belong in the selected mode. |

**Inputs.** `*.wrapgod.json` additional files (manifest JSON).
**Outputs.** `IWrapped*.g.cs` interface files, `*Facade.g.cs` proxy class files.

---

### WrapGod.Analyzers

**Purpose.** Roslyn analyzer and code-fix provider that detects direct usage of
third-party types that have generated wrappers and offers automated migration.

| Key type | Role |
|---|---|
| `DirectUsageAnalyzer` | `[DiagnosticAnalyzer]`. Reads `*.wrapgod-types.txt` additional files to build a mapping from original types to their wrapper interface and facade. Reports `WG2001` (direct type usage) and `WG2002` (direct method call). |
| `UseWrapperCodeFixProvider` | `[ExportCodeFixProvider]`. Fixes `WG2001` by replacing type references with the wrapper interface; fixes `WG2002` by replacing method receivers with the facade type. Supports `FixAll` via `BatchFixer`. |
| `DiagnosticDescriptors` | Static descriptor definitions for `WG2001` and `WG2002`. |

**Inputs.** `*.wrapgod-types.txt` additional files (one mapping per line: `Original -> IWrapper, Facade`), user source code.
**Outputs.** Diagnostics (warnings), automated code-fix rewrites.

---

### WrapGod.Runtime

**Purpose.** Provides runtime helpers referenced by generated code. Notably,
the `WrapGodVersionHelper.IsMemberAvailable()` method used by Adaptive-mode
facades to gate member access based on the detected library version at runtime.

**Status.** Scaffold only -- implementation pending.

**Inputs.** Called by generated facade code at runtime.
**Outputs.** Boolean availability checks.

---

## Compatibility Modes

WrapGod supports three strategies for handling API members that exist in only a
subset of the targeted library versions:

| Mode | Behavior | Use case |
|---|---|---|
| **LCD** (Lowest Common Denominator) | Emit only members present in *every* targeted version. | Maximum portability; no runtime version checks needed. |
| **Targeted** | Emit members present in a single specified version. | Pinned deployment against a known library version. |
| **Adaptive** | Emit all members; version-specific ones are wrapped with `WrapGodVersionHelper.IsMemberAvailable()` guards that throw `PlatformNotSupportedException` if the member is unavailable at runtime. | Libraries that must support multiple versions simultaneously. |

---

## Extension Points

### Custom Converters

`ConverterRef` allows users to plug in custom conversion logic at both the
type level and the member level. The generator delegates to the referenced
static method instead of emitting default property-copy code.

```csharp
// Example: type-level converter
new TypeMappingOverride
{
    SourceType = "Vendor.Lib.LegacyDate",
    Kind = TypeMappingKind.Custom,
    Converter = new ConverterRef
    {
        TypeName = "MyApp.Converters.LegacyDateConverter",
        MethodName = "Convert"
    }
};
```

### Config Sources

Three config sources feed into `ConfigMergeEngine`:

1. **JSON** -- `*.wrapgod.json` files loaded by `JsonConfigLoader`.
2. **Attributes** -- `[WrapType]` / `[WrapMember]` scanned by `AttributeConfigReader`.
3. **Fluent DSL** -- `WrapGodConfiguration` builder producing a `GenerationPlan`.

Precedence on conflict is controlled by `ConfigMergeOptions.HigherPrecedence`
(default: Attributes win). Conflicts emit `ConfigDiagnostic` entries with codes
`WG6001`--`WG6004`.

### Analyzer Rules

The analyzer reads mapping data from `*.wrapgod-types.txt` additional files.
Adding new entries to this file (or generating it as a build artifact from the
manifest) automatically extends analyzer coverage without code changes.

### Diagnostics Report Formats (RFC-0054)

`WrapGod.Abstractions.Diagnostics.WgDiagnosticEmitter` is the canonical formatter
entry point for structured diagnostics:

- `EmitJson(...)` emits the `wg.diagnostic.v1` contract records.
- `EmitSarif(...)` emits SARIF 2.1.0 for CI/security tooling integration.

SARIF output projects a stable WG rule catalog into
`runs[0].tool.driver.rules[]` (one entry per `WG####`) and maps diagnostic
severity, locations, related locations, fingerprints, and suppression metadata
from the same canonical model used by JSON output.

---

## Determinism and Reproducibility

- **Stable IDs.** Every type and member receives a deterministic `StableId`
  derived from its namespace, name, and parameter signature. These IDs are
  used for cross-version presence tracking and diffing.
- **Schema versioning.** `ApiManifest.SchemaVersion` enables forward
  compatibility; consumers can reject manifests with an unsupported schema.
- **Source hashing.** `AssemblyExtractor` computes a SHA-256 hash of the input
  DLL and embeds it in `ApiManifest.SourceHash`. Downstream stages can detect
  drift when a manifest is stale relative to its source artifact.
- **Deterministic sorting.** Types and members within a manifest are sorted by
  `StableId` using ordinal comparison, ensuring identical output regardless of
  reflection enumeration order.

---

## Performance Notes

### Incremental Generator Caching

`WrapGodIncrementalGenerator` leverages Roslyn's incremental pipeline
(`IncrementalGeneratorInitializationContext`) to avoid redundant work:

- `GenerationPlan`, `TypePlan`, `MemberPlan`, and `ParameterPlan` all implement
  `IEquatable<T>` with value-based equality.
- When a `*.wrapgod.json` file changes, only its corresponding `GenerationPlan`
  is re-parsed. If the parsed plan is structurally equal to the previous one,
  no source is re-emitted.
- Plans are collected via `.Collect()` into an `ImmutableArray`, enabling a
  single `RegisterSourceOutput` callback that emits all files.

### Partitioned Output

Generated source files are partitioned by type (`IWrapped*.g.cs` and
`*Facade.g.cs` per type). This limits the blast radius of a single type change
-- only that type's files are regenerated.

### No Reflection in Generator Path

All reflection-based metadata loading is confined to `WrapGod.Extractor`.
The generator and analyzer paths operate purely on serialized manifest data and
Roslyn symbol models, avoiding the cost and complexity of runtime reflection.
