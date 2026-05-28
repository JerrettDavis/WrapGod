using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters;

/// <summary>
/// Rewrites member accesses where the member name matches
/// <see cref="RenameMemberRule.OldMemberName"/> and the receiver can be heuristically
/// resolved to the declaring <see cref="RenameMemberRule.TypeName"/>. When the receiver
/// type cannot be determined, records a <see cref="SkippedRewrite"/> and leaves the node
/// unchanged.
/// </summary>
internal sealed class RenameMemberRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "renameMember";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not RenameMemberRule typed)
            return null;

        var walker = new RenameMemberWalker(typed, ctx, node);
        var result = walker.Visit(node);

        // Return the rewritten tree only if at least one successful rewrite happened.
        // (Skips-only still return null for the tree — the context already records the skips.)
        return walker.AppliedCount > 0 ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class RenameMemberWalker : CSharpSyntaxRewriter
    {
        private readonly RenameMemberRule _rule;
        private readonly RewriteContext _ctx;
        private readonly SyntaxNode _root;
        private readonly string _declTypeShort;
        internal int AppliedCount;

        internal RenameMemberWalker(
            RenameMemberRule rule,
            RewriteContext ctx,
            SyntaxNode root)
        {
            _rule = rule;
            _ctx = ctx;
            _root = root;
            _declTypeShort = RewriterHelpers.ShortName(rule.TypeName);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // Recurse first so that nested member accesses are handled bottom-up
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;

            if (visited.Name.Identifier.ValueText != _rule.OldMemberName)
                return visited;

            // Attempt to infer the receiver type using syntax-only heuristics
            var inferredType = RewriterHelpers.TryInferReceiverTypeName(visited, _root);

            if (inferredType is null)
            {
                // Cannot determine whether the receiver is the declaring type — skip
                _ctx.RecordSkipped(
                    _rule,
                    visited.Span,
                    line: RewriterHelpers.LineOf(visited),
                    reason: $"ambiguous: cannot determine receiver type for '{visited.Expression}' " +
                            $"(expected '{_declTypeShort}') — manual review required");
                return visited;
            }

            if (!string.Equals(inferredType, _declTypeShort, StringComparison.Ordinal))
            {
                // Receiver type is known but does NOT match the declaring type — skip
                _ctx.RecordSkipped(
                    _rule,
                    visited.Span,
                    line: RewriterHelpers.LineOf(visited),
                    reason: $"ambiguous: receiver type '{inferredType}' does not match " +
                            $"declaring type '{_declTypeShort}'");
                return visited;
            }

            // Replace the member name while preserving trivia
            var oldNameToken = visited.Name.Identifier;
            var newNameToken = RewriterHelpers.IdentifierWithTrivia(oldNameToken, _rule.NewMemberName);
            var newName = visited.Name.WithIdentifier(newNameToken);
            var replacement = visited.WithName(newName);

            _ctx.RecordApplied(
                _rule,
                visited.Name.Span,
                originalText: _rule.OldMemberName,
                replacementText: _rule.NewMemberName,
                line: RewriterHelpers.LineOf(visited.Name));

            AppliedCount++;
            return replacement;
        }
    }
}
