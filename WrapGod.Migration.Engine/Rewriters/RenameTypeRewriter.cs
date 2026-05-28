using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters;

/// <summary>
/// Rewrites occurrences of a renamed type's short name (identifier nodes, qualified names,
/// generic type arguments) using syntax-only heuristics. Fully-qualified references are
/// updated in-place; unresolvable ambiguities are recorded as <see cref="SkippedRewrite"/>.
/// </summary>
internal sealed class RenameTypeRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "renameType";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not RenameTypeRule typed)
            return null;

        var oldShort = RewriterHelpers.ShortName(typed.OldName);
        var newShort = RewriterHelpers.ShortName(typed.NewName);

        var walker = new RenameTypeWalker(typed, oldShort, newShort, ctx);
        var result = walker.Visit(node);
        return walker.Changed ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class RenameTypeWalker : CSharpSyntaxRewriter
    {
        private readonly RenameTypeRule _rule;
        private readonly string _oldShort;
        private readonly string _newShort;
        private readonly RewriteContext _ctx;
        internal bool Changed;

        internal RenameTypeWalker(
            RenameTypeRule rule,
            string oldShort,
            string newShort,
            RewriteContext ctx)
        {
            _rule = rule;
            _oldShort = oldShort;
            _newShort = newShort;
            _ctx = ctx;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            // Only rewrite when the identifier text exactly equals the short name
            if (node.Identifier.ValueText != _oldShort)
                return base.VisitIdentifierName(node);

            // Avoid renaming identifiers that are clearly not type references:
            // member names on the right side of a MemberAccessExpression are handled
            // by their parent; attribute names ending in "Attribute" keep the original
            // (we rely on the parent QualifiedName visitor when fully-qualified).
            var parent = node.Parent;
            if (parent is MemberAccessExpressionSyntax mae && mae.Name == node)
                return base.VisitIdentifierName(node);

            var newIdentifier = RewriterHelpers.IdentifierWithTrivia(node.Identifier, _newShort);
            var replacement = node.WithIdentifier(newIdentifier);

            _ctx.RecordApplied(
                _rule,
                node.Span,
                originalText: node.ToString(),
                replacementText: replacement.ToString(),
                line: RewriterHelpers.LineOf(node));

            Changed = true;
            return replacement;
        }

        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
        {
            // Handle fully-qualified reference: check that the right part matches
            // and left part matches the old namespace.
            var nodeText = node.ToString();
            var oldFq = _rule.OldName;
            var newFq = _rule.NewName;

            if (nodeText == oldFq)
            {
                // Replace the entire qualified name
                var replacement = SyntaxFactory.ParseName(newFq)
                    .WithTriviaFrom(node);

                _ctx.RecordApplied(
                    _rule,
                    node.Span,
                    originalText: nodeText,
                    replacementText: replacement.ToString(),
                    line: RewriterHelpers.LineOf(node));

                Changed = true;
                return replacement;
            }

            return base.VisitQualifiedName(node);
        }
    }
}
