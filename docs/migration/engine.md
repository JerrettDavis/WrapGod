# Migration Engine

`WrapGod.Migration.Engine` is the Roslyn-backed rewrite pipeline that consumes a
`MigrationSchema` (authored or generated via `WrapGod.Migration`) and applies its rules
against C# source files. This page documents the scaffold contracts that ship in
issue #194; the concrete rewriters and the orchestrator that wire everything together
arrive in later issues (see "What's next" below).

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

## Rewriters shipping in #195

Seven concrete `IRuleRewriter` implementations are now available under
`WrapGod.Migration.Engine.Rewriters`. See [A-Level Rewriters](rewriters.md) for the
full catalogue, per-rewriter contracts, and before/after examples.

| Class | `Kind` | Rule type |
|---|---|---|
| `RenameTypeRewriter` | `renameType` | `RenameTypeRule` |
| `RenameNamespaceRewriter` | `renameNamespace` | `RenameNamespaceRule` |
| `RenameMemberRewriter` | `renameMember` | `RenameMemberRule` |
| `ChangeParameterRewriter` | `changeParameter` | `ChangeParameterRule` |
| `RemoveMemberRewriter` | `removeMember` | `RemoveMemberRule` |
| `AddRequiredParameterRewriter` | `addRequiredParameter` | `AddRequiredParameterRule` |
| `ChangeTypeReferenceRewriter` | `changeTypeReference` | `ChangeTypeReferenceRule` |

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

    // Convenience: pre-loads all 7 A-level rewriters.
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

## What's next

- **#202** — B-level structural rewriters (`SplitMethod`,
  `ExtractParameterObject`, `PropertyToMethod`, `MoveMember`).

## State-tracking (idempotent re-runs)

`StatefulMigrationEngine` (namespace `WrapGod.Migration.Engine`) wraps the
base engine with persistent state so that `apply` runs are idempotent. The
state is stored in a sibling file next to the schema (e.g.
`schema.json.state.json`). See [Migration State](state.md) for the full
specification, hash semantics, and API reference.
