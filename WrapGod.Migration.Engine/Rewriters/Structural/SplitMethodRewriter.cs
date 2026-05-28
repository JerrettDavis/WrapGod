using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters.Structural;

/// <summary>
/// Handles <see cref="SplitMethodRule"/>. At each statement-context call site, comments
/// out the original call and inserts the replacement calls on adjacent lines.
/// When the return value is consumed (e.g., <c>var x = Foo();</c>) or the call is in a
/// chained expression context, records a <see cref="SkippedRewrite"/> and leaves the node
/// unchanged — splitting a value-producing call cannot be done safely by a syntax-only rewriter.
/// </summary>
internal sealed class SplitMethodRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "splitMethod";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not SplitMethodRule typed)
            return null;

        var walker = new SplitMethodWalker(typed, ctx);
        var result = walker.Visit(node);
        return walker.Changed ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class SplitMethodWalker : CSharpSyntaxRewriter
    {
        private readonly SplitMethodRule _rule;
        private readonly RewriteContext _ctx;
        private readonly string _declTypeShort;
        internal bool Changed;

        internal SplitMethodWalker(SplitMethodRule rule, RewriteContext ctx)
        {
            _rule = rule;
            _ctx = ctx;
            _declTypeShort = RewriterHelpers.ShortName(rule.TypeName);
        }

        /// <summary>
        /// Detects non-statement-context invocations of the target method.
        /// These are cases like chained calls or return value consumption where the
        /// call cannot be safely split, so we emit a <see cref="SkippedRewrite"/>.
        /// </summary>
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Recurse first (bottom-up)
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

            // Check if this invocation is our target method
            if (visited.Expression is not MemberAccessExpressionSyntax mae ||
                mae.Name.Identifier.ValueText != _rule.OldMethodName)
            {
                return visited;
            }

            // Check receiver type
            var inferredType = RewriterHelpers.TryInferReceiverTypeName(mae, visited.SyntaxTree.GetRoot());
            if (inferredType is not null &&
                !string.Equals(inferredType, _declTypeShort, StringComparison.Ordinal))
            {
                return visited;
            }

            // If this invocation IS directly the expression of an ExpressionStatement,
            // the block visitor will handle it. Skip it here to avoid double-recording.
            var parent = visited.Parent;
            if (parent is ExpressionStatementSyntax)
                return visited;

            // It's either chained or in a value-consumed context — emit SkippedRewrite
            var reason = IsChainedCall(visited)
                ? $"split-method '{_rule.OldMethodName}' skipped: chained-call cannot be safely split"
                : $"split-method '{_rule.OldMethodName}' skipped: return value is consumed, manual review required";

            _ctx.RecordSkipped(
                _rule,
                visited.Span,
                line: RewriterHelpers.LineOf(visited),
                reason: reason);

            // Leave the node unchanged
            return visited;
        }

        public override SyntaxNode? VisitBlock(BlockSyntax node)
        {
            // Recurse first so nested blocks are processed bottom-up.
            var visitedBlock = (BlockSyntax)base.VisitBlock(node)!;

            var newStatements = new List<StatementSyntax>(visitedBlock.Statements.Count * 2);
            bool blockChanged = false;

            foreach (var stmt in visitedBlock.Statements)
            {
                if (TryHandleStatement(stmt, out var replacements))
                {
                    newStatements.AddRange(replacements);
                    blockChanged = true;
                    Changed = true;
                    continue;
                }

                newStatements.Add(stmt);
            }

            if (!blockChanged)
                return visitedBlock;

            return visitedBlock.WithStatements(SyntaxFactory.List(newStatements));
        }

        /// <summary>
        /// Attempts to match and transform a single statement. Returns true if the statement
        /// was handled (either split or skipped with comment). <paramref name="replacements"/>
        /// contains the output statements when true.
        /// </summary>
        private bool TryHandleStatement(StatementSyntax stmt, out IReadOnlyList<StatementSyntax> replacements)
        {
            replacements = [];

            // Only handle bare expression statements (statement-context calls)
            if (stmt is not ExpressionStatementSyntax exprStmt)
                return false;

            var expr = exprStmt.Expression;

            // Check for chained call: the invocation's result is accessed further
            if (!IsDirectInvocation(expr, out var inv))
                return false;

            // Verify the member access matches our target
            if (inv.Expression is not MemberAccessExpressionSyntax mae)
                return false;

            if (mae.Name.Identifier.ValueText != _rule.OldMethodName)
                return false;

            // Infer receiver type — use the statement's tree root for heuristic lookup
            var inferredType = RewriterHelpers.TryInferReceiverTypeName(mae, stmt.SyntaxTree.GetRoot());

            if (inferredType is not null &&
                !string.Equals(inferredType, _declTypeShort, StringComparison.Ordinal))
            {
                // Receiver is a different known type — not our target, leave unchanged
                return false;
            }

            // Check for chained calls: parent of the invocation expression statement is
            // not a block-level statement but is used in an expression context
            if (IsChainedCall(inv))
            {
                _ctx.RecordSkipped(
                    _rule,
                    stmt.Span,
                    line: RewriterHelpers.LineOf(stmt),
                    reason: $"split-method '{_rule.OldMethodName}' skipped: chained-call cannot be safely split");
                // Return true with the original statement so we skip adding it again in the loop
                replacements = [stmt];
                return true;
            }

            // All good — build the replacement statements
            var leadingTrivia = stmt.GetLeadingTrivia();
            var trailingTrivia = stmt.GetTrailingTrivia();
            var receiver = mae.Expression;
            var receiverStr = receiver.ToString();
            var originalText = exprStmt.ToString().Trim();

            var result = new List<StatementSyntax>();

            // 1. Commented-out original
            var commentLine = $"// MIGRATION: {_rule.Id} split-method — original: {originalText}";
            var commentStmt = SyntaxFactory.EmptyStatement(
                SyntaxFactory.Token(
                    leading: leadingTrivia
                        .Add(SyntaxFactory.Comment(commentLine))
                        .Add(SyntaxFactory.CarriageReturnLineFeed),
                    kind: SyntaxKind.SemicolonToken,
                    trailing: SyntaxTriviaList.Empty))
                .WithLeadingTrivia(SyntaxTriviaList.Empty);

            // 2. Comment line as leading trivia on first new call
            // Build new call statements for each replacement method
            var newMethodNames = _rule.NewMethodNames;
            for (int i = 0; i < newMethodNames.Count; i++)
            {
                var methodName = newMethodNames[i];
                var newCallText = $"{receiverStr}.{methodName}()";
                var parsedCallExpr = SyntaxFactory.ParseExpression(newCallText);
                var newStmt = SyntaxFactory.ExpressionStatement(parsedCallExpr);

                if (i == 0)
                {
                    // Attach the comment as leading trivia
                    var firstLeading = leadingTrivia
                        .Add(SyntaxFactory.Comment(commentLine))
                        .Add(SyntaxFactory.CarriageReturnLineFeed)
                        .AddRange(leadingTrivia);
                    newStmt = newStmt.WithLeadingTrivia(firstLeading);
                }
                else
                {
                    newStmt = newStmt.WithLeadingTrivia(leadingTrivia);
                }

                if (i == newMethodNames.Count - 1)
                    newStmt = newStmt.WithTrailingTrivia(trailingTrivia);

                result.Add(newStmt);
            }

            _ctx.RecordApplied(
                _rule,
                stmt.Span,
                originalText: originalText,
                replacementText: string.Join("; ", newMethodNames.Select(m => $"{receiverStr}.{m}()")),
                line: RewriterHelpers.LineOf(stmt));

            replacements = result;
            return true;
        }

        /// <summary>
        /// Returns true if <paramref name="expr"/> is a plain invocation expression —
        /// NOT one that is itself the receiver of a further member access (chained call).
        /// </summary>
        private static bool IsDirectInvocation(ExpressionSyntax expr, out InvocationExpressionSyntax inv)
        {
            if (expr is InvocationExpressionSyntax directInv)
            {
                inv = directInv;
                return true;
            }

            inv = null!;
            return false;
        }

        /// <summary>
        /// Returns true when the invocation's result is further accessed (chained call).
        /// E.g., <c>card.Render().ToString()</c> — the inner <c>card.Render()</c> is chained.
        /// </summary>
        private static bool IsChainedCall(InvocationExpressionSyntax inv)
        {
            var parent = inv.Parent;

            // If parent is a MemberAccessExpression where this invocation is the expression
            // (left side), it's a chain: e.g., inv.SomeMethod()
            if (parent is MemberAccessExpressionSyntax mae && mae.Expression == inv)
                return true;

            // If parent is another invocation where this is the expression, it's a chain
            if (parent is InvocationExpressionSyntax parentInv && parentInv.Expression == inv)
                return true;

            return false;
        }
    }
}
