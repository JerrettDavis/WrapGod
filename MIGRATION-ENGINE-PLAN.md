# WrapGod Migration Engine — Master Execution Plan

**Plan version:** 1.0
**Author:** Architecture planning pass, 2026-05-27
**Scope:** Close every open issue on https://github.com/JerrettDavis/WrapGod/issues (17 total)
**Authoritative design source:** `docs/plans/2026-04-01-migration-engine-design.md` (status: Accepted)

This plan is the mechanical execution blueprint. Every issue gets a block with SDD/BDD/TDD specs, file paths, docs, samples, and `done when` criteria. Implementer agents should be able to pick up a single block and ship it without further design decisions.

---

## Section 1 — Execution Order

### 1.1 Topological order

The 12 implementation issues form a DAG rooted at `#192` (already closed). The 5 epic/RFC issues sit on top of the implementation graph and close as a function of their children.

```
#192 (CLOSED) ──┬──> #193 ──> #198 ──┐
                │                     │
                ├──> #194 ──> #195 ──┬──> #196 ──> #197 ──┬──> #199 ──> #201
                │                    │                    │     │
                │                    └──> #202            │     ├──> #200
                │                                         │     │
                │                                         └─────┴──> #203 ──> #204
                │
                └─────────────────────────────────────────────────────> (epic #191 closes when 193-204 done)

#1  closes when remaining tracked sub-issues are closed (most are already closed; assessment in §3).
#53 closes via deferred-RFC closure comment (parked).
#155, #163 close via "epic complete, file follow-ups" comment — all listed children already closed.
```

### 1.2 Ordered work list (numbered)

The mandatory ordering — each later item depends on at least one earlier item (or runs in parallel where shown):

1. **#193** Schema generator from `VersionDiff`
2. **#194** `WrapGod.Migration.Engine` scaffold + `IRuleRewriter` interface — *parallel with #193*
3. **#195** A-level syntax rewriters (7 rewriters)
4. **#198** CLI `migrate generate` — *parallel with #195 once #193 is done*
5. **#196** `MigrationEngine` orchestrator (Apply + DryRun)
6. **#202** B-level structural rewriters (4 rewriters) — *parallel with #197 once #196 is done*
7. **#197** State tracking, idempotent re-runs, schema hash
8. **#199** CLI `migrate apply --dry-run`
9. **#200** CLI `migrate status [--json]` — *parallel with #199 once #197 is done*
10. **#201** CLI `migrate verify`
11. **#203** E2E example with real library (Serilog v2 → v3)
12. **#204** Docs: migration authoring + applying, README updates
13. **#191** Epic close (auto when 193-204 are closed)
14. **#155** Bidirectional wrappers epic — narrow-scope close (children already shipped)
15. **#163** NuGet competing-version epic — narrow-scope close (children already shipped)
16. **#1** MVP Master Plan — close (sub-issues #68-71, #73-74, #76, #81-83 are all closed)
17. **#53** Deferred IDE RFC — convert/close with parked-vNext comment

### 1.3 Critical path

The longest chain through the DAG that determines minimum wall-clock time:

> **#193 → #196 → #197 → #199 → #201 → #203 → #204 → #191**

That is 8 sequential issues. Everything else (`#194` scaffolding, `#195` A-level rewriters, `#198` generate CLI, `#202` B-level rewriters, `#200` status CLI, all epic closures) parallelizes off this trunk.

### 1.4 Parallelism opportunities

Once a barrier is cleared, the following sets can run concurrently on separate branches:

| After this lands | These can run in parallel |
|---|---|
| `#192` (already done) | `#193`, `#194` |
| `#193` | `#198` (CLI generate) starts in parallel with `#194`-track work |
| `#194` | `#195` (A-level rewriters) |
| `#195` | `#196` |
| `#196` | `#197`, `#202` (B-level rewriters) |
| `#197` | `#199`, `#200` (apply + status) |
| `#199` | `#201` (verify) |
| `#201`+`#202` | `#203` (E2E example) |
| `#203` | `#204` (docs) |
| All implementation closed | `#191`, then `#1`, `#155`, `#163`, `#53` epic closures (these 5 are independent) |

### 1.5 Branch / PR convention

* One feature branch per issue: `feat/migration-<issue-number>-<slug>` (e.g. `feat/migration-194-engine-scaffold`).
* PRs target `main`. Each PR closes its single issue via `Closes #N` in the description.
* Merge style: **merge commit** (not squash) so per-commit messages are preserved. Issues #155 and #163 sub-issues used squash-merge in the past and that wiped intermediate context — do not repeat that.
* Each PR must be green (build + tests + 90% coverage gate) before merge. No `--no-verify`.
* Before merging epic-closure PRs (#191, #1, #155, #163, #53), confirm the closure comment is correct.

---

## Section 2 — Per-Issue Detailed Plan

> NOTE on issue numbering: `#192` is already closed (scaffold project landed). It is included here only as the dependency baseline. The 12 implementation issues plus 5 epics = 17 issues all addressed below.

---

## Issue #193 — Migration Engine: Schema generator from VersionDiff

### a) Specification (SDD)

**Introduces:** A pure function that converts a `VersionDiff` (output of `MultiVersionExtractor`) into a draft `MigrationSchema` with auto-inferred rules. This is the "generate the boring 80%" pass that lives in front of human enrichment.

**Public API:**

```csharp
namespace WrapGod.Migration.Generation;

public static class MigrationSchemaGenerator
{
    public static MigrationSchema FromDiff(
        VersionDiff diff,
        string library,
        MigrationSchemaGeneratorOptions? options = null);
}

public sealed class MigrationSchemaGeneratorOptions
{
    /// <summary>Minimum similarity (0.0–1.0) to treat a "remove + add" as a rename. Default 0.65.</summary>
    public double RenameSimilarityThreshold { get; init; } = 0.65;

    /// <summary>Similarity at which the rule's confidence becomes <c>verified</c>. Default 0.85.</summary>
    public double VerifiedSimilarityThreshold { get; init; } = 0.85;

    /// <summary>Prefix for auto-generated rule IDs, e.g. "MUD" yields "MUD-001". Defaults to library uppercased and truncated to 6 chars.</summary>
    public string? RuleIdPrefix { get; init; }

    /// <summary>Disable rename detection (always emit remove + add). Default false.</summary>
    public bool DisableRenameDetection { get; init; }
}
```

**Mapping table (canonical):**

| `BreakingChangeKind` | Generated rule kind | Confidence assignment |
|---|---|---|
| `TypeRemoved` (no similar new type) | `RemoveMemberRule` (synthetic on type-level) → actually emit `RemoveMemberRule` with `TypeName = full, MemberName = ".type"` *OR* emit as a synthetic `RemoveType` note (see below) | `Manual` |
| `TypeRemoved` + similar `AddedType` (`sim ≥ RenameSimilarityThreshold`) | `RenameTypeRule` | `Auto` (or `Verified` if `sim ≥ VerifiedSimilarityThreshold`) |
| `MemberRemoved` (no similar new member on same type) | `RemoveMemberRule` | `Manual` |
| `MemberRemoved` + similar `AddedMember` on same type | `RenameMemberRule` | `Auto` / `Verified` per threshold |
| `ReturnTypeChanged` | `ChangeTypeReferenceRule` | `Auto` |
| `ParameterTypesChanged` (same arity, single positional swap) | `ChangeParameterRule` (one rule per swapped slot) | `Auto` |
| `ParameterTypesChanged` (arity grew by 1, new param has no default) | `AddRequiredParameterRule` | `Manual` (we cannot know the right argument value) |
| `ParameterTypesChanged` (arity shrank or shape entirely different) | `ChangeParameterRule` with `Note` describing reshape | `Manual` |
| Namespace-only relocation (`MudBlazor.Components.X` → `MudBlazor.X`) detected by matching short name + identical member shape | `RenameNamespaceRule` (one rule per distinct old-namespace prefix) | `Auto` |

**Decision: synthetic "RemoveType" handling.** The existing rule model has no `RemoveType` kind. For a removed top-level type with no rename target, emit a `RemoveMemberRule` with `TypeName = "<global>"` and `MemberName = "<full type name>"`, plus a `Note` describing it. (This avoids a schema-level change. If consumers want a richer model, file follow-up.)

**Similarity algorithm:** Use **Jaro-Winkler** over the short name (case-insensitive); for members, additionally require identical declaring-type stable ID and (for methods) identical parameter arity. Compute once per candidate pair.

**Rule ID generation:**
- Prefix = `options.RuleIdPrefix ?? library.ToUpperInvariant().Replace(".", "").Substring(0, Math.Min(6, len))`.
- Sequential: `{PREFIX}-001`, `{PREFIX}-002`, …
- IDs are deterministic for a given `(VersionDiff, library, options)` triple — sort rules before assigning IDs by `(Kind ordinal, declaringType, memberName)` so re-runs produce identical IDs.

**Schema metadata set on output:**
- `Schema = "wrapgod-migration/1.0"`
- `Library = library` (parameter)
- `From = diff.Versions.First()`
- `To = diff.Versions.Last()`
- `GeneratedFrom = "manifest-diff"`
- `LastEdited = DateTimeOffset.UtcNow`

**Error behavior:**
- Throws `ArgumentNullException` for null `diff` or null `library`.
- Throws `ArgumentException` if `library` is empty/whitespace.
- Throws `ArgumentException` if `diff.Versions.Count < 2`.
- Never throws on "unmapped" diff entries — instead, emits a `Manual`-confidence catch-all rule with a descriptive `Note`. The goal is "always produce a schema, even if some rules are vague."

**Acceptance criteria (verbatim from issue):**

- [ ] `MigrationSchemaGenerator.FromDiff(VersionDiff, string)` returns a `MigrationSchema`.
- [ ] Renames detected when similarity > threshold.
- [ ] Parameter reorderings detected and mapped to `ChangeParameter`.
- [ ] Semantic unknowns marked `confidence: "manual"` with descriptive `note`.
- [ ] Unit tests with known diffs producing expected schemas.
- [ ] Integration test: extract two real NuGet versions, generate schema, verify rules.

### b) Behavior (BDD) — TinyBDD scenarios

Group: **happy**
1. *Given* a `VersionDiff` with a single `TypeRemoved` and a matching `AddedType` (similarity 1.0), *when* `FromDiff` is called, *then* the schema contains exactly one `RenameTypeRule` with `Confidence.Verified`.
2. *Given* a `VersionDiff` with a `MemberRemoved` and an `AddedMember` on the same declaring type with similarity 0.9, *when* `FromDiff` is called, *then* the schema contains a `RenameMemberRule` with `Confidence.Verified`.
3. *Given* a `VersionDiff` with one `ReturnTypeChanged` entry, *when* `FromDiff` is called, *then* the schema contains one `ChangeTypeReferenceRule` with `Confidence.Auto`.
4. *Given* a `VersionDiff` that produces multiple rules, *when* `FromDiff` is called, *then* rule IDs are sequential (`LIB-001`, `LIB-002`, …) and stable across two invocations.
5. *Given* a `VersionDiff` with three removed types all matched by namespace prefix to added types, *when* `FromDiff` is called, *then* a single `RenameNamespaceRule` is emitted plus zero per-type renames.

Group: **sad**
6. *Given* a null `VersionDiff`, *when* `FromDiff` is called, *then* it throws `ArgumentNullException`.
7. *Given* an empty `library` string, *when* `FromDiff` is called, *then* it throws `ArgumentException`.
8. *Given* a `VersionDiff.Versions` list with only one entry, *when* `FromDiff` is called, *then* it throws `ArgumentException` with a descriptive message.
9. *Given* a `VersionDiff` with a `MemberRemoved` and no similar add, *when* `FromDiff` is called, *then* a `RemoveMemberRule` with `Confidence.Manual` and a non-null `Note` is emitted.
10. *Given* a `VersionDiff` where parameter arity grew by 1 with no default, *when* `FromDiff` is called, *then* an `AddRequiredParameterRule` with `Confidence.Manual` is emitted.

Group: **edge**
11. *Given* a `VersionDiff` where two removed types each have identical similarity (0.7) to a single added type, *when* `FromDiff` is called, *then* exactly one rename is emitted (deterministic tiebreak by stable ID lexicographic order) and the loser becomes a `RemoveMemberRule` with `Manual` confidence and a note explaining the ambiguity.
12. *Given* `DisableRenameDetection = true`, *when* `FromDiff` is called on a diff with obvious renames, *then* the schema contains only `RemoveMemberRule` + (implicit add — nothing emitted for adds, but the removes are present) and no rename rules.
13. *Given* a custom `RuleIdPrefix = "X"`, *when* `FromDiff` is called, *then* all rules are `X-001`, `X-002`, … regardless of `library` name.
14. *Given* a `VersionDiff` with zero changes, *when* `FromDiff` is called, *then* the schema's `Rules` list is empty but other metadata fields are populated.
15. *Given* a `VersionDiff` produced from MudBlazor 6.0.0 → 7.0.0 (real NuGet fetch), *when* `FromDiff` is called, *then* the schema has ≥ 1 rule and round-trips through `MigrationSchemaSerializer` without loss.

### c) Tests (TDD)

**Test class:** `WrapGod.Tests.MigrationSchemaGeneratorTests : TinyBddXunitBase`

| Test method | Fixture | Asserts |
|---|---|---|
| `FromDiff_SingleRenameType_ProducesRenameTypeRule` | one removed/added pair with identical short name | exact rule kind + confidence |
| `FromDiff_SingleRenameMember_ProducesRenameMemberRule` | one removed/added member, same declaring type | exact rule kind + confidence |
| `FromDiff_ReturnTypeChange_ProducesChangeTypeReferenceRule` | one `ChangedMemberEntry` with return-type delta | exact rule kind |
| `FromDiff_RuleIds_AreSequentialAndStable` | 5-rule fixture, run twice | IDs match across runs |
| `FromDiff_NamespaceRelocation_CollapsesToOneRule` | 3 type removes all from `OldNs.*`, 3 adds all in `NewNs.*` with matching short names | exactly 1 `RenameNamespaceRule` |
| `FromDiff_NullDiff_Throws` | null | `ArgumentNullException` |
| `FromDiff_EmptyLibrary_Throws` | non-null diff, empty library | `ArgumentException` |
| `FromDiff_SingleVersion_Throws` | diff with 1 version | `ArgumentException` |
| `FromDiff_UnmatchedRemove_ProducesManualRule` | 1 remove, no candidates | `RemoveMemberRule` with `Confidence.Manual` and non-null `Note` |
| `FromDiff_ArityGrew_ProducesAddRequiredParameter` | param count went 2→3 | `AddRequiredParameterRule` with `Confidence.Manual` |
| `FromDiff_AmbiguousRename_DeterministicTiebreak` | two removes vs one add | only one rename emitted, by lexicographic stableId order |
| `FromDiff_RenameDetectionDisabled_SuppressesRenames` | obvious rename + `DisableRenameDetection = true` | zero `RenameTypeRule` |
| `FromDiff_CustomPrefix_UsesCustomPrefix` | prefix `"X"` | rules start with `X-` |
| `FromDiff_NoChanges_ReturnsEmptyRules` | empty diff | `schema.Rules.Count == 0`, metadata still populated |
| `FromDiff_RealNuGetVersions_RoundTripsThroughSerializer` | real `MultiVersionExtractor` over MudBlazor 6.0.0 vs 7.0.0 | schema serializes + deserializes equal |

**Fixtures needed:**
- `DiffFixtures.cs` (new file) with named builders: `BuildRenameTypeDiff()`, `BuildRenameMemberDiff()`, `BuildArityGrewDiff()`, `BuildNamespaceShuffleDiff()`, `BuildEmptyDiff()`, `BuildAmbiguousRenameDiff()`. Each returns a fully-populated `VersionDiff`.
- One integration test references a cached `wrapgod-fixtures/mudblazor-6.0.0.dll` and `…-7.0.0.dll` under `WrapGod.Tests/fixtures/migration/`.

**Coverage target:** 95% on the new `WrapGod.Migration.Generation` namespace. Similarity edge cases are the easiest to under-cover — exercise both ends of every threshold.

### d) Files to create/modify

**Create:**
- `WrapGod.Migration/Generation/MigrationSchemaGenerator.cs` — public static class with `FromDiff`.
- `WrapGod.Migration/Generation/MigrationSchemaGeneratorOptions.cs` — options record.
- `WrapGod.Migration/Generation/Similarity.cs` — internal `JaroWinkler` helper.
- `WrapGod.Migration/Generation/RuleIdAllocator.cs` — internal deterministic sequence allocator.
- `WrapGod.Tests/MigrationSchemaGeneratorTests.cs` — TinyBDD test class.
- `WrapGod.Tests/Fixtures/Migration/DiffFixtures.cs` — fixture builders.
- `WrapGod.Tests/Fixtures/Migration/mudblazor-6.0.0.dll` and `mudblazor-7.0.0.dll` (or a stub assembly we build inline). Prefer building stub assemblies in-test via `Roslyn.CompileToAssembly` to avoid binary fixtures in git.

**Modify:**
- `WrapGod.Migration/WrapGod.Migration.csproj` — **add** `<ProjectReference Include="..\WrapGod.Extractor\WrapGod.Extractor.csproj" />`. (Currently only Manifest is referenced; the generator needs `VersionDiff`.)
- `WrapGod.slnx` — no change (project already in solution).

### e) Docs deliverable

- `docs/migration/schema-generation.md` (NEW) — explains the diff → schema mapping table, similarity thresholds, deterministic ID allocation, and the `DisableRenameDetection` knob.
- `docs/migration/index.md` (MODIFY) — link to the new schema-generation page.

Sample code block the docs must include:

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

### f) Samples deliverable

- None required at this issue level. `#203` owns the end-to-end sample. We do extend the migration fixtures referenced by `#203`.

### g) Done when

- All 15 tests green locally and in CI.
- Coverage on `WrapGod.Migration.Generation` ≥ 90%.
- `docs/migration/schema-generation.md` rendered, linked from `docs/migration/index.md`.
- `WrapGod.Migration.csproj` references `WrapGod.Extractor`.
- `gh issue close 193 --reason completed --comment "Closed by PR #<n>. Generator + docs + tests landed; covered by MigrationSchemaGeneratorTests."`

---

## Issue #194 — Migration Engine: `WrapGod.Migration.Engine` scaffold + `IRuleRewriter` interface

### a) Specification (SDD)

**Introduces:** A new project `WrapGod.Migration.Engine` (`net10.0`) that owns the Roslyn-dependent rewrite pipeline. This issue ships only the **contracts** — no concrete rewriters, no orchestrator. Future issues (`#195`, `#196`, `#202`) fill it in.

**Public API:**

```csharp
namespace WrapGod.Migration.Engine;

public interface IRuleRewriter
{
    /// <summary>The MigrationRuleKind name (camelCase, matches schema discriminator) this rewriter handles.</summary>
    string Kind { get; }

    /// <summary>
    /// Returns a rewritten node if the rule applies, or null to leave the node unchanged.
    /// Implementations must preserve trivia via WithTriviaFrom on every replaced node.
    /// </summary>
    SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx);
}

public sealed class RewriteContext
{
    public string FilePath { get; }
    public IReadOnlyList<AppliedRewrite> Applied { get; }
    public IReadOnlyList<SkippedRewrite> Skipped { get; }

    public RewriteContext(string filePath);

    public void RecordApplied(MigrationRule rule, TextSpan original, string originalText, string replacementText, int line);
    public void RecordSkipped(MigrationRule rule, TextSpan location, int line, string reason);
}

public sealed record AppliedRewrite(string RuleId, string File, int Line, string OriginalText, string ReplacedWith);
public sealed record SkippedRewrite(string RuleId, string File, int Line, string Reason);
public sealed record ManualRewrite(string RuleId, string Note, IReadOnlyList<string> MatchedFiles);

public sealed class MigrationResult
{
    public IReadOnlyList<AppliedRewrite> Applied { get; }
    public IReadOnlyList<SkippedRewrite> Skipped { get; }
    public IReadOnlyList<ManualRewrite> Manual { get; }
    public IReadOnlyDictionary<string, string> RewrittenFiles { get; } // path → new text
    public bool DryRun { get; }

    public MigrationResult(
        IReadOnlyList<AppliedRewrite> applied,
        IReadOnlyList<SkippedRewrite> skipped,
        IReadOnlyList<ManualRewrite> manual,
        IReadOnlyDictionary<string, string> rewrittenFiles,
        bool dryRun);
}
```

Additional helper:

```csharp
namespace WrapGod.Migration.Engine;

public static class TriviaPreservation
{
    /// <summary>Convenience: replace node's identifier text while preserving leading/trailing trivia.</summary>
    public static T WithReplacedToken<T>(this T node, SyntaxToken oldToken, SyntaxToken newToken) where T : SyntaxNode;
}
```

**Acceptance criteria:**
- [ ] New `WrapGod.Migration.Engine` project targets `net10.0` and is in `WrapGod.slnx`.
- [ ] References `WrapGod.Migration` and `Microsoft.CodeAnalysis.CSharp` (version pinned to match repo convention — see §4).
- [ ] `IRuleRewriter` interface compiles and is XML-documented.
- [ ] `RewriteContext` tracks both applied and skipped collections; both are `IReadOnlyList` externally but `List<T>` internally.
- [ ] `MigrationResult` aggregates results immutably.
- [ ] Scaffold tests verify each contract type's invariants (constructor null guards, record equality, immutability).

### b) Behavior (BDD)

Group: **happy**
1. *Given* a fresh `RewriteContext("foo.cs")`, *when* I call `RecordApplied(rule, span, "old", "new", 12)`, *then* `Applied` has one entry with `RuleId = rule.Id`, `File = "foo.cs"`, `Line = 12`, `OriginalText = "old"`, `ReplacedWith = "new"`.
2. *Given* a `RewriteContext`, *when* I call `RecordSkipped` three times, *then* `Skipped.Count == 3` in insertion order.
3. *Given* a `MigrationResult` constructed with empty lists, *when* I read its properties, *then* all are empty and `DryRun == false` unless explicitly true.
4. *Given* two `AppliedRewrite` records with the same field values, *when* compared, *then* they are equal (record semantics).

Group: **sad**
5. *Given* a null `filePath` to `RewriteContext`, *when* constructed, *then* it throws `ArgumentNullException`.
6. *Given* a null `rule` to `RecordApplied`, *when* called, *then* it throws `ArgumentNullException`.
7. *Given* a null `reason` to `RecordSkipped`, *when* called, *then* it throws `ArgumentNullException`.
8. *Given* a `MigrationResult` constructor passed a null `applied` list, *when* called, *then* it throws `ArgumentNullException`.

Group: **edge**
9. *Given* `WithReplacedToken` on a node whose token has trailing whitespace and a comment, *when* replaced, *then* the new token has identical leading and trailing trivia.
10. *Given* an `IRuleRewriter` implementation returning `null`, *when* used in a Roslyn `CSharpSyntaxRewriter.Visit` integration smoke test, *then* the input tree is returned unchanged (reference-equal at the top level).
11. *Given* a `RewriteContext`'s `Applied` collection, *when* I cast to `List<AppliedRewrite>`, *then* it throws `InvalidCastException` (collections are truly read-only externally).

### c) Tests (TDD)

**Test class:** `WrapGod.Tests.MigrationEngineScaffoldTests : TinyBddXunitBase` in `WrapGod.Tests/MigrationEngineScaffoldTests.cs`.

| Test method | Asserts |
|---|---|
| `RewriteContext_RecordApplied_AppendsEntry` | one applied record after one call |
| `RewriteContext_RecordSkipped_AppendsEntries` | three skipped records after three calls |
| `MigrationResult_Defaults_AreEmpty` | empty lists, dryRun false |
| `AppliedRewrite_RecordEquality_Holds` | record equality |
| `SkippedRewrite_RecordEquality_Holds` | record equality |
| `ManualRewrite_RecordEquality_Holds` | record equality |
| `RewriteContext_NullFilePath_Throws` | ANE |
| `RewriteContext_NullRule_Throws` | ANE |
| `RewriteContext_NullReason_Throws` | ANE |
| `MigrationResult_NullList_Throws` | ANE |
| `WithReplacedToken_PreservesTrivia` | trivia identity preserved |
| `IRuleRewriter_NoOp_LeavesTreeUnchanged` | reference equality on input tree |
| `RewriteContext_ExternalCast_Fails` | applied collection is not mutable from caller |

**Fixtures:** A tiny `NoOpRewriter : IRuleRewriter` (private to the test) returning null for everything. Synthetic syntax trees via `CSharpSyntaxTree.ParseText("class C { int X; }")`.

**Coverage target:** 95% — the surface is small and contract-only.

### d) Files to create/modify

**Create:**
- `WrapGod.Migration.Engine/WrapGod.Migration.Engine.csproj` — `net10.0` SDK project, references `WrapGod.Migration` + `Microsoft.CodeAnalysis.CSharp` (4.x — match `WrapGod.Generator`/`WrapGod.Analyzers` versions; verify by reading their csprojs).
- `WrapGod.Migration.Engine/IRuleRewriter.cs`
- `WrapGod.Migration.Engine/RewriteContext.cs`
- `WrapGod.Migration.Engine/AppliedRewrite.cs`
- `WrapGod.Migration.Engine/SkippedRewrite.cs`
- `WrapGod.Migration.Engine/ManualRewrite.cs`
- `WrapGod.Migration.Engine/MigrationResult.cs`
- `WrapGod.Migration.Engine/TriviaPreservation.cs`
- `WrapGod.Tests/MigrationEngineScaffoldTests.cs`

**Modify:**
- `WrapGod.slnx` — add `<Project Path="WrapGod.Migration.Engine/WrapGod.Migration.Engine.csproj" />`.
- `WrapGod.Tests/WrapGod.Tests.csproj` — add `<ProjectReference Include="..\WrapGod.Migration.Engine\WrapGod.Migration.Engine.csproj" />`.

### e) Docs deliverable

- `docs/migration/engine.md` (NEW) — stub page with architecture diagram, the `IRuleRewriter` interface, and a "rewriters ship in #195/#202" note. Cross-link from `docs/migration/index.md`.

### f) Samples deliverable

- None.

### g) Done when

- New project builds clean in Release.
- All 13 scaffold tests green.
- Coverage gate passes for the new project.
- `docs/migration/engine.md` published.
- `gh issue close 194 --reason completed --comment "Closed by PR #<n>. Scaffold contracts landed; rewriters arrive in #195/#202; orchestrator in #196."`

---

## Issue #195 — Migration Engine: A-level syntax rewriters

### a) Specification (SDD)

**Introduces:** Seven concrete `IRuleRewriter` implementations covering all `MigrationRuleKind` values listed as A-level in the design doc:

| Rewriter class | Handles `MigrationRuleKind` | Roslyn nodes visited |
|---|---|---|
| `RenameTypeRewriter` | `RenameType` | `IdentifierNameSyntax`, `QualifiedNameSyntax`, `UsingDirectiveSyntax` |
| `RenameNamespaceRewriter` | `RenameNamespace` | `UsingDirectiveSyntax`, `QualifiedNameSyntax`, namespace declarations |
| `RenameMemberRewriter` | `RenameMember` | `MemberAccessExpressionSyntax`, `IdentifierNameSyntax` (when inside a member access) |
| `ChangeParameterRewriter` | `ChangeParameter` | `ArgumentListSyntax` on matching `InvocationExpressionSyntax` |
| `RemoveMemberRewriter` | `RemoveMember` | `InvocationExpressionSyntax`, `MemberAccessExpressionSyntax` |
| `AddRequiredParameterRewriter` | `AddRequiredParameter` | `ArgumentListSyntax` on matching `InvocationExpressionSyntax` |
| `ChangeTypeReferenceRewriter` | `ChangeTypeReference` | `TypeSyntax` in declarations, casts, `typeof()`, generic type arguments |

**Per-rewriter behavior:**

- **`RenameTypeRewriter`** — When the identifier text matches the *short name* of `RenameTypeRule.OldName`, replace it with the *short name* of `NewName`. Update `using` directives that import the old fully-qualified name. Add a `using` for the new namespace if the new type is in a different namespace and not already imported.
- **`RenameNamespaceRewriter`** — Walk `UsingDirectiveSyntax`. If a using starts with `OldNamespace`, rewrite to `NewNamespace + suffix`. Walk `QualifiedNameSyntax` similarly. Do not affect identifiers that happen to share the namespace's short name.
- **`RenameMemberRewriter`** — At each `MemberAccessExpressionSyntax`, if (a) the member name equals `OldMemberName` AND (b) syntax-only heuristics say the receiver is likely of `TypeName` (matching short type name in a `using` or in a declared variable type within scope — best-effort, syntax-only), rewrite the member name. If the type-of-receiver is uncertain, record a `SkippedRewrite` and leave the node alone.
- **`ChangeParameterRewriter`** — On matching `InvocationExpressionSyntax`, transform the `ArgumentListSyntax` per the rule's `OldParameterName`/`NewParameterName`/`OldParameterType`/`NewParameterType` fields. If the parameter name changed only, rewrite the named-argument label. If the parameter type changed, this is **ambiguous in syntax-only mode** — record `SkippedRewrite` with a note.
- **`RemoveMemberRewriter`** — Comment out the entire call expression with a `// MIGRATION: <ruleId> removed — see <Note> //` prefix, preserve surrounding trivia, and record `Applied`. Never delete code silently. The comment retains the original text inside the comment so users can re-introduce manually.
- **`AddRequiredParameterRewriter`** — Append `default` (or `null` for reference types based on `ParameterType`) at `Position` in the argument list, wrapped with `/* TODO MIGRATION: <ruleId> required arg added */`. Record as `Applied` but the rule emitting these usually has `Confidence.Manual` in practice — the orchestrator (#196) will filter that out before invoking. This rewriter is correctness-first.
- **`ChangeTypeReferenceRewriter`** — Replace exact-match `TypeSyntax` nodes whose text equals `OldType` (short or fully-qualified) with `NewType`. Walks generic arguments. Update `using` if `NewType`'s namespace isn't imported.

**Universal contract:**

- Every rewriter MUST preserve trivia. Use `WithTriviaFrom`, never raw token replacement without it.
- Every rewriter MUST be safe under `null` rule — return `null` immediately if `rule.Kind != Kind`.
- Every ambiguous match MUST record a `SkippedRewrite` with a descriptive reason. **Never apply an uncertain rewrite.**
- No rewriter performs semantic lookup — syntax only. The whole engine works on broken code.

**Acceptance criteria:**
- [ ] All 7 rewriters implemented with trivia preservation.
- [ ] Dedicated unit tests using synthetic syntax trees per rewriter.
- [ ] Ambiguous cases produce `SkippedRewrite` entries (not exceptions, not bad rewrites).
- [ ] Multi-rule integration test on synthetic project.
- [ ] ≥ 90% test coverage.

### b) Behavior (BDD)

Per-rewriter scenarios — abbreviated to representative coverage. The implementer should expand each to 6–10 scenarios.

**`RenameTypeRewriter` — happy / sad / edge**
1. *Given* a rule renaming `Foo.Bar` → `Foo.Baz` and source `var x = new Bar();`, *when* rewritten, *then* output contains `var x = new Baz();`.
2. *Given* a rule renaming `Foo.Bar` → `Qux.Baz` and source with `using Foo;`, *when* rewritten, *then* the using is updated to `using Qux;` and `Bar` becomes `Baz`.
3. *Given* an identifier `Bar` that does not refer to the renamed type (it's a local variable), *when* rewritten, *then* a `SkippedRewrite` is recorded with reason "ambiguous: identifier is not a type reference".
4. *Given* a fully-qualified usage `Foo.Bar`, *when* rewritten, *then* it becomes `Foo.Baz` (or `Qux.Baz` if namespace changed).
5. *Given* source with two types of the same short name from different namespaces, *when* rewritten, *then* only the matching one is changed and the other is skipped with reason.

**`RenameNamespaceRewriter` — happy**
6. *Given* `using OldNs.Sub;` and rule `OldNs` → `NewNs`, *when* rewritten, *then* the using becomes `using NewNs.Sub;`.

**`RenameMemberRewriter` — happy / sad**
7. *Given* `var b = new MudButton(); b.Color = ...;` and rule renaming `Color` → `ButtonColor`, *when* rewritten, *then* `b.Color` becomes `b.ButtonColor`.
8. *Given* `obj.Color = ...;` where `obj`'s type cannot be inferred syntactically, *when* rewritten, *then* a `SkippedRewrite` is recorded.

**`ChangeParameterRewriter` — edge**
9. *Given* a call with named argument `Foo(size: 12)` and rule renaming parameter `size` → `buttonSize`, *when* rewritten, *then* the argument becomes `Foo(buttonSize: 12)`.
10. *Given* a parameter type change (e.g., `int` → `MudBlazor.Size`) with positional argument `Foo(12)`, *when* rewritten, *then* a `SkippedRewrite` is recorded with reason "type change requires semantic conversion".

**`RemoveMemberRewriter` — happy**
11. *Given* `obj.Deprecated();` and a `RemoveMember` rule for `Deprecated`, *when* rewritten, *then* the line becomes `// MIGRATION: MUD-005 removed: obj.Deprecated();` and an `Applied` entry is recorded.

**`AddRequiredParameterRewriter` — happy**
12. *Given* `provider.Apply()` and rule adding required `MudTheme theme` at position 0, *when* rewritten, *then* the call becomes `provider.Apply(default /* TODO MIGRATION: MUD-006 required arg added */)`.

**`ChangeTypeReferenceRewriter` — happy / edge**
13. *Given* `IList<string> items` and rule `IList<T>` → `IReadOnlyList<T>`, *when* rewritten, *then* the declaration becomes `IReadOnlyList<string> items`.
14. *Given* `typeof(IList<int>)` and the same rule, *when* rewritten, *then* it becomes `typeof(IReadOnlyList<int>)`.
15. *Given* a generic constraint `where T : IList<int>`, *when* rewritten, *then* the constraint is updated.

**Universal — across all rewriters**
16. *Given* a rule of a kind not handled by this rewriter, *when* `TryRewrite` is called, *then* it returns `null` immediately without inspecting the node.
17. *Given* any successful rewrite, *when* the result is inspected, *then* leading and trailing trivia of the replaced token are byte-identical to the original.

### c) Tests (TDD)

**Test classes (one per rewriter):**

- `WrapGod.Tests.RenameTypeRewriterTests`
- `WrapGod.Tests.RenameNamespaceRewriterTests`
- `WrapGod.Tests.RenameMemberRewriterTests`
- `WrapGod.Tests.ChangeParameterRewriterTests`
- `WrapGod.Tests.RemoveMemberRewriterTests`
- `WrapGod.Tests.AddRequiredParameterRewriterTests`
- `WrapGod.Tests.ChangeTypeReferenceRewriterTests`
- `WrapGod.Tests.RewriterIntegrationTests` (multi-rule on a 50-line synthetic source)

Each per-rewriter class follows the pattern:
- `Rewrite_HappyPath_ReplacesAndRecordsApplied`
- `Rewrite_WrongRuleKind_ReturnsNull`
- `Rewrite_AmbiguousMatch_RecordsSkipped`
- `Rewrite_PreservesTrivia`
- `Rewrite_NoMatch_LeavesNodeUnchanged`
- 3–5 rewriter-specific edge cases drawn from §b.

**Integration test:** `RewriterIntegrationTests.MultipleRulesOnOneFile_AllRewritersApply` — feeds one synthetic file through all seven rewriters and asserts (a) total `Applied.Count`, (b) total `Skipped.Count`, (c) final source equals a hand-authored expected fixture.

**Fixtures:**
- `WrapGod.Tests/Fixtures/Migration/SyntheticBefore.cs.txt` — input
- `WrapGod.Tests/Fixtures/Migration/SyntheticAfter.cs.txt` — expected output
- `WrapGod.Tests/Fixtures/Migration/SyntheticRules.json` — schema applied

**Coverage:** 92% minimum on `WrapGod.Migration.Engine.Rewriters.*`. Trivia paths are easy to miss — explicit trivia assertions in every test.

### d) Files to create/modify

**Create:**
- `WrapGod.Migration.Engine/Rewriters/RenameTypeRewriter.cs`
- `WrapGod.Migration.Engine/Rewriters/RenameNamespaceRewriter.cs`
- `WrapGod.Migration.Engine/Rewriters/RenameMemberRewriter.cs`
- `WrapGod.Migration.Engine/Rewriters/ChangeParameterRewriter.cs`
- `WrapGod.Migration.Engine/Rewriters/RemoveMemberRewriter.cs`
- `WrapGod.Migration.Engine/Rewriters/AddRequiredParameterRewriter.cs`
- `WrapGod.Migration.Engine/Rewriters/ChangeTypeReferenceRewriter.cs`
- `WrapGod.Migration.Engine/Rewriters/RewriterHelpers.cs` — shared utilities (e.g., `MatchesShortName`, `IsLikelyTypeReference`, `UpdateOrAddUsing`).
- 7× `*RewriterTests.cs` in `WrapGod.Tests/`.
- `WrapGod.Tests/RewriterIntegrationTests.cs`.
- The three `Synthetic*` fixtures under `WrapGod.Tests/Fixtures/Migration/`.

**Modify:** none beyond the above.

### e) Docs deliverable

- `docs/migration/engine.md` (MODIFY) — fill out the "Rewriters shipping in #195" section with the table from §a and one example per rewriter.

### f) Samples deliverable

- None at this layer (synthetic fixtures live in tests).

### g) Done when

- All ~60 tests across the 7 rewriters + integration green.
- Coverage on `WrapGod.Migration.Engine.Rewriters` ≥ 90%.
- Integration test reproduces the hand-authored "after" fixture byte-for-byte (except line endings normalized).
- Docs page updated.
- `gh issue close 195 --reason completed --comment "Closed by PR #<n>. All 7 A-level rewriters land with synthetic + integration tests."`

---

## Issue #196 — Migration Engine: `MigrationEngine` orchestrator (Apply + DryRun)

### a) Specification (SDD)

**Introduces:** The top-level orchestration class that consumes a `MigrationSchema` plus a set of file paths, dispatches to the registered `IRuleRewriter`s, and produces a `MigrationResult`.

**Public API:**

```csharp
namespace WrapGod.Migration.Engine;

public sealed class MigrationEngine
{
    public MigrationEngine(IEnumerable<IRuleRewriter> rewriters);

    /// <summary>Convenience: creates an engine with all built-in A-level and B-level rewriters.</summary>
    public static MigrationEngine CreateDefault();

    public MigrationResult Apply(MigrationSchema schema, IEnumerable<string> files);
    public MigrationResult DryRun(MigrationSchema schema, IEnumerable<string> files);
}
```

Internal pipeline (single tree walk per file):

1. For each file path:
    a. Read file (`File.ReadAllText`). On `IOException`, record a `SkippedRewrite` synthetic entry (`ruleId = "<io>"`, `reason = ex.Message`) and continue.
    b. Parse via `CSharpSyntaxTree.ParseText(text, path: filePath)`.
    c. Walk rules in schema order. For each rule:
        - If `rule.Confidence == Manual`, append to `manual` list with `MatchedFiles` (collected lazily; see step c.iv).
        - Else, find `IRuleRewriter` where `rewriter.Kind == rule.Kind.ToCamelCase()`. If none, record `SkippedRewrite` with reason `"no rewriter for kind '<x>'"` against the *file* (line 0).
        - Else, walk the syntax tree with a `CSharpSyntaxRewriter` subclass that forwards each node to `rewriter.TryRewrite(node, rule, ctx)`. Replace returned non-null nodes.
        - For manual rules, run a syntax-only "would this rule match anything here?" scan and append the file path to the matching `ManualRewrite.MatchedFiles`.
    d. After all rules, write the modified tree text (when not dry-run). Use atomic write: write to `path + ".tmp"`, then `File.Move(tmp, path, overwrite: true)`.
2. Aggregate all `RewriteContext.Applied`, `Skipped`, and `manual` entries across files into `MigrationResult`.

**Ordering rule:** First-wins. Rules execute in schema order; if rule A rewrites a node, subsequent rules in the same file see the rewritten tree.

**Performance target:** 1000-file project (~250 KB average) in under 5 seconds on a developer laptop. Achieved via:
- Single tree walk per (file, rule) pair (acceptable since rules are usually < 50).
- No semantic compilation. No workspace.
- Pooled `StringBuilder` for read/write.

**File I/O abstraction:** Inject `IMigrationFileSystem` (defaults to `RealFileSystem`) so tests can swap an in-memory FS. Keep this internal — the public API still takes paths.

**Acceptance criteria:**
- [ ] `Apply()` rewrites files and returns `MigrationResult` with full audit trail.
- [ ] `DryRun()` returns the same `MigrationResult` (`DryRun = true`, `RewrittenFiles` populated) but writes nothing.
- [ ] Manual rules are listed in `result.Manual` with matched files; never applied.
- [ ] Multiple rules on the same file/node process correctly under first-wins.
- [ ] Integration tests with multi-file projects pass.
- [ ] 1000-file synthetic project runs in < 5 s (a benchmark test in CI flagged informational, not gating).

### b) Behavior (BDD)

Group: **happy**
1. *Given* a schema with one `RenameTypeRule` and one source file that contains a match, *when* `Apply` runs, *then* the file is rewritten on disk and `result.Applied.Count == 1`.
2. *Given* the same schema and file, *when* `DryRun` runs, *then* the file on disk is unchanged but `result.RewrittenFiles[path]` contains the rewritten text and `result.DryRun == true`.
3. *Given* a schema with rules A and B where A renames `X→Y` and B renames `Y→Z`, *when* `Apply` runs against a file with `X`, *then* the file ends as `Z` (first-wins chained).
4. *Given* a schema with one `Manual`-confidence rule, *when* `Apply` runs, *then* the rule appears in `result.Manual` with `MatchedFiles` populated and no `Applied` entries for it.
5. *Given* a 50-file project where 10 files contain matches, *when* `Apply` runs, *then* `result.RewrittenFiles.Count == 10`.

Group: **sad**
6. *Given* a schema with a rule whose `Kind` has no registered rewriter, *when* `Apply` runs, *then* one `SkippedRewrite` per affected file is recorded with reason starting `"no rewriter for kind"`.
7. *Given* a file that cannot be read (IO error simulated), *when* `Apply` runs, *then* a synthetic `SkippedRewrite` is recorded and processing continues to the next file.
8. *Given* a file with a Roslyn parse error (unclosed brace), *when* `Apply` runs, *then* the engine still attempts rewrites against the partial tree (Roslyn returns a tree even on errors) — assert `result.Applied` is populated where rewriters matched valid sub-trees, no exception bubbles.
9. *Given* `Apply` is called with `null` schema or `null` files, *when* invoked, *then* `ArgumentNullException` is thrown.
10. *Given* `MigrationEngine` constructed with `null` rewriters, *when* called, *then* `ArgumentNullException`.

Group: **edge**
11. *Given* a schema with two rewriters claiming the same `Kind`, *when* `MigrationEngine` is constructed, *then* the first one registered wins; subsequent duplicates are ignored (and a one-time debug-log line emitted — not asserted).
12. *Given* an `Apply` run that modifies a file, *when* the run is repeated immediately, *then* the second run produces zero new `Applied` entries (rules no longer match the rewritten code). (Idempotence at the rewrite layer; state-file idempotence is `#197`.)
13. *Given* 1000 trivial files, *when* `Apply` runs, *then* the run completes in under 5 seconds in Release config (perf test, soft assertion).
14. *Given* a file with `\r\n` line endings on Windows, *when* `Apply` writes back, *then* line endings are preserved.

### c) Tests (TDD)

**Test class:** `WrapGod.Tests.MigrationEngineTests` + `WrapGod.Tests.MigrationEnginePerfTests` (perf is `[Trait("Category","Perf")]` and excluded from coverage gate).

| Test method | Asserts |
|---|---|
| `Apply_OneRule_RewritesFileAndRecordsApplied` | file changed on disk, result populated |
| `DryRun_OneRule_PopulatesResultWithoutWriting` | disk unchanged, result populated |
| `Apply_TwoChainedRenames_FirstWinsThenSecond` | end-state correct |
| `Apply_ManualRule_RecordedInManual` | applied empty, manual populated |
| `Apply_MultipleFiles_RewritesAllMatches` | exact file count |
| `Apply_UnknownRuleKind_RecordsSkipped` | skipped reason |
| `Apply_IoError_RecordsSkippedAndContinues` | virtual FS injection throws on one file |
| `Apply_ParseError_DoesNotThrow` | partial tree still walks |
| `Apply_NullSchema_Throws` | ANE |
| `Apply_NullFiles_Throws` | ANE |
| `Engine_DuplicateKind_FirstWins` | only one wins |
| `Apply_TwiceInARow_SecondIsNoOp` | second run has zero applied |
| `Apply_PreservesLineEndings` | CRLF preserved |
| `Apply_1000Files_UnderFiveSeconds` | `[Trait("Category","Perf")]` |

**Fixtures:** `InMemoryFileSystem` test helper, schemas as inline JSON strings deserialized via `MigrationSchemaSerializer`.

**Coverage:** 92% on `WrapGod.Migration.Engine` orchestrator types (exclude perf test class).

### d) Files to create/modify

**Create:**
- `WrapGod.Migration.Engine/MigrationEngine.cs`
- `WrapGod.Migration.Engine/IMigrationFileSystem.cs` (internal)
- `WrapGod.Migration.Engine/RealFileSystem.cs` (internal)
- `WrapGod.Migration.Engine/InternalRewriterDispatcher.cs` (`CSharpSyntaxRewriter` subclass)
- `WrapGod.Tests/MigrationEngineTests.cs`
- `WrapGod.Tests/MigrationEnginePerfTests.cs`
- `WrapGod.Tests/Fixtures/Migration/InMemoryFileSystem.cs`

**Modify:**
- `WrapGod.Migration.Engine/WrapGod.Migration.Engine.csproj` — no changes; already pulls Roslyn.

### e) Docs deliverable

- `docs/migration/engine.md` (MODIFY) — add the orchestrator pipeline diagram, the `MigrationEngine.CreateDefault()` convenience, and the performance budget.

### f) Samples deliverable

- None at this layer.

### g) Done when

- All ~14 tests green (perf test passing locally even if untracked in CI gate).
- Coverage gate passes.
- Docs section live.
- `gh issue close 196 --reason completed --comment "Closed by PR #<n>. Orchestrator with Apply + DryRun lands; perf budget met."`

---

## Issue #197 — Migration Engine: State tracking (state file, idempotent re-runs, schema hash)

### a) Specification (SDD)

**Introduces:** Persistent migration state and the logic that consumes it to make `Apply` idempotent.

**State file location convention (LOCKED):** Same directory as the schema file, with filename `{schemaFilename}.state.json`. For example, `mudblazor.6.0-to-7.0.wrapgod-migration.json` ⇒ `mudblazor.6.0-to-7.0.wrapgod-migration.json.state.json`. The state file is intended to be committed alongside the schema.

> Rationale: matches the design doc, makes the state visible/git-trackable, avoids hidden directories.

**Public API:**

```csharp
namespace WrapGod.Migration.Engine.State;

public sealed class MigrationState
{
    public string Schema { get; set; } = string.Empty;          // schema filename (relative or absolute)
    public string SchemaHash { get; set; } = string.Empty;      // "sha256:<hex>"
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset LastRunAt { get; set; }
    public MigrationStateSummary Summary { get; set; } = new();
    public List<AppliedRewrite> Applied { get; set; } = new();
    public List<SkippedRewrite> Skipped { get; set; } = new();
    public List<ManualRewrite> Manual { get; set; } = new();
}

public sealed class MigrationStateSummary
{
    public int TotalRules { get; set; }
    public int Applied { get; set; }
    public int Skipped { get; set; }
    public int Manual { get; set; }
}

public static class MigrationStateSerializer
{
    public static string Serialize(MigrationState state);
    public static MigrationState? Deserialize(string json);
}

public static class MigrationStateStore
{
    public static string GetStatePath(string schemaPath);
    public static MigrationState? Load(string schemaPath);
    public static void Save(string schemaPath, MigrationState state);
    public static string ComputeSchemaHash(string schemaJson);   // returns "sha256:<lowerhex>"
}

public sealed class StatefulMigrationEngine
{
    public StatefulMigrationEngine(MigrationEngine inner);

    /// <summary>Applies skipping rules already in state; updates state on disk after the run.</summary>
    public MigrationResult ApplyWithState(string schemaPath, MigrationSchema schema, IEnumerable<string> files);

    /// <summary>Dry-run equivalent that uses state to filter but writes neither files nor state.</summary>
    public MigrationResult DryRunWithState(string schemaPath, MigrationSchema schema, IEnumerable<string> files);
}
```

**Idempotence logic:**
- Before running, load state. Compute `currentHash = ComputeSchemaHash(<schema-json>)`.
- If state exists AND `state.SchemaHash == currentHash`:
    - Build a `HashSet<string>` of "already-applied keys": `$"{a.RuleId}|{a.File}|{a.Line}"` for each `AppliedRewrite`. Pass this set into the inner engine so it can short-circuit per node (via a `RewriteContext.AlreadyApplied(key)` check that rewriters consult).
- If state exists AND hash differs:
    - Re-run all rules. Old `Applied` entries that match in the new run are de-duped (same key wins, no double entry). Entries that no longer match get dropped from the new state (with a one-line note logged to summary).
- If no state file: full run, fresh state.

After the run, write the merged state. The `Skipped` list is **replaced wholesale** each run (skipped reasons can change as rewriters improve). The `Applied` list is **append-only** with de-duplication by key. The `Manual` list is **replaced wholesale**.

**Schema hash format:** `sha256:` followed by lowercase hex of the SHA-256 of the UTF-8 schema JSON, **after** trimming trailing whitespace per line and normalising line endings to `\n`. This makes the hash insensitive to git's autocrlf behaviour.

**Acceptance criteria:**
- [ ] State file written after `ApplyWithState()`.
- [ ] State file read before subsequent runs; matched-key rules skipped.
- [ ] Schema hash change triggers re-evaluation; old entries de-duped.
- [ ] State JSON round-trips losslessly.
- [ ] Unit tests cover state transitions: empty → populated, populated → updated, hash-changed merge, missing state graceful.

### b) Behavior (BDD)

Group: **happy**
1. *Given* no prior state and a schema with one rule, *when* `ApplyWithState` runs, *then* the state file is created next to the schema with `Applied.Count == 1` and a populated `SchemaHash`.
2. *Given* an existing state file containing one `Applied` entry, *when* `ApplyWithState` runs again with the same schema, *then* the rule is skipped (no new applied entries) and the file is not re-modified.
3. *Given* an existing state file with one entry, *when* the user edits the schema (adds a new rule) and `ApplyWithState` runs, *then* the new rule applies and the old applied entry remains.
4. *Given* a state file, *when* `Status`-style consumption reads it, *then* `Summary.Applied`, `Summary.Skipped`, `Summary.Manual` match the list lengths exactly.

Group: **sad**
5. *Given* a corrupt state file (invalid JSON), *when* `ApplyWithState` runs, *then* the corrupt file is renamed to `{name}.state.json.bak` and a fresh run executes. A `SkippedRewrite` with `ruleId="<state>"` documents the recovery.
6. *Given* read-only filesystem on state file write, *when* `ApplyWithState` finishes the run, *then* an `IOException` propagates after the file rewrites have already happened — implementer must document this; option: write state first to `.tmp` and `File.Move` atomically.
7. *Given* `ApplyWithState` called with `null` schemaPath, *when* invoked, *then* `ArgumentNullException`.

Group: **edge**
8. *Given* a schema modified only by reordering rules (same content), *when* `ApplyWithState` runs, *then* the schema hash is unchanged (hash is on canonical-serialised content, not source bytes — implementer choice: either canonicalise via `MigrationSchemaSerializer.Serialize(schema)` before hashing, or accept that re-ordering changes the hash). **Lock decision: hash the file bytes after line-ending normalisation. Reordering DOES change the hash. Document this clearly.**
9. *Given* the schema is renamed on disk, *when* `ApplyWithState` runs against the new path, *then* a new state file is created next to the new schema (state is path-bound).
10. *Given* a state file from a different schema (Schema field mismatch), *when* `ApplyWithState` runs, *then* the run treats the state as orphaned, archives to `.bak`, starts fresh.
11. *Given* a state file with 10000 `Applied` entries, *when* deserialized, *then* it completes in under 500ms (perf check).

### c) Tests (TDD)

**Test class:** `WrapGod.Tests.MigrationStateTests` (round-trip and serializer) + `WrapGod.Tests.StatefulMigrationEngineTests` (integration).

| Test method | Asserts |
|---|---|
| `Serializer_RoundTrips_FullState` | full state with all 3 lists populated |
| `Serializer_EmptyState_RoundTrips` | minimal state |
| `Serializer_NullJson_ReturnsNull` | mirrors schema serializer |
| `Hash_LineEndingNormalised` | same content with `\r\n` and `\n` produces same hash |
| `Hash_WhitespaceSensitive` | trailing whitespace per line trimmed; intra-line whitespace preserved |
| `Apply_FirstRun_WritesState` | state file appears |
| `Apply_SecondRunSameSchema_NoOp` | applied count stable |
| `Apply_SchemaEdited_AppliesNewRulesOnly` | only new rule lands |
| `Apply_CorruptState_RecoversWithBackup` | `.bak` file exists after run |
| `Apply_StateForWrongSchema_Archives` | orphan handling |
| `Apply_AtomicStateWrite_NoPartialFileOnInterrupt` | mock IO mid-write — state stays valid |
| `GetStatePath_ReturnsSiblingPath` | path math |
| `Apply_ManualRule_AppearsInState` | state.Manual populated |
| `Apply_SkippedRule_AppearsInState` | state.Skipped populated; replaces previous skipped list |

**Coverage:** 92% on `WrapGod.Migration.Engine.State`.

### d) Files to create/modify

**Create:**
- `WrapGod.Migration.Engine/State/MigrationState.cs`
- `WrapGod.Migration.Engine/State/MigrationStateSummary.cs`
- `WrapGod.Migration.Engine/State/MigrationStateSerializer.cs`
- `WrapGod.Migration.Engine/State/MigrationStateStore.cs`
- `WrapGod.Migration.Engine/StatefulMigrationEngine.cs`
- `WrapGod.Tests/MigrationStateTests.cs`
- `WrapGod.Tests/StatefulMigrationEngineTests.cs`

**Modify:**
- `WrapGod.Migration.Engine/RewriteContext.cs` — add `AlreadyApplied(string key)` predicate (consumes a set provided by `StatefulMigrationEngine`).
- `WrapGod.Migration.Engine/MigrationEngine.cs` — add overload `Apply(MigrationSchema, IEnumerable<string>, ISet<string> alreadyApplied)`. Public surface stays the same (the new param defaults to empty set on the existing overload).

### e) Docs deliverable

- `docs/migration/state.md` (NEW) — state file format, hash semantics, recovery from corruption, git workflow guidance.
- Cross-link from `docs/migration/index.md`.

### f) Samples deliverable

- A representative state file checked in at `docs/migration/examples/sample.state.json` for documentation purposes.

### g) Done when

- All ~14 tests green.
- Coverage gate passes.
- State file format documented and example present.
- `gh issue close 197 --reason completed --comment "Closed by PR #<n>. State tracking + idempotent re-runs working."`

---

## Issue #198 — CLI: `migrate generate`

### a) Specification (SDD)

**Introduces:** A new sub-command under `migrate` in `WrapGod.Cli` that wraps `MultiVersionExtractor` + `MigrationSchemaGenerator` end-to-end.

**Command surface:**

```
wrap-god migrate generate
    --package <id>             (optional) NuGet package ID
    --from <version>           (required) source version
    --to <version>             (required) target version
    --from-assembly <path>     (optional) local DLL for source version
    --to-assembly <path>       (optional) local DLL for target version
    --output, -o <path>        (optional) defaults to {package}.{from}-to-{to}.wrapgod-migration.json
    --source <feed-url>        (optional) private NuGet feed
    --tfm <moniker>            (optional) target framework moniker (e.g. net8.0)
    --rule-id-prefix <prefix>  (optional) override generator's default prefix
    --no-rename-detection      (optional) flag — passes options through to generator
    --json                     (optional) emit summary as JSON
```

**Validation:**
- Either `--package` OR (`--from-assembly` AND `--to-assembly`) must be provided. Mutually exclusive within a run.
- `--from` and `--to` are always required.
- `--output` defaults; if file already exists, error with exit code `RuntimeFailure (1)` unless `--force` is supplied (`--force` not in issue body — proposing it as a small ergonomic add; if pushed back, drop it).

**Behavior:**
1. Resolve assemblies: if `--package`, call `NuGetPackageResolver` for both versions; if local, validate paths exist.
2. Build `VersionDiff` via `MultiVersionExtractor` (already exists).
3. Call `MigrationSchemaGenerator.FromDiff(diff, library, options)`.
4. Serialize via `MigrationSchemaSerializer.Serialize`.
5. Write to `--output`.
6. Print summary: total rules, breakdown by confidence (`Auto`/`Verified`/`Manual`), output path.

**Summary output (human, default):**

```
WrapGod migrate generate
------------------------
Library:  MudBlazor
From:     6.0.0
To:       7.0.0
Rules:    47 total
  auto:      31
  verified:  9
  manual:    7
Output:   ./mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json
```

**Summary output (`--json`):**

```json
{
  "library": "MudBlazor",
  "from": "6.0.0",
  "to": "7.0.0",
  "outputPath": "./mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json",
  "rules": {
    "total": 47,
    "byConfidence": { "auto": 31, "verified": 9, "manual": 7 }
  }
}
```

**Exit codes:** see §4 table. Success = 0; bad args = 2; runtime failure (file IO, network) = 1; nothing-to-do (zero rules) = 0 with a warning printed.

**Acceptance criteria:**
- [ ] NuGet-based generation works end-to-end against a real package.
- [ ] Local assembly-based generation works.
- [ ] Default output path follows naming convention.
- [ ] Summary output shows rule counts by confidence.
- [ ] Error handling: missing package, invalid version, network failure.
- [ ] In-process CLI test coverage.

### b) Behavior (BDD)

Group: **happy**
1. *Given* `--package MudBlazor --from 6.0.0 --to 7.0.0`, *when* the command runs, *then* exit code is 0 and an output file is written.
2. *Given* `--from-assembly old.dll --to-assembly new.dll --from 6.0.0 --to 7.0.0`, *when* run, *then* exit code is 0 and the file is written.
3. *Given* a successful run with `--json`, *when* the output is parsed as JSON, *then* it has `rules.total >= 0` and `rules.byConfidence` keys.
4. *Given* no `--output`, *when* run, *then* the file is written at `./{package-or-library}.{from}-to-{to}.wrapgod-migration.json`.

Group: **sad**
5. *Given* neither `--package` nor assembly paths, *when* run, *then* exit code is 2 with "either --package or assembly paths required".
6. *Given* both `--package` and `--from-assembly`, *when* run, *then* exit code is 2 with "mutually exclusive".
7. *Given* `--package NotARealPackageXyz`, *when* run, *then* exit code is 1 with a NuGet "not found" message.
8. *Given* an output file that already exists, *when* run, *then* exit code is 1 with "output exists; remove it or change --output".
9. *Given* `--from missing-version`, *when* run, *then* exit code is 1 with "version not resolvable".
10. *Given* network timeout (simulated by pointing `--source` at unreachable URL), *when* run, *then* exit code is 1 with the error printed.

Group: **edge**
11. *Given* a package with zero breaking changes between versions, *when* run, *then* exit code is 0, file is written with `Rules.Count == 0`, and stdout warns "no breaking changes detected".
12. *Given* `--help`, *when* run, *then* exit code is 0 and all flags are documented.

### c) Tests (TDD)

**Test class:** `WrapGod.Tests.MigrateGenerateCliTests` (in-process via `Command.InvokeAsync`).

| Test method | Asserts |
|---|---|
| `Generate_Help_ListsAllFlags` | stdout contains every flag |
| `Generate_NoArgs_FailsWithUsage` | exit 2 |
| `Generate_PackageAndAssemblyTogether_Fails` | exit 2 |
| `Generate_NeitherPackageNorAssembly_Fails` | exit 2 |
| `Generate_LocalAssemblies_WritesFile` | output present, valid JSON |
| `Generate_DefaultOutputPath_FollowsConvention` | filename matches `{lib}.{from}-to-{to}.wrapgod-migration.json` |
| `Generate_Json_OutputsValidJsonSummary` | summary JSON deserialises |
| `Generate_OutputAlreadyExists_Fails` | exit 1 |
| `Generate_UnresolvableVersion_Fails` | exit 1 |
| `Generate_ZeroBreakingChanges_SucceedsWithWarning` | exit 0, stderr/stdout contains warning |
| `Generate_RuleIdPrefix_PassesThrough` | rule IDs in file start with chosen prefix |
| `Generate_NoRenameDetection_DisablesRenames` | generator option propagated |

NuGet network tests live behind `[Trait("Category","Network")]` and are excluded from coverage gate.

**Coverage:** 88%+ on the new `MigrateGenerateCommand`. Use a local-assembly fixture (two tiny stubs compiled in-test) to avoid network in mainline tests.

### d) Files to create/modify

**Create:**
- `WrapGod.Cli/MigrateGenerateCommand.cs`
- `WrapGod.Tests/MigrateGenerateCliTests.cs`

**Modify:**
- `WrapGod.Cli/Program.cs` — refactor `migrate` to hold multiple subcommands. Currently `MigrateInitCommand.Create()` returns a `migrate` command containing only `init`. Move that construction into a new `WrapGod.Cli/MigrateCommandBuilder.cs` that aggregates `init`, `generate`, and (later) `apply`/`status`/`verify`.
- `WrapGod.Cli/MigrateInitCommand.cs` — extract its `Create()` to produce only the `init` subcommand, not the `migrate` parent.
- `WrapGod.Cli/WrapGod.Cli.csproj` — add `<ProjectReference Include="..\WrapGod.Migration\WrapGod.Migration.csproj" />` if not already present.
- `WrapGod.Tests/CliCommandTests.cs` — update `ExpectedRootCommands` discovery; the migrate subcommand now has multiple children. Add a check that `migrate generate` is wired.

### e) Docs deliverable

- `docs/guide/cli.md` (MODIFY) — add the new command with all flags.
- `docs/CLI.md` (MODIFY) — same.
- `README.md` (MODIFY) — add a row to the CLI table.

### f) Samples deliverable

- None at this layer.

### g) Done when

- All 12 tests green.
- Coverage gate passes.
- `wrap-god migrate generate --help` shows the new flags.
- Docs updated.
- `gh issue close 198 --reason completed --comment "Closed by PR #<n>. migrate generate live with NuGet + local assembly modes."`

---

## Issue #199 — CLI: `migrate apply --dry-run`

### a) Specification (SDD)

**Introduces:** `wrap-god migrate apply` — runs `StatefulMigrationEngine` against a project directory.

**Command surface:**

```
wrap-god migrate apply
    --schema, -s <path>         (required) path to migration schema JSON
    --project-dir, -p <path>    (optional) defaults to cwd
    --dry-run                   (optional) preview only, no writes
    --include <glob>            (optional, repeatable) default: **/*.cs
    --exclude <glob>            (optional, repeatable) defaults: **/obj/**, **/bin/**
    --json                      (optional) emit summary as JSON
```

**Behavior:**
1. Load + parse schema; bail with exit 1 if invalid.
2. Resolve files: walk `--project-dir` and apply include/exclude globs. Implementation: `Microsoft.Extensions.FileSystemGlobbing.Matcher` (already a transitive dep) OR build via `Directory.EnumerateFiles + glob match`. Choose `Matcher` to handle the repeated `--include`/`--exclude`.
3. Build `MigrationEngine.CreateDefault()`; wrap in `StatefulMigrationEngine`.
4. Call `DryRunWithState` or `ApplyWithState` depending on `--dry-run`.
5. Print summary; in `--dry-run`, additionally print a unified-diff preview per file (truncate at 20 lines per file, link to dump file for full diff).

**Summary output (human, default):**

```
WrapGod migrate apply [DRY-RUN]
-------------------------------
Schema:    mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json (47 rules)
Project:   ./src
Files:     128 scanned, 22 modified
Applied:   38 rewrites
Skipped:   6 (see details below)
Manual:    3 rules require human intervention

Skipped:
  MUD-017 src/Dialogs/Confirm.cs:42  Ambiguous: two overloads of Show() in scope
  ...

Manual:
  MUD-003 Parameters restructured -- requires manual mapping
    matched in: src/Dialogs/Confirm.cs, src/Dialogs/Edit.cs

(no files were modified)            ← only on --dry-run
```

**JSON summary (`--json`):** mirrors the human format as a JSON object.

**Exit codes:** 0 on success. 1 on runtime failure (schema not readable, IO). 2 on bad args. **No special exit for "skipped > 0"** — skips are informational, not failures.

**Acceptance criteria:**
- [ ] Dry-run prints preview without modifying files.
- [ ] Apply modifies files and writes state.
- [ ] Second apply with same schema is a no-op (covered by state-tracking in #197).
- [ ] Include/exclude globs work.
- [ ] Exit codes follow §4 convention.
- [ ] In-process CLI test coverage.

### b) Behavior (BDD)

Group: **happy**
1. *Given* a valid schema and a project directory with one matching `.cs` file, *when* `apply` runs, *then* the file is modified and exit code is 0.
2. *Given* the same inputs with `--dry-run`, *when* run, *then* exit code is 0, no files modified, summary shows the preview.
3. *Given* a successful apply, *when* `apply` is invoked again with the same arguments, *then* the summary shows 0 newly applied (idempotent via state).
4. *Given* `--include "**/Components/*.cs"`, *when* run, *then* only files matching that glob are touched.
5. *Given* `--exclude "**/Generated/**"`, *when* run, *then* generated files are skipped.

Group: **sad**
6. *Given* `--schema does-not-exist.json`, *when* run, *then* exit code is 1 with "schema not found".
7. *Given* a malformed schema file, *when* run, *then* exit code is 1 with the JSON parse error.
8. *Given* `--project-dir does-not-exist`, *when* run, *then* exit code is 1.
9. *Given* no `--schema`, *when* run, *then* exit code is 2 (required flag).

Group: **edge**
10. *Given* a schema with zero rules, *when* run, *then* exit code is 0 with "no rules to apply".
11. *Given* a schema where every rule's confidence is `Manual`, *when* run, *then* exit code is 0, `Applied.Count == 0`, `Manual` populated.
12. *Given* a `--dry-run` against a read-only directory, *when* run, *then* exit code is 0 (dry-run never writes).
13. *Given* `--json`, *when* the output is captured, *then* it parses to JSON and contains keys `applied`, `skipped`, `manual`, `dryRun`, `filesScanned`, `filesModified`.
14. *Given* a `.cs` file with parse errors, *when* run, *then* the file is processed best-effort and the summary reports any successful rewrites within the partial tree.

### c) Tests (TDD)

**Test class:** `WrapGod.Tests.MigrateApplyCliTests` in-process.

| Test method | Asserts |
|---|---|
| `Apply_Help_ListsAllFlags` | help text |
| `Apply_HappyPath_ModifiesFile` | file content changed, state created |
| `Apply_DryRun_DoesNotModifyFile` | file content unchanged, no state |
| `Apply_SecondRun_IsNoOp` | applied count stable on second invocation |
| `Apply_Include_FiltersFiles` | only matching files modified |
| `Apply_Exclude_FiltersFiles` | excluded files untouched |
| `Apply_SchemaMissing_Fails` | exit 1 |
| `Apply_SchemaMalformed_Fails` | exit 1 |
| `Apply_ProjectDirMissing_Fails` | exit 1 |
| `Apply_NoSchemaFlag_Fails` | exit 2 |
| `Apply_ZeroRules_SucceedsWithNote` | exit 0, message |
| `Apply_AllManual_AppliesNothing` | applied count 0, manual populated |
| `Apply_Json_OutputParsesAsJson` | parses; required keys present |
| `Apply_PartialTree_StillRewrites` | broken file handled |

Each test uses an `InMemoryFileSystem`-equivalent under `Path.GetTempPath()` with a per-test GUID directory.

**Coverage:** 88%+ on `MigrateApplyCommand`.

### d) Files to create/modify

**Create:**
- `WrapGod.Cli/MigrateApplyCommand.cs`
- `WrapGod.Cli/Globbing/FileMatcherHelper.cs` (wrapper around `Microsoft.Extensions.FileSystemGlobbing.Matcher`)
- `WrapGod.Tests/MigrateApplyCliTests.cs`

**Modify:**
- `WrapGod.Cli/MigrateCommandBuilder.cs` — add `apply` subcommand.
- `WrapGod.Cli/WrapGod.Cli.csproj` — add `<ProjectReference Include="..\WrapGod.Migration.Engine\WrapGod.Migration.Engine.csproj" />` and a `PackageReference` for `Microsoft.Extensions.FileSystemGlobbing` if not transitively available.
- `WrapGod.Tests/CliCommandTests.cs` — update wiring assertions.

### e) Docs deliverable

- `docs/guide/cli.md`, `docs/CLI.md`, `README.md` — add the new command.
- `docs/migration/applying.md` (NEW) — the consumer workflow walkthrough; `#204` will fold this into the larger docs deliverable but stub it here.

### f) Samples deliverable

- None (E2E demo lives in #203).

### g) Done when

- All 14 tests green.
- Coverage gate passes.
- Docs and README updated.
- `gh issue close 199 --reason completed --comment "Closed by PR #<n>. migrate apply (with --dry-run) lands and is idempotent via state."`

---

## Issue #200 — CLI: `migrate status [--json]`

### a) Specification (SDD)

**Introduces:** Read-only command that loads the state file and prints progress.

**Command surface:**

```
wrap-god migrate status
    --schema, -s <path>        (required) path to migration schema JSON
    --project-dir, -p <path>   (optional) defaults to cwd
    --json                     (optional) JSON output
```

**Behavior:**
1. Compute state path via `MigrationStateStore.GetStatePath(schemaPath)`.
2. Read state file. If missing, print "No migration runs recorded for this schema." and exit 0.
3. Load schema for context (library name, from/to, total rule count).
4. Print human-readable summary; with `--json`, print serialised state plus computed progress percentage.

**Human output format:**

```
WrapGod migrate status
----------------------
Migration:   MudBlazor 6.0.0 → 7.0.0
Started:     2026-04-01 12:00:00 UTC
Last run:    2026-04-02 09:14:33 UTC
Progress:    38 / 47 rules applied (81%)

Applied:  38   (across 22 files)
Skipped:  6
Manual:   3

Skipped rules:
  MUD-017 src/Dialogs/Confirm.cs:42  Ambiguous: two overloads of Show() in scope
  ...

Manual rules (require human intervention):
  MUD-003 Parameters restructured -- requires manual mapping
    matched in:
      src/Dialogs/Confirm.cs
      src/Dialogs/Edit.cs
```

**JSON output:** the raw `MigrationState` plus a synthesized top-level `progressPct` (0.0–1.0) and the migration's `library/from/to`.

**Acceptance criteria:**
- [ ] Reads state file and displays progress.
- [ ] Shows skipped rules with reasons.
- [ ] Shows manual rules with notes and matched files.
- [ ] `--json` outputs machine-readable format.
- [ ] Missing state file handled gracefully.
- [ ] In-process CLI test coverage.

### b) Behavior (BDD)

Group: **happy**
1. *Given* a state file with 38 applied, 6 skipped, 3 manual, *when* `status` runs, *then* exit 0, stdout contains "38 / 47 rules applied" and "81%".
2. *Given* the same state, *when* `--json` is set, *then* stdout parses as JSON and has `progressPct ≈ 0.808`.
3. *Given* a state file with no skipped, *when* run, *then* the "Skipped rules:" section is omitted but a "Skipped: 0" line is present.

Group: **sad**
4. *Given* no state file, *when* run, *then* exit 0 with "No migration runs recorded for this schema." (NOT an error).
5. *Given* a corrupt state file, *when* run, *then* exit 1 with the parse error.
6. *Given* `--schema does-not-exist.json`, *when* run, *then* exit 1.

Group: **edge**
7. *Given* a schema with zero rules and an empty state file, *when* run, *then* progress is reported as 0/0 (display as `0 / 0 rules applied (n/a)`).
8. *Given* `--json` with no state file, *when* run, *then* exit 0 with JSON `{ "status": "no-runs-recorded" }`.
9. *Given* a manual rule with empty `matchedFiles`, *when* displayed, *then* the section omits the "matched in:" indent and reports "(no files matched yet)".

### c) Tests (TDD)

**Test class:** `WrapGod.Tests.MigrateStatusCliTests`.

| Test method | Asserts |
|---|---|
| `Status_Help_ListsAllFlags` | help text |
| `Status_HappyPath_PrintsProgress` | stdout contains expected strings |
| `Status_Json_ParsesAsJson` | parses, has required keys |
| `Status_NoState_PrintsFriendlyMessage` | exit 0, "No migration runs recorded" |
| `Status_CorruptState_Fails` | exit 1 |
| `Status_MissingSchema_Fails` | exit 1 |
| `Status_NoSchemaFlag_Fails` | exit 2 |
| `Status_ZeroRules_HandlesNa` | "n/a" displayed |
| `Status_Json_NoState_OutputsSentinel` | `status == "no-runs-recorded"` |
| `Status_ManualWithEmptyMatched_PrintsPlaceholder` | "(no files matched yet)" |

**Coverage:** 88%+.

### d) Files to create/modify

**Create:**
- `WrapGod.Cli/MigrateStatusCommand.cs`
- `WrapGod.Tests/MigrateStatusCliTests.cs`

**Modify:**
- `WrapGod.Cli/MigrateCommandBuilder.cs` — add `status`.
- `WrapGod.Tests/CliCommandTests.cs` — wiring assertion.

### e) Docs deliverable

- `docs/guide/cli.md`, `docs/CLI.md`, `README.md` updated.
- `docs/migration/applying.md` (MODIFY) — add a "Check status" section.

### f) Samples deliverable

- None.

### g) Done when

- All 10 tests green.
- Coverage gate passes.
- `gh issue close 200 --reason completed --comment "Closed by PR #<n>. migrate status with --json support shipping."`

---

## Issue #201 — CLI: `migrate verify` (semantic, ±3 lines)

### a) Specification (SDD)

**Introduces:** Optional post-apply semantic check that compiles the project and correlates each compiler diagnostic to a migration rule when the diagnostic falls within ±3 lines of a state-recorded rewrite.

**Command surface:**

```
wrap-god migrate verify
    --project-dir, -p <path>      (optional) defaults to cwd
    --schema, -s <path>           (optional) if absent, auto-detect via state files in project-dir
    --baseline <path>             (optional) pre-migration diagnostic snapshot for net-new computation
    --json                        (optional) JSON output
```

**Behavior:**
1. Locate state file. If `--schema` not given, search `--project-dir` (and `--project-dir/../`) for `*.wrapgod-migration.json.state.json`. Pick most recent. Bail if none.
2. Locate project file (`*.csproj`). If multiple, require user to pass `--schema` explicitly with adjacent `.csproj` — error otherwise.
3. Invoke `dotnet build --nologo --no-restore -p:WarningLevel=4 -p:RunAnalyzers=false` capturing stderr+stdout. Parse diagnostics via regex matching `path(line,col): error|warning CODE: message`.
4. Load baseline if provided (same format). Compute `netNew = current \ baseline` by `(path, line, code)` tuple.
5. For each diagnostic, look up rewrites in the state file by `(path, line ± 3)`. If a match is found, attribute the diagnostic to that rule's `RuleId`.
6. Print a report. Exit 0 even if there are diagnostics (verify is non-gating per the issue body).

**Attribution rule (LOCKED):** A diagnostic at `(file F, line L)` is attributed to rewrite `R` if and only if:
- `R.File == F` (normalised path comparison, case-insensitive on Windows), AND
- `abs(R.Line - L) <= 3`.

Ties: if multiple rewrites in the same file are within 3 lines, attribute to the one with the smallest absolute distance; tiebreak by `R.AppliedAt` descending (latest rewrite wins).

**Human output:**

```
WrapGod migrate verify
----------------------
Project:   ./src/MyApp.csproj
Schema:    mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json
Baseline:  (none)

Build:     SUCCEEDED (0 errors, 4 warnings)
Or:        FAILED (12 errors, 3 warnings)

Attribution:
  MUD-003   3 errors  (Parameters restructured -- requires manual mapping)
  MUD-017   1 error
  Unattributed: 8 errors, 4 warnings (likely pre-existing)
```

**Graceful degradation:**
- If `dotnet build` is not available on PATH → exit 0 with stderr note "dotnet build not found; skipping verify".
- If the project does not compile at all (catastrophic failure) → exit 0 with note "project failed to compile; raw diagnostics:" and dump the first 50.
- If no state file → exit 0 with "no migration state; nothing to verify".

**Exit codes:** Always 0 for the verify run itself unless invocation args were bad (exit 2). This is explicitly non-gating.

**Acceptance criteria:**
- [ ] Compiles project and collects diagnostics.
- [ ] Attributes errors to migration rules via state file correlation (±3 lines).
- [ ] Reports pre-existing vs net-new diagnostics (baseline support).
- [ ] Handles projects that don't compile at all.
- [ ] In-process CLI test coverage.

### b) Behavior (BDD)

Group: **happy**
1. *Given* a state file with one applied rewrite at `Foo.cs:14` and a successful `dotnet build` returning a CS0103 at `Foo.cs:16`, *when* `verify` runs, *then* the report attributes the diagnostic to the rule at line 14 (delta 2, within ±3).
2. *Given* the same setup with the diagnostic at `Foo.cs:18`, *when* `verify` runs, *then* the diagnostic is unattributed (delta 4).
3. *Given* a baseline that already contained the CS0103 at `Foo.cs:16`, *when* `verify` runs, *then* the diagnostic is classified as pre-existing and not blamed on the rule.
4. *Given* a fresh build with zero errors, *when* `verify` runs, *then* the report says "0 errors" and the exit is 0.

Group: **sad**
5. *Given* `dotnet build` not on PATH (mocked), *when* `verify` runs, *then* exit 0 and stderr contains the skip note.
6. *Given* no state file present, *when* `verify` runs, *then* exit 0 with "no migration state".
7. *Given* an unparseable diagnostic line in `dotnet build` output, *when* `verify` runs, *then* the line is logged as a warning and processing continues.
8. *Given* `--baseline does-not-exist`, *when* `verify` runs, *then* exit 1 with "baseline file not found".

Group: **edge**
9. *Given* two rewrites in the same file at lines 10 and 14, and a diagnostic at line 12, *when* attributing, *then* the diagnostic goes to the rule at line 10 (delta 2 < delta 2 tie → tiebreak by `AppliedAt` descending; if 14's AppliedAt is later, it wins).
10. *Given* a diagnostic with Windows-style path on a case-insensitive FS, *when* matching, *then* case differences are ignored.
11. *Given* `--json`, *when* output is captured, *then* it parses to JSON containing `build.exitCode`, `attribution[]`, `unattributed[]`.

### c) Tests (TDD)

**Test class:** `WrapGod.Tests.MigrateVerifyCliTests`.

| Test method | Asserts |
|---|---|
| `Verify_Help_ListsAllFlags` | help text |
| `Verify_AttributesDiagnosticWithin3Lines` | rule matched |
| `Verify_DoesNotAttributeBeyond3Lines` | unattributed |
| `Verify_BaselineSubtractsPreExisting` | only net-new attributed |
| `Verify_BuildSucceeds_ReportsZeroErrors` | clean report |
| `Verify_BuildNotOnPath_SkipsGracefully` | exit 0 |
| `Verify_NoStateFile_SkipsGracefully` | exit 0 |
| `Verify_UnparseableDiagnostic_LogsWarning` | continues |
| `Verify_BaselineMissing_Fails` | exit 1 |
| `Verify_TwoRulesWithinRange_PicksClosestThenLatest` | tiebreak logic |
| `Verify_PathCaseInsensitiveMatch` | matches |
| `Verify_Json_ParsesAsJson` | required keys |

**Approach to mocking `dotnet build`:** introduce a small `IBuildRunner` abstraction; the test injects a fake that returns a canned diagnostic stream. The real implementation shells out to `dotnet`.

**Coverage:** 88%+ on `MigrateVerifyCommand` (excluding `IBuildRunner` real implementation, which has integration coverage via a single end-to-end test using a tiny test project).

### d) Files to create/modify

**Create:**
- `WrapGod.Cli/MigrateVerifyCommand.cs`
- `WrapGod.Cli/Verification/IBuildRunner.cs` (internal)
- `WrapGod.Cli/Verification/DotnetBuildRunner.cs` (internal)
- `WrapGod.Cli/Verification/DiagnosticParser.cs` (parses MSBuild output)
- `WrapGod.Cli/Verification/RuleAttributor.cs` (±3-line logic)
- `WrapGod.Tests/MigrateVerifyCliTests.cs`
- `WrapGod.Tests/Fixtures/Verify/TinyBrokenProject/` (tiny .csproj used by integration test)

**Modify:**
- `WrapGod.Cli/MigrateCommandBuilder.cs` — add `verify`.
- `WrapGod.Tests/CliCommandTests.cs` — wiring assertion.

### e) Docs deliverable

- `docs/guide/cli.md`, `docs/CLI.md`, `README.md` updated.
- `docs/migration/verifying.md` (NEW) — explains attribution logic, baseline workflow, when to run verify.

### f) Samples deliverable

- The tiny broken-project fixture serves as the only sample for now; the E2E example in #203 will include a verify pass.

### g) Done when

- All 12 tests green.
- Coverage gate passes.
- Docs present.
- `gh issue close 201 --reason completed --comment "Closed by PR #<n>. migrate verify with ±3-line attribution and graceful degradation."`

---

## Issue #202 — B-level structural rewriters

### a) Specification (SDD)

**Introduces:** Four additional `IRuleRewriter` implementations for the structural rule kinds in `MigrationRuleKind`:

| Rewriter | Kind | Description |
|---|---|---|
| `SplitMethodRewriter` | `SplitMethod` | One call → multiple sequential calls. **Behavior:** comment out the original call and insert `// MIGRATION: split into NewMethodNames` followed by `obj.NewA();\nobj.NewB();\nobj.NewC();` on adjacent lines. Confidence-`Manual` if the user actually consumed the return value; in that case, skip with reason "split-method requires manual review when return value is consumed". |
| `ExtractParameterObjectRewriter` | `ExtractParameterObject` | `foo.M(a, b, c)` → `foo.M(new ParamObj { A = a, B = b, C = c })`. The mapping of positional/named arguments to the new object's properties is read from the rule's `ExtractedParameters`. Match by parameter *name* where possible; skip with reason if positional args can't be mapped unambiguously. |
| `PropertyToMethodRewriter` | `PropertyToMethod` | `obj.IsEnabled = true` → `obj.SetEnabled(true)`; `var x = obj.IsEnabled` → `var x = obj.GetEnabled()` (when rule supplies a getter form) — but the schema only carries `NewMethodName`. **Decision:** writes always go to `Set{NewMethodName}` if the property had a setter; reads to `Get{NewMethodName}` IF `NewMethodName` starts with neither `Get` nor `Set`, otherwise to `NewMethodName` verbatim. Records `Skipped` for ambiguous lvalue/rvalue contexts. |
| `MoveMemberRewriter` | `MoveMember` | `oldType.Member` → `newType.Member` at call sites. Updates `using` if `newType`'s namespace isn't imported. Matches by the rule's `OldTypeName` short name + `MemberName`. |

**Universal contract:** identical to A-level — trivia preservation, syntax-only matching, skip on ambiguity.

**Acceptance criteria:**
- [ ] All 4 structural rewriters implemented.
- [ ] Preserves trivia and formatting.
- [ ] Handles edge cases (method in expression context, chained calls).
- [ ] Each has dedicated unit tests.
- [ ] Integration test with real structural migration scenario.
- [ ] Coverage ≥ 90%.

### b) Behavior (BDD)

**`SplitMethodRewriter`**
1. *Happy* — `card.Render();` with rule splitting into `RenderHeader/Body/Footer` becomes three lines, original commented.
2. *Sad* — `var html = card.Render();` records `SkippedRewrite` "split-method requires manual review when return value is consumed".
3. *Edge* — chained `card.Render().ToString()` skips with "chained-call".

**`ExtractParameterObjectRewriter`**
4. *Happy* — `dlg.ShowAsync(title: "T", content: c)` with extracted-params `["title", "content"]` and `ParameterObjectType = "DialogParameters"` becomes `dlg.ShowAsync(new DialogParameters { Title = "T", Content = c })`.
5. *Sad* — positional `dlg.ShowAsync("T", c)` where ordering can't be confirmed against the rule's parameter list skips with "positional arguments require named-argument migration".
6. *Edge* — extra parameters not in `ExtractedParameters` are left in the call and the new object is appended.

**`PropertyToMethodRewriter`**
7. *Happy* — `btn.Disabled = true` with `NewMethodName = "SetDisabled"` becomes `btn.SetDisabled(true)`.
8. *Happy* — `var b = btn.Disabled` becomes `var b = btn.GetDisabled()`.
9. *Sad* — `btn.Disabled++` skips with "compound-assignment not supported".
10. *Edge* — `NewMethodName = "ChangeDisabled"` (no Get/Set prefix) becomes `btn.ChangeDisabled(true)` on writes and `var b = btn.ChangeDisabled()` on reads (matches rule semantics; the rewriter does not synthesize Get/Set in this case).

**`MoveMemberRewriter`**
11. *Happy* — `Utilities.GetColor()` with rule moving `GetColor` to `MudHelpers` becomes `MudHelpers.GetColor()`; adds `using MudBlazor;` if not present.
12. *Sad* — `myInstance.GetColor()` where `myInstance` isn't a static reference to the old type → skip with "instance call cannot be moved syntactically".

### c) Tests (TDD)

Mirror #195 structure: one test class per rewriter (`SplitMethodRewriterTests`, etc.), 5–8 scenarios each, plus a `StructuralRewriterIntegrationTests` running all four against one synthetic source.

Coverage target: 90% on `WrapGod.Migration.Engine.Rewriters.Structural.*`.

### d) Files to create/modify

**Create:**
- `WrapGod.Migration.Engine/Rewriters/Structural/SplitMethodRewriter.cs`
- `WrapGod.Migration.Engine/Rewriters/Structural/ExtractParameterObjectRewriter.cs`
- `WrapGod.Migration.Engine/Rewriters/Structural/PropertyToMethodRewriter.cs`
- `WrapGod.Migration.Engine/Rewriters/Structural/MoveMemberRewriter.cs`
- `WrapGod.Tests/SplitMethodRewriterTests.cs` (etc., 4 files)
- `WrapGod.Tests/StructuralRewriterIntegrationTests.cs`

**Modify:**
- `WrapGod.Migration.Engine/MigrationEngine.cs` — `CreateDefault()` includes the new rewriters.

### e) Docs deliverable

- `docs/migration/engine.md` (MODIFY) — extend rewriter table with B-level entries and examples.

### f) Samples deliverable

- None at this level. The E2E example (#203) may or may not exercise B-level rewriters depending on the chosen target; Serilog 2→3 is mostly A-level, so plan for a small B-level scenario in the test fixtures only.

### g) Done when

- All ~25 tests green.
- Coverage gate passes.
- Docs section updated.
- `gh issue close 202 --reason completed --comment "Closed by PR #<n>. All 4 B-level structural rewriters land with edge-case coverage."`

---

## Issue #203 — E2E example with real library migration

### a) Specification (SDD)

**Introduces:** A complete, CI-validated end-to-end example demonstrating: generate → review → dry-run → apply → status → verify, using a real library version pair.

**Library choice (LOCKED): Serilog v2 → v3.** Rationale: namespace flattening and minor API cleanup are dominated by A-level rewrites (good first showcase). Serilog already has a `serilog-nlog-bidirectional` example in-repo — we will *not* duplicate that; we are demonstrating *upgrade migration*, not *library swap*.

**Directory layout:**

```
examples/migrations/serilog-v2-to-v3/
    README.md
    before/                                     # Serilog v2-using project
        Serilog.V2.Sample.csproj
        Program.cs
        Logging/MyLogger.cs
    after/                                      # Expected post-migration source
        Serilog.V3.Sample.csproj
        Program.cs
        Logging/MyLogger.cs
    schema/
        serilog.2.x-to-3.x.wrapgod-migration.json
    state/
        serilog.2.x-to-3.x.wrapgod-migration.json.state.json   # post-apply state, committed
    scripts/
        run-migration.ps1                       # generate + apply + status + verify
        run-migration.sh                        # *nix equivalent
```

**Workflow demonstrated in `README.md`:**

```bash
# From repo root:
cd examples/migrations/serilog-v2-to-v3

# 1. Generate a draft schema
dotnet run --project ../../../WrapGod.Cli -- migrate generate \
    --package Serilog --from 2.12.0 --to 3.1.1 \
    --output schema/serilog.2.x-to-3.x.wrapgod-migration.json

# 2. (Human edits enrichment notes/confidence into the schema; the committed schema is post-edit)

# 3. Dry-run preview
dotnet run --project ../../../WrapGod.Cli -- migrate apply \
    --schema schema/serilog.2.x-to-3.x.wrapgod-migration.json \
    --project-dir ./before \
    --dry-run

# 4. Apply
dotnet run --project ../../../WrapGod.Cli -- migrate apply \
    --schema schema/serilog.2.x-to-3.x.wrapgod-migration.json \
    --project-dir ./before

# 5. Status
dotnet run --project ../../../WrapGod.Cli -- migrate status \
    --schema schema/serilog.2.x-to-3.x.wrapgod-migration.json \
    --project-dir ./before

# 6. Verify (after manually bumping Serilog package ref in before/ to v3)
dotnet run --project ../../../WrapGod.Cli -- migrate verify \
    --project-dir ./before
```

**CI parity validation:** A test under `WrapGod.Tests/MigrationE2ETests.cs` (or extend `examples.yml` workflow) that:
1. Copies `examples/migrations/serilog-v2-to-v3/before/` to a temp directory.
2. Runs the CLI `migrate apply` against the committed schema.
3. Diffs the resulting tree against `after/`. Test fails on any difference.
4. Optionally builds `after/` with Serilog v3 referenced to confirm the migration target compiles.

**Acceptance criteria:**
- [ ] Working end-to-end example with real Serilog v2 → v3.
- [ ] Schema generated via `migrate generate`, then hand-verified (the committed schema includes enrichment notes that the auto-generator wouldn't add).
- [ ] `migrate apply` transforms `before/` to match `after/`.
- [ ] CI test validates parity.
- [ ] README documents the full workflow.

### b) Behavior (BDD)

Group: **happy**
1. *Given* the committed schema and `before/` tree, *when* `migrate apply` runs into a temp copy, *then* the result is byte-equal to `after/` (modulo line-ending normalisation).
2. *Given* the committed state file, *when* `migrate status` runs, *then* it reports 100% progress.
3. *Given* `before/` with Serilog v2 package ref, *when* `migrate verify` runs after bumping to v3 and applying, *then* zero net-new errors are reported.

Group: **sad**
4. *Given* `before/` mutated to drop a using directive, *when* `apply` runs, *then* some rewrites become `Skipped` (documented in README's troubleshooting section).

Group: **edge**
5. *Given* the example CI test runs on Windows and Linux, *when* the parity diff is computed, *then* line-ending differences are normalised and the test passes on both OSes.

### c) Tests (TDD)

**Test class:** `WrapGod.Tests.SerilogMigrationE2ETests` (or extend the existing examples CI; the cleanest approach is a new in-test runner).

| Test method | Asserts |
|---|---|
| `Apply_BeforeMatchesAfter_AfterMigration` | byte-equal diff |
| `Status_ShowsCompleteAfterApply` | progress 100% |
| `Verify_ZeroNetNew_AfterRealUpgrade` | (integration; behind `[Trait("Category","Network")]` because it needs Serilog v3 from NuGet) |

**Coverage:** Not the focus — these are integration tests. They contribute to coverage incidentally.

### d) Files to create/modify

**Create (new directory):**
- `examples/migrations/serilog-v2-to-v3/README.md`
- `examples/migrations/serilog-v2-to-v3/before/Serilog.V2.Sample.csproj`
- `examples/migrations/serilog-v2-to-v3/before/Program.cs`
- `examples/migrations/serilog-v2-to-v3/before/Logging/MyLogger.cs`
- `examples/migrations/serilog-v2-to-v3/after/Serilog.V3.Sample.csproj`
- `examples/migrations/serilog-v2-to-v3/after/Program.cs`
- `examples/migrations/serilog-v2-to-v3/after/Logging/MyLogger.cs`
- `examples/migrations/serilog-v2-to-v3/schema/serilog.2.x-to-3.x.wrapgod-migration.json`
- `examples/migrations/serilog-v2-to-v3/state/serilog.2.x-to-3.x.wrapgod-migration.json.state.json`
- `examples/migrations/serilog-v2-to-v3/scripts/run-migration.ps1`
- `examples/migrations/serilog-v2-to-v3/scripts/run-migration.sh`
- `WrapGod.Tests/SerilogMigrationE2ETests.cs`

**Modify:**
- `examples/WrapGod.Examples.slnx` — add the new before/after projects.
- `.github/workflows/examples.yml` — add a job that runs the parity test.

### e) Docs deliverable

- `docs/migration/examples.md` (NEW or MODIFY) — link out to the example directory.

### f) Samples deliverable

This issue *is* the sample. Done by definition.

### g) Done when

- Apply-and-diff test green on Windows + Linux CI.
- README contains the full workflow with copy-pasteable commands.
- The schema is committed in its enriched form (some manual confidence levels overridden from `Auto` to `Verified`).
- `gh issue close 203 --reason completed --comment "Closed by PR #<n>. Serilog v2→v3 E2E example lands with CI parity test."`

---

## Issue #204 — Documentation: migration authoring + applying

### a) Specification (SDD)

**Introduces:** The cross-cutting documentation suite for the entire migration engine. Pulls together stubs from #193/#194/#197/#199/#201 into a coherent reading order.

**Four pages (Status: produced or upgraded):**

1. `docs/migration/index.md` (UPGRADE) — landing page. Explains the engine in three paragraphs, links to the four sub-pages.
2. `docs/migration/authoring.md` (NEW) — for migration-pack authors. Covers:
    - When to author a pack (vs. relying on `migrate generate`)
    - Each rule kind with a JSON example and an "after rewrite" snippet
    - Confidence levels and when to bump from `Auto` to `Verified`
    - The `Note` field as authorial intent capture
    - Testing your pack on a sample project
    - Distributing as a file (preferred) or NuGet content package (advanced)
3. `docs/migration/applying.md` (UPGRADE) — for consumers. Covers:
    - `generate → review → dry-run → apply → status → verify` flow
    - Reading the schema before applying
    - Handling skipped rules
    - Handling manual rules
    - Re-running after schema edits (hash-change semantics)
4. `docs/migration/schema.md` (UPGRADE) — schema reference. Already exists for v1; expand with examples for every rule kind, the polymorphism convention (`kind` discriminator), and validation via `wrapgod-migration.v1.schema.json`.

Plus:
5. `docs/guide/cli.md` (UPGRADE) — add a `migrate` section under the existing CLI reference with every flag.
6. `docs/CLI.md` (UPGRADE) — mirror.
7. `README.md` (UPGRADE) — add `migrate` rows to the CLI table; add a "Migrating between library versions" paragraph under the existing "How WrapGod Solves It" section pointing to `docs/migration/index.md`.

**Cross-linking checklist:** Every page links back to `docs/migration/index.md`; the index links to all four sub-pages; each sub-page links to the CLI reference. Run a link-check (markdown-link-check or similar) as part of the docs CI job if it doesn't exist yet — defer to `docs.yml` workflow.

**Acceptance criteria:**
- [ ] All four doc pages written.
- [ ] CLI reference updated with the four new commands.
- [ ] README updated.
- [ ] Schema format fully documented with examples for every rule kind.
- [ ] Links cross-referenced and working.

### b) Behavior (BDD) — applied to docs

Docs BDD is light, but verifiable:

1. *Given* `docs/migration/index.md`, *when* read top-to-bottom, *then* it links to `authoring.md`, `applying.md`, `schema.md`, `engine.md`.
2. *Given* `docs/migration/authoring.md`, *when* scanned, *then* every `MigrationRuleKind` value has a section with a JSON example.
3. *Given* `docs/guide/cli.md`, *when* searched, *then* the four migrate sub-commands appear with all flags.
4. *Given* the rendered docs (`docs.yml` workflow), *when* the link-check runs, *then* no broken internal links.
5. *Given* `README.md`'s CLI table, *when* read, *then* `migrate generate`, `apply`, `status`, `verify` rows exist.

### c) Tests (TDD)

**Test class:** `WrapGod.Tests.MigrationDocsCoverageTests` (small, mechanical — ensures the docs mention every rule kind).

| Test method | Asserts |
|---|---|
| `AuthoringDoc_MentionsEveryRuleKind` | iterate `MigrationRuleKind` enum, each name appears in `authoring.md` |
| `SchemaDoc_HasJsonExamplePerRuleKind` | regex for `"kind": "<each>"` |
| `CliDoc_ListsAllMigrateSubcommands` | strings `migrate generate`, `migrate apply`, `migrate status`, `migrate verify` present in `cli.md` |
| `ReadmeCliTable_ListsMigrateCommands` | same |
| `IndexDoc_LinksToSubpages` | parses markdown links |

These are static-text grep-style assertions — they catch doc drift on subsequent changes.

**Link-check:** rely on the docs.yml workflow's existing job if present; otherwise add a `markdown-link-check` step.

**Coverage:** Documentation-only; tests above are minimal but count toward `WrapGod.Tests` coverage. Aim for 100% on `MigrationDocsCoverageTests` itself (trivially).

### d) Files to create/modify

**Create:**
- `docs/migration/authoring.md`
- `docs/migration/applying.md` (the earlier issues stubbed this; final content lands here)
- `docs/migration/examples.md` (linked from #203)
- `WrapGod.Tests/MigrationDocsCoverageTests.cs`

**Modify:**
- `docs/migration/index.md`
- `docs/migration/schema.md`
- `docs/migration/engine.md`
- `docs/migration/state.md`
- `docs/migration/verifying.md`
- `docs/migration/schema-generation.md`
- `docs/guide/cli.md`
- `docs/CLI.md`
- `README.md`

### e) Docs deliverable

The full deliverable IS this issue. Nothing further.

### f) Samples deliverable

- Snippets within docs only.

### g) Done when

- All 5 doc tests green.
- Docs.yml workflow passes including link-check.
- All migrate commands appear in README + CLI reference.
- `gh issue close 204 --reason completed --comment "Closed by PR #<n>. Full migration docs suite (authoring + applying + schema + CLI + README) shipping."`

---

## Issue #191 — Epic: Migration Engine (parent)

Closes automatically when #193–#204 close. The implementation issues are children; #192 is already closed; the remaining 11 need to ship.

**Closure mechanism:** Once the last of #193, #194, #195, #196, #197, #198, #199, #200, #201, #202, #203, #204 is merged to `main` with green CI, post the closure comment below and run `gh issue close 191`.

**Draft closure comment for #191:**

> # Migration Engine epic complete
>
> All 12 child issues (#192 through #204) are closed:
>
> - #192 Scaffold + schema model (closed via PR #206)
> - #193 Schema generator from VersionDiff
> - #194 Migration.Engine scaffold + IRuleRewriter
> - #195 A-level syntax rewriters (7 rewriters)
> - #196 MigrationEngine orchestrator (Apply + DryRun)
> - #197 State tracking + idempotent re-runs
> - #198 CLI: migrate generate
> - #199 CLI: migrate apply --dry-run
> - #200 CLI: migrate status [--json]
> - #201 CLI: migrate verify (±3 lines attribution)
> - #202 B-level structural rewriters (4 rewriters)
> - #203 E2E example — Serilog v2 → v3 with CI parity
> - #204 Docs — authoring + applying + schema + CLI + README
>
> The engine ships in v0.2-alpha. Future work (C-level semantic rewrites, IDE integration per #53, additional bidirectional packs) lives in separate epics.

---

## Issue #1 — MVP Master Plan

### a) Spec

This is the long-running tracking issue. Per inspection, every sub-issue called out (#68, #69, #70, #71, #73, #74, #76, #81, #82, #83) is **already closed**. The MVP foundation is done. The remaining "open" status on #1 is purely administrative.

### Closure path

Post the closure comment and run `gh issue close 1 --reason completed`.

**Draft closure comment for #1:**

> # MVP Master Plan complete
>
> All P1 sub-issues tracked in this master plan are closed:
>
> **Generic processing chain (#68–#71)** — closed via PRs #89/#90/etc.
> **Performance & diagnostics (#73, #74, #81, #82, #83)** — closed.
> **Extractor cache (#76)** — closed.
>
> The MVP scope (extract → manifest → generate → analyze → migrate) is shipping in v0.1-alpha. The Migration Engine epic (#191) extends the MVP with automated code rewriting and is tracked separately.
>
> Deferred:
> - **#53 IDE Extension RFC** — explicitly deferred to vNext. See its own closure note.
>
> Closing this tracking issue. New roadmap items move to their own epics.

### Done when

- `gh issue close 1 --reason completed --comment "<as above>"` runs cleanly.

---

## Issue #53 — [Deferred] IDE Extension RFC

### a) Spec

Open RFC for VS / VS Code extension. Explicitly deferred to vNext per its own body.

### Closure path

Convert to a GitHub Discussion if the maintainer wants to preserve the conversation (`gh issue transfer` or manual copy), OR close with a deferred-explicitly comment. **Recommendation:** close with comment (simpler; the RFC content stays in the issue history).

**Draft closure comment for #53:**

> # Deferred to vNext — closing this RFC
>
> The IDE Extension RFC was always conditional on three prerequisites:
> 1. Core pipeline hardening (✓ done in v0.1-alpha)
> 2. CLI/user workflows stabilisation (✓ done; migration engine ships in v0.2-alpha — see #191)
> 3. Diagnostics/reporting contract finalisation (✓ done; RFC-0054 landed via #81/#82/#83)
>
> With those prerequisites met, we're ready to formally split IDE work into focused epics:
> - VS Code extension (LSP-style hookup to `wrap-god analyze` and `wrap-god migrate status`)
> - Visual Studio extension (richer manifest browser + code-fix surfacing)
>
> Closing this RFC. Follow-ups will be filed as separate epics when IDE work is queued for a release. The UX concepts documented above remain the design reference.

### Done when

- `gh issue close 53 --reason completed --comment "<as above>"`

---

## Issue #155 — [EPIC] Bidirectional wrapper packs

### a) Spec

Stale epic listing P0/P1 sub-issues #156–#162. Inspection shows the listed children have shipped (#156 closed via PR #173; the example folders `serilog-nlog-bidirectional`, `automapper-mapster-bidirectional`, `efcore-dapper-bidirectional`, `mediatr-masstransit-mediator-bidirectional`, `hangfire-quartz-bidirectional`, `MoqToNSubstitute`, `NSubstituteToMoq`, `NUnitToXUnit`, `XUnitToNUnit` are all checked in).

### Closure path

Narrow scope: declare the epic done as-of-shipping-children. File follow-up issues for any gap if needed.

**Draft closure comment for #155:**

> # Bidirectional wrapper packs epic complete (scope narrowed)
>
> All originally-listed P0 and P1 sub-issues have shipped:
>
> | Sub-issue | Pack | Status |
> |---|---|---|
> | #156 | Moq ↔ NSubstitute | shipped (PR #173) |
> | #157 | xUnit ↔ NUnit | shipped — examples/NUnitToXUnit, examples/XUnitToNUnit |
> | #158 | Serilog ↔ NLog | shipped — examples/migrations/serilog-nlog-bidirectional |
> | #159 | AutoMapper ↔ Mapster | shipped — examples/migrations/automapper-mapster-bidirectional |
> | #160 | EF Core ↔ Dapper | shipped — examples/migrations/efcore-dapper-bidirectional |
> | #161 | MediatR ↔ MassTransit Mediator | shipped — examples/migrations/mediatr-masstransit-mediator-bidirectional |
> | #162 | Hangfire ↔ Quartz.NET | shipped — examples/migrations/hangfire-quartz-bidirectional |
>
> The "shared safety tiering convention" and "standardised migration-outcome matrix" that were the cross-cutting deliverables of this epic are now implicit in the Migration Engine's confidence model (`Auto` / `Verified` / `Manual` — see #191/#193/#194).
>
> Future bidirectional packs will be filed as standalone issues rather than under a long-running epic. Closing this epic as complete.

### Done when

- `gh issue close 155 --reason completed --comment "<as above>"`

---

## Issue #163 — [EPIC] NuGet competing-version examples

### a) Spec

Stale epic listing sub-issues #164–#169. Inspection confirms children have closed (#164 via PR #170, etc.). The `examples/migrations/nuget-version-matrix/` directory contains the deliverables (fluent-assertions, moq, serilog, mediatr subfolders all present).

### Closure path

Same shape as #155 — declare done with a scope-narrowing comment.

**Draft closure comment for #163:**

> # NuGet competing-version examples epic complete
>
> All listed sub-issues have shipped under `examples/migrations/nuget-version-matrix/`:
>
> | Sub-issue | Package | Status |
> |---|---|---|
> | #164 | FluentAssertions version-matrix | shipped (PR #170) — examples/migrations/nuget-version-matrix/fluent-assertions |
> | #165 | Moq version-matrix | shipped — examples/migrations/nuget-version-matrix/moq |
> | #166 | Serilog version-matrix | shipped — examples/migrations/nuget-version-matrix/serilog |
> | #167 | Generic-heavy package version-matrix | shipped — examples/migrations/nuget-version-matrix/mediatr |
> | #168 | Version divergence compatibility report standard | shipped (compatibility-report.v1.schema.json) |
> | #169 | CI matrix job | shipped — `.github/workflows/examples.yml` |
>
> The Migration Engine epic (#191) builds the next layer — *automated* code rewriting that consumes version-divergence diffs to rewrite call sites. The example packs from this epic become reference fixtures for the engine.
>
> Closing as complete.

### Done when

- `gh issue close 163 --reason completed --comment "<as above>"`

---

## Section 3 — Epic Closure Strategy (summary)

| Issue | Strategy | Trigger | Comment status |
|---|---|---|---|
| **#191** | Auto-close when last child PR (anywhere in #193–#204) merges to main and CI is green. | Last child issue closes. | Draft in #191 block above. |
| **#1** | Scope-narrowing close — all listed P1 children already closed. | At any time; recommend bundling with #191's close to ship as a single "MVP + Migration Engine wrap" announcement. | Draft in #1 block. |
| **#53** | Deferred-RFC close. Optional: open follow-up epics for VS Code and Visual Studio separately. | Bundle with #191 close. | Draft in #53 block. |
| **#155** | Scope-narrowing close — all P0/P1 children closed, deliverables present in `examples/migrations/`. | Bundle with #191 close. | Draft in #155 block. |
| **#163** | Scope-narrowing close — all children closed, deliverables present in `examples/migrations/nuget-version-matrix/`. | Bundle with #191 close. | Draft in #163 block. |

The five epic closures can be done as a single batch on the same day:

```bash
gh issue close 191 --reason completed --comment "<#191 draft>"
gh issue close 1   --reason completed --comment "<#1 draft>"
gh issue close 53  --reason completed --comment "<#53 draft>"
gh issue close 155 --reason completed --comment "<#155 draft>"
gh issue close 163 --reason completed --comment "<#163 draft>"
```

---

## Section 4 — Cross-Cutting Concerns

### 4.1 Roslyn integration

- **Package:** `Microsoft.CodeAnalysis.CSharp`. Pin to the same major.minor as `WrapGod.Generator`/`WrapGod.Analyzers` (read their csproj at implementation time; do not hard-code here).
- **Parsing entry point:** `CSharpSyntaxTree.ParseText(text, path: filePath)`. Always pass `path` so diagnostics carry the file path through.
- **Rewriting pattern:** Implement an internal `CSharpSyntaxRewriter` subclass per pass; or, since each `IRuleRewriter` operates per-node, use a single dispatcher subclass that calls each rewriter's `TryRewrite` on every visited node.
- **Trivia preservation:** every token replacement uses `newToken.WithLeadingTrivia(old.LeadingTrivia).WithTrailingTrivia(old.TrailingTrivia)`. Every node replacement uses `newNode.WithTriviaFrom(oldNode)`. **No exceptions.**
- **Syntax-only mode:** never call `Compilation.Create` or `GetSemanticModel`. The engine works on non-compiling code by design. Any rewriter tempted to reach for semantics must instead record a `SkippedRewrite` and move on.

### 4.2 State file location convention (LOCKED)

| Schema file | State file |
|---|---|
| `./mudblazor.6.0-to-7.0.wrapgod-migration.json` | `./mudblazor.6.0-to-7.0.wrapgod-migration.json.state.json` |
| `./schema/x.wrapgod-migration.json` | `./schema/x.wrapgod-migration.json.state.json` |

Computed by `MigrationStateStore.GetStatePath(schemaPath) => schemaPath + ".state.json"`. State files are intended to be committed to source control.

### 4.3 CLI exit code convention

Already partially established via `WgCliExitCode` in `WrapGod.Abstractions.Diagnostics`. Locking the table for the new migrate commands:

| Code | Name | When |
|---|---|---|
| 0 | Success | Normal completion. Includes "no rules to apply" and "no state found" friendly paths. |
| 1 | RuntimeFailure | I/O failure, schema parse error, file not found, network failure, dotnet build crash. |
| 2 | UsageError | Invalid CLI args (missing required, mutually exclusive, unparseable values). System.CommandLine emits this naturally. |
| 3 | WarningGate | Reserved for `analyze --warnings-as-errors`. Migrate commands do NOT use 3. Skipped-rewrites are never failures. |

Implementers reuse `WgCliExitCode` enum.

### 4.4 Output formatting convention

All `migrate *` commands share:
- Header line: `WrapGod migrate <verb>` then `-` separator of same length.
- Field labels left-aligned in a 12-char column when human output.
- `--json` outputs a single JSON object with `kebab-case` top-level keys (`progress-pct`, `files-scanned`) for consistency with the diagnostic JSON contract from RFC-0054. **Decision:** if RFC-0054 emitter uses camelCase, follow that instead. Check `WgDiagnosticV1` and match. (`WgDiagnosticV1` uses PascalCase property names in C# but System.Text.Json defaults emit them PascalCase; the diagnostics schema in JSON uses lowerCamelCase. So **lowerCamelCase** is the convention.)
- All `--json` payloads serialize via `System.Text.Json` with `JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }`.
- All commands respect `NO_COLOR` env var (skip ANSI when set).

### 4.5 90% coverage gate maintenance

CI's coverage gate (`.github/workflows/ci.yml`, lines 41–91) parses per-package line-rates from cobertura. **It applies to every package, including new ones.** A scaffold project with no tests fails the gate immediately.

Strategy for each new project (`WrapGod.Migration.Engine`):
- Land contract tests in the same PR as the project scaffold (#194 does this).
- Use `InternalsVisibleTo("WrapGod.Tests")` so test code can exercise internal helpers (e.g., `IMigrationFileSystem`).
- For unavoidable low-coverage areas (CLI command help-text, perf benchmarks, network paths), use `[ExcludeFromCodeCoverage]` *sparingly* on those types — never on a whole namespace.
- Network/dotnet-build-shelling-out tests are tagged `[Trait("Category", "Network")]` or `[Trait("Category", "ExternalProcess")]` and excluded from the coverage collection run.

### 4.6 Branch / PR strategy

- One feature branch per issue: `feat/migration-<N>-<slug>`.
- PRs target `main`. Each PR closes its single issue via `Closes #N`.
- **Merge style: merge commit, not squash.** Issues #155 / #163 have a history of stacked-PR squash issues — do not repeat that.
- Commit message format per `C:/git/memstack/.claude/rules/memstack.md` (Conventional Commits with `feat`/`fix`/`docs`/`test`/`refactor`/`chore`). Use issue number in scope when useful, e.g. `feat(193): schema generator from VersionDiff`.
- Always `dotnet build` and `dotnet test` locally before push. Never `--no-verify`.

### 4.7 InternalsVisibleTo

Add to both new projects:
```xml
<ItemGroup>
  <InternalsVisibleTo Include="WrapGod.Tests" />
</ItemGroup>
```

---

## Section 5 — Risk Register

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| 1 | **Roslyn API surface differences across versions.** Pinning a different `Microsoft.CodeAnalysis.CSharp` version than `WrapGod.Generator` causes load conflicts when both ship in the same NuGet bundle. | Medium | High | Pin the same major/minor as `WrapGod.Generator`/`WrapGod.Analyzers`. Add a centralised `<MicrosoftCodeAnalysisVersion>` MSBuild property in `Directory.Build.props` for one-place version control. Add a CI smoke test that loads both assemblies in one app domain. |
| 2 | **Trivia preservation edge cases.** Rewriters that forget `WithTriviaFrom` produce reformatted code — breaks PR diffs, annoys users. | High | Medium | Mandatory `Rewrite_PreservesTrivia` test per rewriter (called out in #195/#202 test plans). Code review checklist: every `node.With*` call must have a corresponding trivia source. |
| 3 | **Ambiguous rewriter matches producing false positives.** Syntax-only matching can't tell two `Color` properties apart if both types are imported. A bad rewrite means broken code with no easy fingerprint. | High | High | "Never apply an uncertain rewrite" principle (#195 spec). Default to `SkippedRewrite` on ambiguity. Every per-rewriter test must include at least one ambiguous-case scenario. `migrate status` surfaces skips prominently so users see them. |
| 4 | **State file schema evolution.** v1 state file gets a new field in v2; reading v1 files into v2 deserializer either crashes or silently loses data. | Medium | Medium | Treat state schema as versioned via the `schema` field on `MigrationState` (mirror the migration-schema's convention). Future field additions are additive only; reads tolerate unknown fields. Reserve a migration step for schema-version bumps (out of scope for v1). |
| 5 | **Coverage gate failures from skeleton code.** #194 lands a scaffold with low coverage unless tests land in the same PR. CI blocks merge. | Medium | Low | Scaffold + contract tests ship together (called out in #194). Don't split the scaffold across PRs. |
| 6 | **Stale epic scope creep.** Re-opening #1/#155/#163 to add "one more thing" delays the engine ship. | Medium | Medium | Close epics with the narrow-scope comments (§3 above). Any new work files a fresh issue. |
| 7 | **CI flakiness on examples.** The Serilog v2→v3 E2E test in #203 depends on NuGet availability or pinned dlls. Flake means red CI, blocked PRs. | Medium | Medium | Cache the v2 / v3 packages in `WrapGod.Tests/fixtures/migration/` (binary fixtures, ~200KB each). Tag the live-NuGet test `[Trait("Category", "Network")]` and exclude from PR CI; run only on nightly. The parity test uses the cached fixtures so it's deterministic. |
| 8 | **Codebase-derived risk: `MigrateInitCommand` already wraps `init` inside its own `migrate` command** (`WrapGod.Cli/MigrateInitCommand.cs:36`). Adding `generate`/`apply`/`status`/`verify` as siblings requires refactoring this. If done sloppily, the existing `init` command breaks (tested in `CliCommandTests.RootCommand_WiresExpectedCommands`). | High | Low | #198's "Files to create/modify" calls this out explicitly: extract `init` subcommand from `MigrateInitCommand`, build the `migrate` parent in a new `MigrateCommandBuilder.cs`. Update `CliCommandTests` in the same PR. Run `CliCommandTests` before pushing. |

---

## Section 6 — Definition of Done (overall)

The entire plan is "done" when:

- [ ] All 17 open issues closed via `gh issue close --reason completed`:
    - #1, #53, #155, #163, #191 (epics) — via the comments drafted in §3
    - #193, #194, #195, #196, #197, #198, #199, #200, #201, #202, #203, #204 (implementation)
- [ ] `gh run list --repo JerrettDavis/WrapGod --branch main --limit 5` shows green for the last 5 runs on `main`.
- [ ] All feature branches merged to `main`; `main` is green at the tip.
- [ ] README and docs (`docs/migration/*`, `docs/guide/cli.md`, `docs/CLI.md`) reflect every `migrate` command and rule kind.
- [ ] `examples/migrations/serilog-v2-to-v3/` exists and its parity test passes in CI.
- [ ] All other examples (`examples/migrations/*-bidirectional/`, `examples/migrations/nuget-version-matrix/*`) continue to build and pass their CI workflows (no regression).
- [ ] The 90% coverage gate is green across every package in cobertura output, including the two new projects.
- [ ] A release tag (`v0.2-alpha`) is created summarising the migration engine ship.

---

*End of plan.*
