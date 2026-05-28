using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters;

/// <summary>
/// Handles <see cref="RemoveMemberRule"/>. At every call site for the removed member,
/// the expression statement is removed entirely and its location is annotated with a
/// migration comment attached as TRIVIA to a sibling token. This avoids leaving an
/// orphan <c>;</c> in the rewritten code and is safe against formatters. Records each
/// removal as an <see cref="AppliedRewrite"/>.
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

        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            // First recurse so nested blocks are processed bottom-up.
            var visitedBlock = (BlockSyntax)base.VisitBlock(node)!;

            // Walk statements; if a statement matches the target call, remove it and attach
            // the migration comment as trivia to the next sibling (or the closing brace).
            var newStatements = new List<StatementSyntax>(visitedBlock.Statements.Count);
            // Pending comments to be prepended to the next emitted token.
            var pendingTrivia = SyntaxTriviaList.Empty;

            foreach (var stmt in visitedBlock.Statements)
            {
                if (TryMatchTargetStatement(stmt, out var commentText, out var originalText))
                {
                    // Record this as an applied rewrite — the line is the original statement's line.
                    _ctx.RecordApplied(
                        _rule,
                        stmt.Span,
                        originalText: originalText,
                        replacementText: commentText,
                        line: RewriterHelpers.LineOf(stmt));

                    Changed = true;

                    // Build the trivia: preserve the statement's leading trivia (indentation,
                    // blank lines) then insert the comment then a newline.
                    var commentTrivia = SyntaxFactory.Comment(commentText);
                    var newline = SyntaxFactory.CarriageReturnLineFeed;

                    pendingTrivia = pendingTrivia
                        .AddRange(stmt.GetLeadingTrivia())
                        .Add(commentTrivia)
                        .Add(newline);

                    // Drop the statement (do not add to newStatements).
                    continue;
                }

                // Not a target — attach any pending trivia as leading trivia to this stmt.
                if (pendingTrivia.Count > 0)
                {
                    var combined = pendingTrivia.AddRange(stmt.GetLeadingTrivia());
                    newStatements.Add(stmt.WithLeadingTrivia(combined));
                    pendingTrivia = SyntaxTriviaList.Empty;
                }
                else
                {
                    newStatements.Add(stmt);
                }
            }

            // If we still have pending trivia after iterating all statements, attach it as
            // leading trivia to the closing brace token. This handles the case where the
            // removed call was the last (or only) statement in the block.
            var closeBrace = visitedBlock.CloseBraceToken;
            if (pendingTrivia.Count > 0)
            {
                var combined = pendingTrivia.AddRange(closeBrace.LeadingTrivia);
                closeBrace = closeBrace.WithLeadingTrivia(combined);
            }

            if (!Changed)
                return visitedBlock;

            return visitedBlock
                .WithStatements(SyntaxFactory.List(newStatements))
                .WithCloseBraceToken(closeBrace);
        }

        private bool TryMatchTargetStatement(
            StatementSyntax stmt,
            out string commentText,
            out string originalText)
        {
            commentText = string.Empty;
            originalText = string.Empty;

            if (stmt is not ExpressionStatementSyntax exprStmt)
                return false;

            if (!IsTargetCall(exprStmt.Expression))
                return false;

            originalText = exprStmt.ToString().Trim();
            var noteText = string.IsNullOrEmpty(_rule.Note) ? string.Empty : $" — {_rule.Note}";
            commentText = $"// MIGRATION: {_rule.Id} removed{noteText}: {originalText}";
            return true;
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
