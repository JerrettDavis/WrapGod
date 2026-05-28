using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters;

/// <summary>
/// Rewrites <c>using</c> directives and qualified names when a namespace is renamed.
/// Only the prefix matching <see cref="RenameNamespaceRule.OldNamespace"/> is changed;
/// no other identifiers are touched.
/// </summary>
internal sealed class RenameNamespaceRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "renameNamespace";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not RenameNamespaceRule typed)
            return null;

        var walker = new RenameNamespaceWalker(typed, ctx);
        var result = walker.Visit(node);
        return walker.Changed ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class RenameNamespaceWalker : CSharpSyntaxRewriter
    {
        private readonly RenameNamespaceRule _rule;
        private readonly RewriteContext _ctx;
        internal bool Changed;

        internal RenameNamespaceWalker(RenameNamespaceRule rule, RewriteContext ctx)
        {
            _rule = rule;
            _ctx = ctx;
        }

        public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
        {
            var usingText = node.Name?.ToString() ?? string.Empty;

            if (!StartsWithNamespace(usingText, _rule.OldNamespace))
                return base.VisitUsingDirective(node);

            var newText = _rule.NewNamespace + usingText[_rule.OldNamespace.Length..];
            var newName = SyntaxFactory.ParseName(newText)
                .WithTriviaFrom(node.Name!);

            var replacement = node.WithName(newName);

            _ctx.RecordApplied(
                _rule,
                node.Span,
                originalText: usingText,
                replacementText: newText,
                line: RewriterHelpers.LineOf(node));

            Changed = true;
            return replacement;
        }

        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
        {
            var nodeText = node.ToString();

            if (!StartsWithNamespace(nodeText, _rule.OldNamespace))
                return base.VisitQualifiedName(node);

            var newText = _rule.NewNamespace + nodeText[_rule.OldNamespace.Length..];
            var replacement = SyntaxFactory.ParseName(newText)
                .WithTriviaFrom(node);

            _ctx.RecordApplied(
                _rule,
                node.Span,
                originalText: nodeText,
                replacementText: newText,
                line: RewriterHelpers.LineOf(node));

            Changed = true;
            return replacement;
        }

        /// <summary>
        /// Returns true when <paramref name="nameText"/> equals or starts with
        /// <paramref name="ns"/> followed by a dot (to avoid matching a prefix of a
        /// longer namespace accidentally).
        /// </summary>
        private static bool StartsWithNamespace(string nameText, string ns) =>
            nameText == ns ||
            nameText.StartsWith(ns + ".", StringComparison.Ordinal);
    }
}
