# Schema Generation from VersionDiff

`MigrationSchemaGenerator.FromDiff` converts a `VersionDiff` (produced by `MultiVersionExtractor`)
into a draft `MigrationSchema` with auto-inferred rules. This is the "generate the boring 80%"
pass that lives in front of human enrichment.

## Quick Start

```csharp
var diff = await new MultiVersionExtractor().DiffAsync(
    new[] { "MudBlazor.6.0.0.nupkg", "MudBlazor.7.0.0.nupkg" });

var schema = MigrationSchemaGenerator.FromDiff(
    diff,
    library: "MudBlazor",
    options: new MigrationSchemaGeneratorOptions { RuleIdPrefix = "MUD" });

File.WriteAllText("mudblazor.6.0-to-7.0.wrapgod-migration.json",
    MigrationSchemaSerializer.Serialize(schema));
```

## Diff → Rule Mapping

| VersionDiff entry | Generated rule kind | Confidence |
|---|---|---|
| `TypeRemoved` + matching `AddedType` (similarity ≥ threshold) | `RenameTypeRule` | `Auto` or `Verified` |
| `TypeRemoved` (no match) | `RemoveMemberRule` (synthetic, `TypeName = "<global>"`) | `Manual` |
| `MemberRemoved` + matching `AddedMember` on same declaring type | `RenameMemberRule` | `Auto` or `Verified` |
| `MemberRemoved` (no match) | `RemoveMemberRule` | `Manual` |
| `ReturnTypeChanged` | `ChangeTypeReferenceRule` | `Auto` |
| `ParameterTypesChanged` (arity same, one slot changed) | `ChangeParameterRule` per changed slot | `Auto` |
| `ParameterTypesChanged` (arity grew by 1, new param added) | `AddRequiredParameterRule` | `Manual` |
| `ParameterTypesChanged` (arity shrank or complex reshape) | `ChangeParameterRule` with descriptive `Note` | `Manual` |
| Namespace relocation (≥2 types moved from `OldNs.*` to `NewNs.*`) | Single `RenameNamespaceRule` | `Auto` |

## Similarity Thresholds

Rename detection uses **Jaro-Winkler** similarity on the short (unqualified) name, case-insensitive.

| Threshold | Default | Meaning |
|---|---|---|
| `RenameSimilarityThreshold` | `0.65` | Minimum similarity to treat a remove+add pair as a rename |
| `VerifiedSimilarityThreshold` | `0.85` | Similarity at which confidence is promoted from `Auto` to `Verified` |

An exact match (e.g., `FooButton` removed and `FooButton` added in a different namespace) always
produces `Verified` confidence.

## Deterministic Rule ID Allocation

Rule IDs are assigned after all rules are collected and sorted by `(KindOrdinal, DeclaringType, MemberName)`.
This guarantees that running `FromDiff` twice with the same inputs produces identical IDs.

The ID prefix is derived from the `library` parameter:

- Upper-case the library name, strip dots, truncate to 6 chars.
- Override with `MigrationSchemaGeneratorOptions.RuleIdPrefix` if provided.

Examples: `library = "MudBlazor"` → prefix `MUDBL`, `RuleIdPrefix = "MUD"` → prefix `MUD`.
IDs are `{PREFIX}-001`, `{PREFIX}-002`, …

## Options Reference

```csharp
new MigrationSchemaGeneratorOptions
{
    // Minimum similarity to detect a rename (0.0–1.0). Default: 0.65
    RenameSimilarityThreshold = 0.65,

    // Similarity at which confidence becomes Verified. Default: 0.85
    VerifiedSimilarityThreshold = 0.85,

    // Override the auto-derived rule ID prefix
    RuleIdPrefix = "MUD",

    // Set true to skip rename detection entirely (every remove becomes a RemoveMemberRule)
    DisableRenameDetection = false,
}
```

## Error Behavior

| Condition | Thrown exception |
|---|---|
| `diff` is null | `ArgumentNullException(nameof(diff))` |
| `library` is null | `ArgumentNullException(nameof(library))` |
| `library` is empty or whitespace | `ArgumentException(nameof(library))` |
| `diff.Versions.Count < 2` | `ArgumentException(nameof(diff))` with descriptive message |

Unknown or unmapped diff entries always produce a `Manual`-confidence rule with a descriptive
`Note` — the generator never throws on unmapped data.

## Namespace Relocation Detection

When two or more removed types in the same namespace (`OldNs`) each have an added counterpart
with an identical short name in the same new namespace (`NewNs`), the generator collapses the
per-type rules into a single `RenameNamespaceRule`. This avoids flooding the schema with
hundreds of individual rename rules when a library simply reorganized its namespace tree.

The per-type pairs are consumed and do not additionally generate `RenameTypeRule` entries.
