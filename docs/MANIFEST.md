# Manifest Format Reference

The WrapGod manifest is the canonical description of a third-party
assembly's public API surface. It is serialized as JSON and serves as the
single source of truth for the source generator, compatibility filter, and
analyzer tooling.

## Schema overview

Manifests conform to the JSON Schema at
[`schemas/wrapgod.manifest.v1.schema.json`](../schemas/wrapgod.manifest.v1.schema.json).

Serialization conventions (handled by `ManifestSerializer`):

- Property names use **camelCase**
- Enums are stored as **lowercase strings**
- Null-valued properties are **omitted**
- Output is **indented** for readability

## Root object

| Property | Type | Description |
|---|---|---|
| `schemaVersion` | `string` | Always `"1.0"` for this format version. |
| `generatedAt` | `string` (ISO 8601) | UTC timestamp of extraction. |
| `sourceHash` | `string` | SHA-256 hex digest of the source assembly file. Used for drift detection. |
| `assembly` | `AssemblyIdentity` | Metadata identifying the source assembly. |
| `types` | `ApiTypeNode[]` | All public types, sorted by `stableId`. |

## Assembly identity

| Property | Type | Description |
|---|---|---|
| `name` | `string` | Assembly simple name (e.g. `"Vendor.Lib"`). |
| `version` | `string` | Assembly version string. |
| `culture` | `string?` | Culture string, or `null` for invariant. |
| `publicKeyToken` | `string?` | Hex-encoded public key token, or `null` if unsigned. |
| `targetFramework` | `string?` | Target framework moniker (e.g. `".NETCoreApp,Version=v8.0"`). |

## Type node (`ApiTypeNode`)

Each public type (class, struct, interface, enum, delegate) is represented
by an `ApiTypeNode`.

| Property | Type | Description |
|---|---|---|
| `stableId` | `string` | Stable identifier: `Namespace.TypeName` (including generic arity, e.g. `System.Collections.Generic.List\`1`). Nested types use `+` separator. |
| `fullName` | `string` | Fully qualified name with generic arguments expanded. |
| `name` | `string` | Simple type name. |
| `namespace` | `string` | Namespace (empty string for global types). |
| `kind` | `string` | One of: `class`, `struct`, `interface`, `enum`, `delegate`, `record`, `recordStruct`. |
| `baseType` | `string?` | Fully qualified base type, or `null` for interfaces / `System.Object`. |
| `interfaces` | `string[]` | Implemented interfaces, sorted alphabetically. |
| `genericParameters` | `GenericParameterInfo[]` | Generic type parameter definitions. |
| `isSealed` | `bool` | Whether the type is sealed (false for value types). |
| `isAbstract` | `bool` | Whether the type is abstract (false for interfaces). |
| `isStatic` | `bool` | Whether the type is static (`abstract sealed`). |
| `members` | `ApiMemberNode[]` | Public members, sorted by `stableId`. |
| `presence` | `VersionPresence?` | Version availability metadata (multi-version manifests only). |

## Member node (`ApiMemberNode`)

| Property | Type | Description |
|---|---|---|
| `stableId` | `string` | Stable identifier: `TypeStableId.MemberName(ParamTypes)`. |
| `name` | `string` | Member name (`.ctor` for constructors, `op_*` for operators). |
| `kind` | `string` | One of: `method`, `property`, `field`, `event`, `constructor`, `indexer`, `operator`. |
| `returnType` | `string?` | Return type (for methods, properties, fields, events). |
| `parameters` | `ApiParameterInfo[]` | Parameters (for methods, constructors, indexers). |
| `genericParameters` | `GenericParameterInfo[]` | Generic method parameters. |
| `isStatic` | `bool` | Whether the member is static. |
| `isVirtual` | `bool` | Whether the member is virtual/overridable. |
| `isAbstract` | `bool` | Whether the member is abstract. |
| `hasGetter` | `bool` | Whether a property has a public getter. |
| `hasSetter` | `bool` | Whether a property has a public setter. |
| `presence` | `VersionPresence?` | Version availability metadata. |

### Stable ID conventions

Stable IDs are designed to be deterministic and comparable across versions:

- **Types**: `Namespace.TypeName` (e.g. `Acme.Lib.HttpClient`)
- **Nested types**: `Outer+Inner`
- **Constructors**: `TypeId..ctor(ParamType1, ParamType2)`
- **Methods**: `TypeId.MethodName(ParamType1, ParamType2)`
- **Generic methods**: `TypeId.MethodName\`1(ParamType1)` (backtick + arity)
- **Properties**: `TypeId.PropertyName`
- **Indexers**: `TypeId.Item[ParamType1]`
- **Fields / Events**: `TypeId.MemberName`
- **Operators**: `TypeId.op_Addition(ParamType1, ParamType2)`

## Parameter info (`ApiParameterInfo`)

| Property | Type | Description |
|---|---|---|
| `name` | `string` | Parameter name. |
| `type` | `string` | Fully qualified parameter type. |
| `isOptional` | `bool` | Whether the parameter has a default value. |
| `isParams` | `bool` | Whether the parameter uses `params`. |
| `isOut` | `bool` | Whether the parameter is `out`. |
| `isRef` | `bool` | Whether the parameter is `ref` (non-`out`). |
| `defaultValue` | `string?` | String representation of the default value, if any. |

## Generic parameter info (`GenericParameterInfo`)

| Property | Type | Description |
|---|---|---|
| `name` | `string` | Type parameter name (e.g. `T`). |
| `constraints` | `string[]` | Constraint type names, sorted alphabetically. |

## Version presence metadata (`VersionPresence`)

Present only in manifests produced by `MultiVersionExtractor`. Tracks when
a type or member was introduced, removed, or changed across versions.

| Property | Type | Description |
|---|---|---|
| `introducedIn` | `string?` | Version label when this API element first appeared. |
| `removedIn` | `string?` | Version label when this API element was removed. |
| `changedIn` | `string?` | Version label when the signature changed. |

When all three are `null`, the element is present in every extracted
version.

## Multi-version extraction

The `MultiVersionExtractor` accepts an ordered list of `(VersionLabel,
AssemblyPath)` pairs and produces:

1. A **merged manifest** -- the union of all types and members across
   versions, annotated with `VersionPresence` on every node.
2. A **`VersionDiff`** report containing:
   - `addedTypes` / `removedTypes`
   - `addedMembers` / `removedMembers`
   - `changedMembers` (return type or parameter type changes)
   - `breakingChanges` (classified as `TypeRemoved`, `MemberRemoved`,
     `ReturnTypeChanged`, or `ParameterTypesChanged`)

The merged manifest uses the **latest version's** shape as canonical for
each type/member. The `VersionDiff` is useful for generating migration
guides or feeding into the compatibility filter.

### Example

```csharp
var result = MultiVersionExtractor.Extract(new[]
{
    new MultiVersionExtractor.VersionInput("1.0.0", @"v1\Vendor.Lib.dll"),
    new MultiVersionExtractor.VersionInput("2.0.0", @"v2\Vendor.Lib.dll"),
    new MultiVersionExtractor.VersionInput("3.0.0", @"v3\Vendor.Lib.dll"),
});

// Types/members only in v2+ will have: presence.introducedIn = "2.0.0"
// Types/members removed in v3 will have: presence.removedIn = "3.0.0"
```
