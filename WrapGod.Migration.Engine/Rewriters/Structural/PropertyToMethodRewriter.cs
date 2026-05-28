using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters.Structural;

/// <summary>
/// Handles <see cref="PropertyToMethodRule"/>. Rewrites:
/// <list type="bullet">
/// <item><description>Read context: <c>obj.OldProp</c> → <c>obj.GetMethodName()</c>
/// (or <c>obj.MethodName()</c> when the method name already starts with Get/Set).</description></item>
/// <item><description>Write context: <c>obj.OldProp = value</c> → <c>obj.SetMethodName(value)</c>
/// (or <c>obj.MethodName(value)</c> when the method name already starts with Get/Set).</description></item>
/// <item><description>Compound-assignment (<c>++</c>, <c>-=</c>, etc.) context: records
/// <see cref="SkippedRewrite"/> — those require manual intervention.</description></item>
/// </list>
/// </summary>
internal sealed class PropertyToMethodRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "propertyToMethod";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not PropertyToMethodRule typed)
            return null;

        var walker = new PropertyToMethodWalker(typed, ctx, node);
        var result = walker.Visit(node);
        return walker.AppliedCount > 0 ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class PropertyToMethodWalker : CSharpSyntaxRewriter
    {
        private readonly PropertyToMethodRule _rule;
        private readonly RewriteContext _ctx;
        private readonly SyntaxNode _root;
        private readonly string _declTypeShort;
        internal int AppliedCount;

        internal PropertyToMethodWalker(
            PropertyToMethodRule rule,
            RewriteContext ctx,
            SyntaxNode root)
        {
            _rule = rule;
            _ctx = ctx;
            _root = root;
            _declTypeShort = RewriterHelpers.ShortName(rule.TypeName);
        }

        /// <summary>
        /// Handles the write context: <c>obj.OldProp = value</c> →
        /// <c>obj.SetMethodName(value)</c>. We intercept the WHOLE assignment here so
        /// that the LHS member-access visit below does NOT also fire.
        /// </summary>
        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            // Recurse right side first (may contain nested member accesses we need to rewrite)
            var newRight = (ExpressionSyntax)Visit(node.Right)!;

            // Only handle simple assignment where left is our target property
            if (node.Kind() != SyntaxKind.SimpleAssignmentExpression ||
                node.Left is not MemberAccessExpressionSyntax leftMae ||
                leftMae.Name.Identifier.ValueText != _rule.OldPropertyName)
            {
                if (!ReferenceEquals(newRight, node.Right))
                    return node.WithRight(newRight);
                return node;
            }

            // Skip nameof contents (defence-in-depth — rewriting an assignment LHS
            // inside nameof would also be incorrect).
            if (IsInsideNameOf(node))
            {
                if (!ReferenceEquals(newRight, node.Right))
                    return node.WithRight(newRight);
                return node;
            }

            // Check receiver type (use original tree root for heuristic)
            var inferredType = RewriterHelpers.TryInferReceiverTypeName(leftMae, _root);
            if (inferredType is not null &&
                !string.Equals(inferredType, _declTypeShort, StringComparison.Ordinal))
            {
                if (!ReferenceEquals(newRight, node.Right))
                    return node.WithRight(newRight);
                return node;
            }

            // Build set invocation: receiver.SetMethodName(value)
            var writeMethodName = BuildWriteMethodName(_rule.NewMethodName);
            var setInvocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    leftMae.Expression,
                    SyntaxFactory.IdentifierName(writeMethodName)),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(newRight))));

            _ctx.RecordApplied(
                _rule,
                node.Span,
                originalText: node.ToString(),
                replacementText: setInvocation.ToString(),
                line: RewriterHelpers.LineOf(node));

            AppliedCount++;
            return setInvocation.WithTriviaFrom(node);
        }

        /// <summary>
        /// Handles compound-assignment and read context. Assignment LHS is handled by
        /// <see cref="VisitAssignmentExpression"/> above so we guard against it here.
        /// </summary>
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // Recurse first (bottom-up)
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;

            if (visited.Name.Identifier.ValueText != _rule.OldPropertyName)
                return visited;

            // Skip: this is the LHS of an assignment — handled in VisitAssignmentExpression
            if (visited.Parent is AssignmentExpressionSyntax assignParent &&
                assignParent.Left == visited)
            {
                return visited;
            }

            // Skip: nameof(obj.OldProp) — rewriting to nameof(obj.GetOldProp()) breaks
            // the reflection-style semantics. Leave nameof contents untouched.
            if (IsInsideNameOf(visited))
                return visited;

            // Check receiver type
            var inferredType = RewriterHelpers.TryInferReceiverTypeName(visited, _root);
            if (inferredType is not null &&
                !string.Equals(inferredType, _declTypeShort, StringComparison.Ordinal))
            {
                return visited;
            }

            // Compound-assignment context: obj.Prop++ or obj.Prop--
            var parent = visited.Parent;
            if (parent is PostfixUnaryExpressionSyntax or PrefixUnaryExpressionSyntax)
            {
                _ctx.RecordSkipped(
                    _rule,
                    visited.Span,
                    line: RewriterHelpers.LineOf(visited),
                    reason: $"compound-assignment not supported for property-to-method rewrite of '{_rule.OldPropertyName}'");
                return visited;
            }

            // Compound assignment operators (+=, -=, etc.)
            if (parent is AssignmentExpressionSyntax compoundAssign &&
                compoundAssign.Left == visited &&
                compoundAssign.Kind() != SyntaxKind.SimpleAssignmentExpression)
            {
                _ctx.RecordSkipped(
                    _rule,
                    visited.Span,
                    line: RewriterHelpers.LineOf(visited),
                    reason: $"compound-assignment not supported for property-to-method rewrite of '{_rule.OldPropertyName}'");
                return visited;
            }

            // Read context
            var readMethodName = BuildReadMethodName(_rule.NewMethodName);
            var newInvocation = BuildInvocation(visited, readMethodName);

            _ctx.RecordApplied(
                _rule,
                visited.Span,
                originalText: visited.ToString(),
                replacementText: newInvocation.ToString(),
                line: RewriterHelpers.LineOf(visited));

            AppliedCount++;
            return newInvocation;
        }

        /// <summary>
        /// Builds the read-context method name.
        /// <para>
        /// The <see cref="PropertyToMethodRule.NewMethodName"/> is the authoritative method name.
        /// It is used verbatim for all contexts. Rule authors should supply a fully-specified
        /// name such as <c>"GetDisabled"</c> for reads or <c>"SetDisabled"</c> for writes.
        /// When a single rule applies to both read and write contexts the same method name is
        /// called in both, consistent with Java-style paired accessor patterns where the
        /// method name alone disambiguates via overloads.
        /// </para>
        /// </summary>
        private static string BuildReadMethodName(string newMethodName) => newMethodName;

        /// <summary>
        /// Builds the write-context method name.
        /// See <see cref="BuildReadMethodName"/> for rationale — verbatim in all cases.
        /// </summary>
        private static string BuildWriteMethodName(string newMethodName) => newMethodName;

        /// <summary>Builds <c>receiver.methodName()</c> preserving receiver trivia.</summary>
        private static InvocationExpressionSyntax BuildInvocation(
            MemberAccessExpressionSyntax original,
            string methodName)
        {
            var newMae = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                original.Expression,
                SyntaxFactory.IdentifierName(methodName));

            return SyntaxFactory.InvocationExpression(
                newMae,
                SyntaxFactory.ArgumentList())
                .WithTriviaFrom(original);
        }

        /// <summary>
        /// Returns true if <paramref name="node"/> is syntactically inside a
        /// <c>nameof(...)</c> invocation. <c>nameof</c> takes its argument as a
        /// reflection-style identifier reference; rewriting the contents would
        /// break the resulting string and is never the intended behavior.
        /// </summary>
        private static bool IsInsideNameOf(SyntaxNode node)
        {
            for (var current = node.Parent; current is not null; current = current.Parent)
            {
                if (current is InvocationExpressionSyntax inv &&
                    inv.Expression is IdentifierNameSyntax id &&
                    id.Identifier.ValueText == "nameof")
                {
                    return true;
                }

                // nameof is always in an expression context; once we reach a statement
                // boundary we can stop walking.
                if (current is StatementSyntax)
                    break;
            }

            return false;
        }
    }
}
