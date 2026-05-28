using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters.Structural;

/// <summary>
/// Handles <see cref="MoveMemberRule"/>. Rewrites static-style member accesses where
/// the receiver matches the old type's short name: <c>OldType.Member</c> →
/// <c>NewType.Member</c>. Instance-style calls where the receiver cannot be confirmed to
/// be a static type reference emit a <see cref="SkippedRewrite"/> with reason
/// "instance call cannot be moved syntactically".
/// </summary>
internal sealed class MoveMemberRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "moveMember";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not MoveMemberRule typed)
            return null;

        var walker = new MoveMemberWalker(typed, ctx, node);
        var result = walker.Visit(node);
        return walker.AppliedCount > 0 ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class MoveMemberWalker : CSharpSyntaxRewriter
    {
        private readonly MoveMemberRule _rule;
        private readonly RewriteContext _ctx;
        private readonly SyntaxNode _root;
        private readonly string _oldTypeShort;
        private readonly string _newTypeShort;
        internal int AppliedCount;

        internal MoveMemberWalker(MoveMemberRule rule, RewriteContext ctx, SyntaxNode root)
        {
            _rule = rule;
            _ctx = ctx;
            _root = root;
            _oldTypeShort = RewriterHelpers.ShortName(rule.OldTypeName);
            _newTypeShort = RewriterHelpers.ShortName(rule.NewTypeName);
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            // Recurse first (bottom-up)
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;

            // Check member name matches
            if (visited.Name.Identifier.ValueText != _rule.MemberName)
                return visited;

            // The receiver expression must match the old type short name
            if (visited.Expression is not IdentifierNameSyntax receiverIdent)
            {
                // Complex receiver — check if it's a qualified name like Old.Ns.Type.Member
                if (visited.Expression is MemberAccessExpressionSyntax nestedMae)
                {
                    var fullReceiver = nestedMae.ToString();
                    if (!string.Equals(fullReceiver, _rule.OldTypeName, StringComparison.Ordinal) &&
                        !string.Equals(fullReceiver, _oldTypeShort, StringComparison.Ordinal))
                    {
                        return visited;
                    }

                    // Fully-qualified old type — replace with new type
                    var newTypeName = SyntaxFactory.ParseName(_rule.NewTypeName);
                    var replacement = SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        newTypeName,
                        visited.Name)
                        .WithTriviaFrom(visited);

                    _ctx.RecordApplied(
                        _rule,
                        visited.Span,
                        originalText: visited.ToString(),
                        replacementText: replacement.ToString(),
                        line: RewriterHelpers.LineOf(visited));

                    AppliedCount++;
                    return replacement;
                }

                return visited;
            }

            var receiverName = receiverIdent.Identifier.ValueText;

            // The receiver must match the old type short name exactly
            if (!string.Equals(receiverName, _oldTypeShort, StringComparison.Ordinal))
                return visited;

            // Determine if this looks like a static call (receiver starts uppercase)
            // or if it is confirmed to be an instance variable (known declared type != old type)
            var inferredType = RewriterHelpers.TryInferReceiverTypeName(visited, _root);

            if (inferredType is not null &&
                !string.Equals(inferredType, _oldTypeShort, StringComparison.Ordinal))
            {
                // The receiver is an instance variable of a DIFFERENT type — skip
                _ctx.RecordSkipped(
                    _rule,
                    visited.Span,
                    line: RewriterHelpers.LineOf(visited),
                    reason: $"instance call cannot be moved syntactically: receiver '{receiverName}' " +
                            $"is inferred as type '{inferredType}', not static type '{_oldTypeShort}'");
                return visited;
            }

            // If the receiver starts with a lowercase letter and we could not infer a type,
            // it's likely an instance variable — we cannot safely move it
            if (char.IsLower(receiverName[0]) && inferredType is null)
            {
                _ctx.RecordSkipped(
                    _rule,
                    visited.Span,
                    line: RewriterHelpers.LineOf(visited),
                    reason: $"instance call cannot be moved syntactically: receiver '{receiverName}' " +
                            $"appears to be an instance variable (lowercase identifier)");
                return visited;
            }

            // Replace the receiver type with the new type short name
            var newReceiverToken = RewriterHelpers.IdentifierWithTrivia(receiverIdent.Identifier, _newTypeShort);
            var newReceiver = SyntaxFactory.IdentifierName(newReceiverToken);

            var newMae = visited
                .WithExpression(newReceiver);

            _ctx.RecordApplied(
                _rule,
                visited.Span,
                originalText: visited.ToString(),
                replacementText: newMae.ToString(),
                line: RewriterHelpers.LineOf(visited));

            AppliedCount++;
            return newMae;
        }
    }
}
