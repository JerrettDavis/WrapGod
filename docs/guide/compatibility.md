# Compatibility Modes

When working with multi-version manifests, WrapGod's source generator can
filter the generated API surface based on a **compatibility mode**. This
determines which types and members make it into the generated interfaces
and facades.

The `CompatibilityFilter` applies the chosen mode to a `GenerationPlan`,
producing a new plan with only the appropriate members included.

## Modes

### LCD (Lowest Common Denominator)

```
CompatibilityMode.Lcd
```

Emit **only** types and members that are present in **every** extracted
version. A type/member qualifies when:

- It was introduced in the **earliest** version in the version list, AND
- It was **never removed** (no `removedIn` metadata).

This is the safest mode: generated code compiles and runs against any
version you extracted.

**Use case**: you ship a library that must work across all supported
versions of a vendor dependency.

### Targeted

```
CompatibilityMode.Targeted
```

Emit types and members present in a **single selected version**. A
type/member qualifies when:

- It was introduced **at or before** the target version, AND
- It was **not removed at or before** the target version.

Requires specifying the `targetVersion` parameter.

**Use case**: your application pins to a specific version of the vendor
library and you want the generated surface to match exactly.

### Adaptive

```
CompatibilityMode.Adaptive
```

Emit **all** types and members from the merged manifest. Members that are
version-specific are decorated with **runtime availability guards** in the
generated facade. At runtime, the facade checks
`WrapGodVersionHelper.IsMemberAvailable(introducedIn, removedIn)` before
forwarding a call. If the member is not available in the running version,
a `PlatformNotSupportedException` is thrown.

**Use case**: you want a single compiled binary that adapts its behavior
based on which version of the vendor library is present at runtime.

## API usage

```csharp
using WrapGod.Generator;

// Apply LCD mode
GenerationPlan filtered = CompatibilityFilter.Apply(
    plan,
    CompatibilityMode.Lcd,
    versions: new[] { "1.0.0", "2.0.0", "3.0.0" });

// Apply Targeted mode (pin to v2)
GenerationPlan filtered = CompatibilityFilter.Apply(
    plan,
    CompatibilityMode.Targeted,
    versions: new[] { "1.0.0", "2.0.0", "3.0.0" },
    targetVersion: "2.0.0");

// Apply Adaptive mode (keep everything)
GenerationPlan filtered = CompatibilityFilter.Apply(
    plan,
    CompatibilityMode.Adaptive,
    versions: new[] { "1.0.0", "2.0.0", "3.0.0" });
```

## Version presence rules

The filter uses `VersionPresence` metadata on each `TypePlan` and
`MemberPlan`:

| Metadata field | Meaning |
|---|---|
| `introducedIn` | The version label when the element first appeared. `null` means it was present from the earliest version. |
| `removedIn` | The version label when the element was removed. `null` means it is still present in the latest version. |

When both fields are `null`, the element is treated as present in all
versions and passes every filter mode.

## Runtime version checks (Adaptive mode)

In Adaptive mode, the `SourceEmitter` wraps version-specific members with
guards:

```csharp
// Generated facade method (Adaptive mode)
public Response SendAsync(Request request)
{
    if (!WrapGodVersionHelper.IsMemberAvailable("2.0.0", null))
        throw new System.PlatformNotSupportedException(
            "SendAsync is not available in the current version.");
    return _inner.SendAsync(request);
}
```

Property getters and setters receive the same treatment:

```csharp
public int Timeout
{
    // Available since 2.0.0
    get
    {
        if (!WrapGodVersionHelper.IsMemberAvailable("2.0.0", null))
            throw new System.PlatformNotSupportedException(
                "Timeout is not available in the current version.");
        return _inner.Timeout;
    }
    set
    {
        if (!WrapGodVersionHelper.IsMemberAvailable("2.0.0", null))
            throw new System.PlatformNotSupportedException(
                "Timeout is not available in the current version.");
        _inner.Timeout = value;
    }
}
```

Generated code also includes a comment indicating the availability window:

```
// Available since 2.0.0, removed in 4.0.0
```

## Choosing a mode

| Concern | LCD | Targeted | Adaptive |
|---------|-----|----------|----------|
| Compile-time safety across all versions | Yes | No (target only) | No (runtime checks) |
| Full API surface | No | Single version | Yes |
| Single binary for multiple versions | No | No | Yes |
| Runtime overhead | None | None | Guard check per call |
| Configuration complexity | Low | Low | Medium |

## Fluent DSL integration

Set the mode via the fluent builder:

```csharp
var plan = WrapGodConfiguration.Create()
    .ForAssembly("Vendor.Lib")
    .WithCompatibilityMode("lcd")   // or "targeted", "adaptive"
    .WrapType("Vendor.Lib.HttpClient")
        .As("IHttpClient")
        .WrapAllPublicMembers()
    .Build();
```
