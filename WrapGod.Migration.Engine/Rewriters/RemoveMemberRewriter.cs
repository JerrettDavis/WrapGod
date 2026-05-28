using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters;

/// <summary>
/// Handles <see cref="RemoveMemberRule"/>. At every call site for the removed member,
/// the entire expression statement is replaced with a leading trivia comment that
/// preserves the original text so developers can see what was there and manually restore
/// or remove the call. Records each replacement as an <see cref="AppliedRewrite"/>.
/// </summary>
internal sealed class RemoveMemberRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "removeMember";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not RemoveMemberRule typed)
            return null;

        var walker = new RemoveMemberWalker(typed, ctx);
        var result = walker.Visit(node);
        return walker.Changed ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class RemoveMemberWalker : CSharpSyntaxRewriter
    {
        private readonly RemoveMemberRule _rule;
        private readonly RewriteContext _ctx;
        internal bool Changed;

        internal RemoveMemberWalker(RemoveMemberRule rule, RewriteContext ctx)
        {
            _rule = rule;
            _ctx = ctx;
        }

        public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (!IsTargetCall(node.Expression))
                return base.VisitExpressionStatement(node);

            var originalText = node.ToString().Trim();
            var noteText = string.IsNullOrEmpty(_rule.Note) ? string.Empty : $" — {_rule.Note}";

            // Build a comment that replaces the statement
            var commentText = $"// MIGRATION: {_rule.Id} removed{noteText}: {originalText}";

            // Preserve the leading trivia (indentation, blank lines) from the original node
            var leadingTrivia = node.GetLeadingTrivia();
            var trailingTrivia = node.GetTrailingTrivia();

            var commentTrivia = SyntaxFactory.Comment(commentText);
            var newTrivia = leadingTrivia
                .Add(commentTrivia)
                .AddRange(trailingTrivia);

            // Replace the statement with an empty statement that carries the comment as
            // leading trivia, then is itself invisible (just a semicolon with no real effect).
            // Alternatively, produce a comment-only trivia on an empty statement.
            // We emit a single-line comment on a trivial empty statement node.
            var placeholder = SyntaxFactory.EmptyStatement(
                    SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .WithLeadingTrivia(newTrivia)
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            _ctx.RecordApplied(
                _rule,
                node.Span,
                originalText: originalText,
                replacementText: commentText,
                line: RewriterHelpers.LineOf(node));

            Changed = true;
            return placeholder;
        }

        private bool IsTargetCall(ExpressionSyntax expr)
        {
            // Match: receiver.MemberName(...)
            if (expr is InvocationExpressionSyntax inv &&
                inv.Expression is MemberAccessExpressionSyntax mae)
            {
                return mae.Name.Identifier.ValueText == _rule.MemberName;
            }

            return false;
        }
    }
}
