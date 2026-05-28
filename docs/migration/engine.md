# Migration Engine

`WrapGod.Migration.Engine` is the Roslyn-backed rewrite pipeline that consumes a
`MigrationSchema` (authored or generated via `WrapGod.Migration`) and applies its rules
against C# source files.

## Overview

The migration engine automates the mechanical part of upgrading a codebase from one version of a library to another. Given a [migration schema](schema.md) that describes the breaking changes (renames, moves, removals, restructurings), the engine:

1. Walks every `.cs` file in your project
2. Parses each file into a Roslyn syntax tree (no compilation needed)
3. Applies each applicable rule through its dedicated rewriter
4. Preserves all whitespace and comments
5. Injects any missing `using` directives for new namespaces
6. Writes the result back to disk atomically

The engine is purely syntactic — it never requires a compilable project or a `SemanticModel`. It handles broken code gracefully, which matters when part of your upgrade is fixing the very errors the migration introduces.

### When to use the migration engine

| Scenario | Recommendation |
|----------|---------------|
| Upgrading a NuGet package that shipped a migration schema | Use `migrate apply` directly |
| Upgrading a package with no schema available | Use `migrate generate` to draft a schema first |
| Bulk-renaming types across a large codebase after an internal refactor | Author a schema manually; run `migrate apply` |
| Complex structural changes (method splits, parameter objects) | Author B-level rules; run `migrate apply` with `confidence: "manual"` first |
| Programmatic integration (CI, build scripts) | Use the `MigrationEngine` or `StatefulMigrationEngine` API directly |

## Architecture Diagram

```
 ┌─────────────────────────────────────────────────────────────────────────┐
 │  User inputs                                                             │
 │  ─────────                                                               │
 │  MigrationSchema JSON           Source files (.cs)                       │
 └──────────┬──────────────────────────────────┬───────────────────────────┘
            │                                  │
            ▼                                  ▼
 ┌─────────────────────┐          ┌────────────────────────┐
 │ MigrationSchema     │          │ IMigrationFileSystem   │
 │ (deserialized)      │          │ ReadAllText()          │
 └──────────┬──────────┘          └──────────┬─────────────┘
            │                               │
            ▼                               ▼
 ┌─────────────────────────────────────────────────────────┐
 │                     MigrationEngine                      │
 │  ┌───────────────────────────────────────────────────┐  │
 │  │  Per-file loop (deduplicated paths)               │  │
 │  │  ┌─────────────────────────────────────────────┐  │  │
 │  │  │  CSharpSyntaxTree.ParseText()               │  │  │
 │  │  │         │                                   │  │  │
 │  │  │  Manual-rule scan → MatchedFiles            │  │  │
 │  │  │         │                                   │  │  │
 │  │  │  Auto-rule chain (sequential):              │  │  │
 │  │  │    rule1 → IRuleRewriter.TryRewrite()       │  │  │
 │  │  │    rule2 → IRuleRewriter.TryRewrite()       │  │  │
 │  │  │    ...                                      │  │  │
 │  │  │         │                                   │  │  │
 │  │  │  Using injection post-pass                  │  │  │
 │  │  │         │                                   │  │  │
 │  │  │  WriteAllTextAtomic() (Apply mode only)     │  │  │
 │  │  └─────────────────────────────────────────────┘  │  │
 │  └───────────────────────────────────────────────────┘  │
 │                          │                               │
 │            MigrationResult aggregate                     │
 └──────────────────────────┬──────────────────────────────┘
                            │
            ┌───────────────┼───────────────┐
            ▼               ▼               ▼
         Applied[]       Skipped[]       Manual[]
```

## Full Lifecycle

For each file in the deduplicated `filePaths` set:

1. **Read** — `IMigrationFileSystem.ReadAllText(filePath)`. `IOException` → records a `SkippedRewrite` with `RuleId = "<io>"` and continues to the next file.

2. **Parse** — `CSharpSyntaxTree.ParseText(text)`. Syntax-only; no project references or compilation required. Broken code is accepted.

3. **Manual scan** — For every rule with `Confidence = Manual`, the matching `IRuleRewriter` performs a read-only tree-walk to identify whether the file contains matching patterns. Matches populate `ManualRewrite.MatchedFiles`. Nothing is written.

4. **Auto-rule chain** — For every rule with `Confidence = Auto` or `Verified`, in schema order:
   - Lookup the `IRuleRewriter` whose `Kind` matches `camelCase(rule.Kind)`.
   - If no rewriter is registered → record `SkippedRewrite("no rewriter for kind '…'")`.
   - If found → call `TryRewrite(currentRoot, rule, ctx)`. The returned node replaces `currentRoot` for the next rule in the chain.
   - This sequential strategy lets each rule see the output of previous rules (e.g., `Foo → Bar` then `Bar → Baz` in two passes).

5. **Using injection** — After all rules run, inspect the final `CompilationUnitSyntax`. For any namespace introduced by an applied `ChangeTypeReferenceRule`, `RenameNamespaceRule`, or `RenameTypeRule` that is not already present in the file, inject a `using` directive at the top.

6. **Write** — In `Apply` mode (not dry-run), write the modified file atomically: write to `{path}.tmp`, then `File.Move(tmp, path, overwrite: true)`. In `DryRun` mode, skip the write.

After all files: aggregate `Applied`, `Skipped`, `Manual`, and `RewrittenFiles` into a `MigrationResult`.

---

## Public contracts

The project (`net10.0`, depends on `Microsoft.CodeAnalysis.CSharp`) exposes the following
types under the `WrapGod.Migration.Engine` namespace:

- **`IRuleRewriter`** — interface implemented by every concrete rewriter. `Kind`
  identifies the `MigrationRuleKind` (camelCase) the rewriter handles; `TryRewrite`
  returns a rewritten `SyntaxNode` with trivia preserved, or `null` when the rule does
  not match.
- **`RewriteContext`** — per-file context that records the audit trail. Exposes the
  source `FilePath`, accumulates `Applied` and `Skipped` collections (externally
  immutable via `ReadOnlyCollection<T>`), and offers `RecordApplied`/`RecordSkipped`
  for rewriters to log outcomes.
- **`AppliedRewrite`** — sealed positional record describing a successful rewrite
  (`RuleId`, `File`, `Line`, `OriginalText`, `ReplacedWith`); value-equal across
  instances.
- **`SkippedRewrite`** — sealed positional record describing a rewrite that was
  evaluated but not applied (`RuleId`, `File`, `Line`, `Reason`).
- **`ManualRewrite`** — sealed positional record for `Manual`-confidence rules that
  require human intervention (`RuleId`, `Note`, `MatchedFiles`).
- **`MigrationResult`** — sealed aggregator class with `Applied`, `Skipped`, `Manual`,
  `RewrittenFiles`, and `DryRun` properties. Provides aggregate `*Count` properties
  and a static `Empty` factory.
- **`TriviaPreservation`** — extension helper exposing `WithReplacedToken<T>`, which
  replaces a `SyntaxToken` while copying the leading and trailing trivia from the
  original token onto the replacement.

## The `IRuleRewriter` contract

```csharp
using Microsoft.CodeAnalysis;
using WrapGod.Migration;
using WrapGod.Migration.Engine;

public interface IRuleRewriter
{
    /// <summary>The MigrationRuleKind discriminator this rewriter handles (camelCase).</summary>
    string Kind { get; }

    /// <summary>
    /// Returns a rewritten node (with trivia preserved via WithTriviaFrom) when the rule
    /// applies, or null to leave the node unchanged.
    /// </summary>
    SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx);
}
```

### Example implementation skeleton

```csharp
public sealed class MyRenameRewriter : IRuleRewriter
{
    public string Kind => "renameType";

    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not RenameTypeRule typed)
            return null;

        if (node is not IdentifierNameSyntax id || id.Identifier.ValueText != typed.OldName)
            return null;

        var replacement = SyntaxFactory.IdentifierName(typed.NewName);
        // Trivia contract — every replacement MUST preserve leading/trailing trivia.
        var rewritten = replacement.WithTriviaFrom(id);

        ctx.RecordApplied(
            rule,
            id.Span,
            originalText: id.ToString(),
            replacementText: rewritten.ToString(),
            line: id.GetLocation().GetLineSpan().StartLinePosition.Line + 1);

        return rewritten;
    }
}
```

### Contract rules

- **Trivia must be preserved.** Every rewritten node MUST copy trivia from the
  original via `WithTriviaFrom` (or the token-level `TriviaPreservation.WithReplacedToken`).
  Skipping this corrupts whitespace and comments in the output.
- **Return `null` when the rule does not match.** Never throw, never return a
  partially-modified node. Ambiguous matches should record a `SkippedRewrite` and
  return `null`.
- **No semantic lookup.** The engine works on syntax only so it can operate on
  broken code; rewriters must not require a `SemanticModel`.

## Rewriters

Eleven concrete `IRuleRewriter` implementations are available. See [Rewriters](rewriters.md)
for the full catalogue, per-rewriter contracts, and before/after examples.

### A-level (#195)

| Class | `Kind` | Rule type |
|---|---|---|
| `RenameTypeRewriter` | `renameType` | `RenameTypeRule` |
| `RenameNamespaceRewriter` | `renameNamespace` | `RenameNamespaceRule` |
| `RenameMemberRewriter` | `renameMember` | `RenameMemberRule` |
| `ChangeParameterRewriter` | `changeParameter` | `ChangeParameterRule` |
| `RemoveMemberRewriter` | `removeMember` | `RemoveMemberRule` |
| `AddRequiredParameterRewriter` | `addRequiredParameter` | `AddRequiredParameterRule` |
| `ChangeTypeReferenceRewriter` | `changeTypeReference` | `ChangeTypeReferenceRule` |

### B-level structural (#202)

| Class | `Kind` | Rule type |
|---|---|---|
| `SplitMethodRewriter` | `splitMethod` | `SplitMethodRule` |
| `ExtractParameterObjectRewriter` | `extractParameterObject` | `ExtractParameterObjectRule` |
| `PropertyToMethodRewriter` | `propertyToMethod` | `PropertyToMethodRule` |
| `MoveMemberRewriter` | `moveMember` | `MoveMemberRule` |

## Orchestrator — `MigrationEngine` (#196)

`MigrationEngine` is the top-level class that connects a `MigrationSchema` to a set of
source files and drives the rewrite pipeline from start to finish.

### Public API

```csharp
namespace WrapGod.Migration.Engine;

public sealed class MigrationEngine
{
    // Primary constructors
    public MigrationEngine(IEnumerable<IRuleRewriter> rewriters);

    // Convenience: pre-loads all 11 rewriters (7 A-level + 4 B-level).
    public static MigrationEngine CreateDefault();

    // Apply all auto rules to files, write results to disk.
    public MigrationResult Apply(MigrationSchema schema, IEnumerable<string> filePaths);

    // Same pipeline, no disk writes; RewrittenFiles is still populated.
    public MigrationResult DryRun(MigrationSchema schema, IEnumerable<string> filePaths);
}
```

### Lifecycle diagram

```
foreach file in filePaths (deduplicated):
  1. Read source text via IMigrationFileSystem.ReadAllText()
     └─ IOException → record SkippedRewrite("<io>", …) and continue
  2. CSharpSyntaxTree.ParseText() — syntax-only, no compilation
  3. Manual rules: scan file with matching rewriter, collect MatchedFiles
  4. Auto rules (schema order, Option A — sequential chain):
     for each rule:
       - lookup IRuleRewriter by camelCase(rule.Kind)
       - missing → SkippedRewrite("no rewriter for kind '…'", …)
       - found    → TryRewrite(currentRoot, rule, ctx) → update currentRoot
  5. Using injection: add missing using directives for introduced namespaces
  6. If modified (Apply mode): IMigrationFileSystem.WriteAllTextAtomic()
Aggregate Applied, Skipped, Manual into MigrationResult
```

### Composition strategy

**Option A — sequential chain** was chosen over Option B (single-pass dispatcher)
because:

- Every existing `IRuleRewriter` already encapsulates its own `CSharpSyntaxRewriter`
  tree-walk; there is nothing to refactor.
- Sequential chaining lets each rule see the output of the previous rule, enabling
  transformation chains such as `Foo → Bar` then `Bar → Baz`.
- The perf budget (&lt;5 s for 1 000 files) is met with room to spare
  (~620 ms measured in CI on this machine).
- Option B can be introduced incrementally if profiling identifies a bottleneck.

Each (file, rule) pair performs one tree-walk. For a schema with `N` rules and `F` files:
total walks = `N × F`.

### File I/O abstraction (`IMigrationFileSystem`)

The internal `IMigrationFileSystem` interface is injected for testability via the
`internal MigrationEngine(IEnumerable<IRuleRewriter>, IMigrationFileSystem)` constructor
(exposed to `WrapGod.Tests` via `InternalsVisibleTo`).

The default implementation, `RealFileSystem`, uses `System.IO.File.ReadAllText` and
an atomic write: write to `path + ".tmp"`, then `File.Move(tmp, path, overwrite: true)`.

### Cross-namespace using injection

After all per-file rewrites complete, the orchestrator inspects the final
`CompilationUnitSyntax` and adds `using` directives for any namespace introduced by a
`ChangeTypeReferenceRule`, `RenameNamespaceRule`, or `RenameTypeRule` that was actually
applied and whose namespace is not already present. This closes the #195 deferred
should-fix.

### Performance

| Scenario | Result (Release) |
|---|---|
| 1 000 synthetic files, 5 rules | ~620 ms |
| Per-file overhead | ~0.6 ms |
| Budget (hard gate) | 10 s |
| Target | 5 s |

### Manual-confidence rules

Rules with `Confidence = RuleConfidence.Manual` are never applied automatically.
The engine runs a syntax-only "would this rule match?" scan and populates
`MigrationResult.Manual[].MatchedFiles` with every file where the pattern was
detected. The `Applied` list contains no entries for manual rules.

## Extension Points

### Writing a Custom `IRuleRewriter`

If the eleven built-in rewriters do not cover your transformation, you can implement `IRuleRewriter` yourself:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;
using WrapGod.Migration.Engine;

public sealed class MyCustomRewriter : IRuleRewriter
{
    // Must match the camelCase "kind" string in your schema JSON.
    public string Kind => "myCustomKind";

    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        // Cast to your rule type (define it in WrapGod.Migration or a sibling project).
        if (rule is not MyCustomRule typed)
            return null;

        // Only handle the node type you care about.
        if (node is not SomeSpecificSyntaxNode target)
            return null;

        // Perform the transformation.
        var replacement = BuildReplacement(target, typed);

        // REQUIRED: preserve trivia so whitespace and comments are not corrupted.
        replacement = replacement.WithTriviaFrom(target);

        // Record the outcome.
        ctx.RecordApplied(
            rule,
            target.Span,
            originalText: target.ToString(),
            replacementText: replacement.ToString(),
            line: target.GetLocation().GetLineSpan().StartLinePosition.Line + 1);

        return replacement;
    }
}
```

Key obligations:

| Obligation | Why |
|-----------|-----|
| Preserve trivia via `WithTriviaFrom` | Corrupting trivia mangling whitespace or stripping comments |
| Return `null` when no match | The orchestrator uses `null` to skip file writes when no rule matched |
| Record `SkippedRewrite` for ambiguous matches | Ambiguous matches should be visible to the author — don't silently skip |
| No `SemanticModel` access | The engine never has a compilation; rewriters must be syntax-only |
| Never throw | Exceptions propagate to the orchestrator and abort the entire file |

### Registering a Custom Rewriter

Inject your rewriter into a `MigrationEngine` instead of using `CreateDefault()`:

```csharp
var engine = new MigrationEngine(
    MigrationEngine.CreateDefault().Rewriters
        .Append(new MyCustomRewriter()));
```

Or build from scratch if you only need a subset of rewriters:

```csharp
var engine = new MigrationEngine([
    new RenameTypeRewriter(),
    new RenameMemberRewriter(),
    new MyCustomRewriter(),
]);
```

### Injecting a Custom File System

For testing or virtual file system scenarios, pass an `IMigrationFileSystem` via the internal constructor (exposed to `WrapGod.Tests` via `InternalsVisibleTo`):

```csharp
// In test code
var engine = new MigrationEngine(
    rewriters: [new RenameTypeRewriter()],
    fileSystem: new InMemoryFileSystem(files));
```

`IMigrationFileSystem` exposes `ReadAllText(path)` and `WriteAllTextAtomic(path, content)`. The default `RealFileSystem` implementation uses `System.IO.File` with atomic writes.

## State-tracking (idempotent re-runs)

`StatefulMigrationEngine` (namespace `WrapGod.Migration.Engine`) wraps the
base engine with persistent state so that `apply` runs are idempotent. The
state is stored in a sibling file next to the schema (e.g.
`schema.json.state.json`). See [Migration State](state.md) for the full
specification, hash semantics, and API reference.

## See Also

- [Migration Schema](schema.md) — schema format and all rule kinds
- [Authoring a Migration Schema](authoring.md) — guide for library maintainers
- [Rewriters](rewriters.md) — per-rewriter documentation with before/after examples
- [Migration State](state.md) — idempotency, state file format, hash semantics
- [Applying Migrations](applying.md) — consumer workflow
- [Back to Migration index](./index.md)
