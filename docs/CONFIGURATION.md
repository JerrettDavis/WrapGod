# Configuration Guide

WrapGod provides three configuration surfaces for controlling which types
and members are wrapped, how they are renamed, and what gets excluded.
All three produce the same normalized shape and can be combined with
well-defined merge precedence.

## JSON configuration

Create a `wrapgod.config.json` file (or any `.json` file loaded by
`JsonConfigLoader`). The format mirrors the `WrapGodConfig` model:

```json
{
  "types": [
    {
      "sourceType": "Vendor.Lib.HttpClient",
      "include": true,
      "targetName": "IHttpClient",
      "members": [
        {
          "sourceMember": "SendAsync",
          "include": true,
          "targetName": "SendRequestAsync"
        },
        {
          "sourceMember": "Dispose",
          "include": false
        }
      ]
    },
    {
      "sourceType": "Vendor.Lib.Logger",
      "include": true,
      "targetName": "ILogger",
      "members": []
    }
  ]
}
```

### Loading JSON config

```csharp
using WrapGod.Manifest.Config;

// From a file path
WrapGodConfig config = JsonConfigLoader.LoadFromFile("wrapgod.config.json");

// From a raw JSON string
WrapGodConfig config = JsonConfigLoader.LoadFromJson(jsonString);
```

The loader supports comments in JSON (`//` and `/* */`) and
case-insensitive property names.

### JSON config model

| Type | Property | Type | Description |
|------|----------|------|-------------|
| `WrapGodConfig` | `types` | `TypeConfig[]` | Per-type configuration entries. |
| `TypeConfig` | `sourceType` | `string` | Fully qualified source type name. |
| | `include` | `bool?` | Whether to include this type in generation. `null` = defer to other config sources. |
| | `targetName` | `string?` | Rename the generated wrapper (e.g. `"IHttpClient"`). |
| | `members` | `MemberConfig[]` | Per-member configuration entries. |
| `MemberConfig` | `sourceMember` | `string` | Source member name. |
| | `include` | `bool?` | Whether to include this member. |
| | `targetName` | `string?` | Rename the member on the generated wrapper. |

## Attribute-based configuration

Apply `[WrapType]` and `[WrapMember]` attributes from the
`WrapGod.Abstractions.Config` namespace directly on your wrapper contracts
or marker types.

### `[WrapType]`

```csharp
[WrapType("Vendor.Lib.HttpClient", TargetName = "IHttpClient")]
public interface IHttpClientWrapper { }
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `sourceType` | `string` (ctor) | Fully qualified source type to wrap. |
| `Include` | `bool` | Include this type in generation (default `true`). |
| `TargetName` | `string?` | Rename the generated wrapper. |

Source resolution for `[WrapType]` follows a deterministic discovery order:

1. Convention match in `WrapGodPackage`
2. Convention match in referenced packages/assemblies (first declared package reference wins)
3. `@self` (the attributed type itself)
4. Explicit metadata name from `sourceType`

If no source can be found, the generator emits `WG5001` with remediation
hints (for example, use `[WrapType("@self")]` for local types or provide a
fully-qualified metadata name).

Can be applied to classes, interfaces, and structs.

### `[WrapMember]`

```csharp
[WrapMember("SendAsync", TargetName = "SendRequestAsync")]
Task<Response> SendRequestAsync(Request request);
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `sourceMember` | `string` (ctor) | Source member name to wrap. |
| `Include` | `bool` | Include this member (default `true`). |
| `TargetName` | `string?` | Rename the member on the wrapper. |

Can be applied to methods and properties.

### Reading attribute config

```csharp
using WrapGod.Manifest.Config;

WrapGodConfig attrConfig = AttributeConfigReader.ReadFromAssembly(
    typeof(IHttpClientWrapper).Assembly);
```

## Fluent DSL

The fluent API in `WrapGod.Fluent` provides a programmatic builder that
produces a `GenerationPlan` -- the same normalized shape the JSON and
attribute readers produce.

```csharp
using WrapGod.Fluent;

GenerationPlan plan = WrapGodConfiguration.Create()
    .ForAssembly("Vendor.Lib")
    .WithCompatibilityMode("lcd")
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

### Fluent API reference

| Method | Returns | Description |
|--------|---------|-------------|
| `WrapGodConfiguration.Create()` | `WrapGodConfiguration` | Create a new builder. |
| `.ForAssembly(name)` | `WrapGodConfiguration` | Set the source assembly name. |
| `.WithCompatibilityMode(mode)` | `WrapGodConfiguration` | Set the compatibility mode (`"lcd"`, `"targeted"`, `"adaptive"`). |
| `.WrapType(sourceType)` | `TypeDirectiveBuilder` | Begin configuring a type wrapper. |
| `.MapType(source, dest)` | `WrapGodConfiguration` | Add a source-to-destination type mapping. |
| `.ExcludeType(pattern)` | `WrapGodConfiguration` | Exclude types matching a glob pattern. |
| `.Build()` | `GenerationPlan` | Finalize and produce the generation plan. |

**`TypeDirectiveBuilder`**:

| Method | Returns | Description |
|--------|---------|-------------|
| `.As(targetName)` | `TypeDirectiveBuilder` | Set the wrapper name. |
| `.WrapMethod(name)` | `MemberDirectiveBuilder` | Add a method directive. |
| `.WrapProperty(name)` | `TypeDirectiveBuilder` | Add a property directive. |
| `.ExcludeMember(name)` | `TypeDirectiveBuilder` | Exclude a member by name. |
| `.WrapAllPublicMembers()` | `TypeDirectiveBuilder` | Include all public members. |
| `.WrapType(sourceType)` | `TypeDirectiveBuilder` | Chain to another type (delegates to parent). |
| `.Build()` | `GenerationPlan` | Finalize (delegates to parent). |

**`MemberDirectiveBuilder`**:

| Method | Returns | Description |
|--------|---------|-------------|
| `.As(targetName)` | `TypeDirectiveBuilder` | Rename the member and return to the type builder. |

### `GenerationPlan` output

The `GenerationPlan` contains:

| Property | Type | Description |
|----------|------|-------------|
| `AssemblyName` | `string` | Source assembly name. |
| `TypeDirectives` | `TypeDirective[]` | Per-type wrapper directives. |
| `TypeMappings` | `TypeMapping[]` | Source-to-destination type mappings. |
| `ExclusionPatterns` | `string[]` | Glob patterns for excluded types. |
| `CompatibilityMode` | `string?` | Requested compatibility mode. |

## Merge precedence

WrapGod now supports a deterministic, convention-first precedence chain:

1. Defaults
2. Root JSON
3. Project JSON
4. Attributes
5. Fluent

Later layers always win.

```csharp
using WrapGod.Manifest.Config;

var result = ConfigMergeEngine.MergeByPrecedence(
    defaults,
    rootJson,
    projectJson,
    attributes,
    fluent);

WrapGodConfig merged = result.Config;
List<ConfigDiagnostic> conflicts = result.Diagnostics;
```

For backward compatibility, the two-source API is still available:

```csharp
var result = ConfigMergeEngine.Merge(jsonConfig, attributeConfig);
```

### Precedence rules

- The five-layer chain is always deterministic and left-to-right.
- In the two-source API:
  - **`ConfigSource.Attributes`** (default): attribute-defined values win
    over JSON values when both are present.
  - **`ConfigSource.Json`**: JSON-defined values win.

When a conflict is detected (two sources define different values for the
same setting), a `ConfigDiagnostic` is emitted:

| Code | Description |
|------|-------------|
| `WG6001` | Type `include` conflict. |
| `WG6002` | Type `targetName` conflict. |
| `WG6003` | Member `include` conflict. |
| `WG6004` | Member `targetName` conflict. |

If only one source provides a value, it is used regardless of the
precedence setting.

## Type mappings

Type mappings define source-to-destination type transformations used during
generation. They can be specified in the fluent DSL:

```csharp
.MapType("Vendor.Lib.Config", "MyApp.Config")
```

Or in the `GenerationPlan.TypeMappings` list:

```csharp
new TypeMapping
{
    SourceType = "Vendor.Lib.Config",
    DestinationType = "MyApp.Config",
}
```

These mappings instruct the generator to replace occurrences of the source
type with the destination type in generated wrapper signatures.
