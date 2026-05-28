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

## What's next

- **#196** — `MigrationEngine` orchestrator with `Apply` and `DryRun` entry points.
- **#202** — B-level structural rewriters (`SplitMethod`,
  `ExtractParameterObject`, `PropertyToMethod`, `MoveMember`).
