using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters;

/// <summary>
/// Handles <see cref="ChangeParameterRule"/>. For parameter-name-only changes, rewrites
/// the named-argument label in invocations of the target method. For type-change rules
/// (when positional arguments are used), records a <see cref="SkippedRewrite"/> because
/// a syntax-only rewriter cannot safely cast or convert argument values.
/// </summary>
internal sealed class ChangeParameterRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "changeParameter";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not ChangeParameterRule typed)
            return null;

        var walker = new ChangeParameterWalker(typed, ctx, node);
        var result = walker.Visit(node);
        return walker.Changed ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class ChangeParameterWalker : CSharpSyntaxRewriter
    {
        private readonly ChangeParameterRule _rule;
        private readonly RewriteContext _ctx;
        private readonly SyntaxNode _root;
        private readonly string _declTypeShort;
        internal bool Changed;

        internal ChangeParameterWalker(
            ChangeParameterRule rule,
            RewriteContext ctx,
            SyntaxNode root)
        {
            _rule = rule;
            _ctx = ctx;
            _root = root;
            _declTypeShort = RewriterHelpers.ShortName(rule.TypeName);
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            // Check if this invocation is a call to the target method on the target type.
            if (!IsTargetMethodCall(node))
                return base.VisitInvocationExpression(node);

            // Receiver-type disambiguation: if the receiver is a member access whose receiver
            // type can be inferred and does NOT match the declaring type, record a Skipped
            // and leave the invocation alone. This prevents wrong rewrites on unrelated
            // methods that share a name.
            if (node.Expression is MemberAccessExpressionSyntax mae)
            {
                var inferred = RewriterHelpers.TryInferReceiverTypeName(mae, _root);
                if (inferred is not null &&
                    !string.Equals(inferred, _declTypeShort, StringComparison.Ordinal))
                {
                    _ctx.RecordSkipped(
                        _rule,
                        node.Span,
                        line: RewriterHelpers.LineOf(node),
                        reason: $"ambiguous: receiver type '{inferred}' does not match " +
                                $"declaring type '{_declTypeShort}'");
                    return base.VisitInvocationExpression(node);
                }
            }

            var argList = node.ArgumentList;
            var newArgs = new List<ArgumentSyntax>(argList.Arguments.Count);
            var anyChanged = false;

            foreach (var arg in argList.Arguments)
            {
                if (arg.NameColon is not null &&
                    arg.NameColon.Name.Identifier.ValueText == _rule.OldParameterName)
                {
                    // Named argument with matching label — rewrite the label if name changed
                    if (_rule.NewParameterName is not null &&
                        _rule.NewParameterName != _rule.OldParameterName)
                    {
                        var oldToken = arg.NameColon.Name.Identifier;
                        var newToken = RewriterHelpers.IdentifierWithTrivia(
                            oldToken, _rule.NewParameterName);
                        var newNameColon = arg.NameColon.WithName(
                            arg.NameColon.Name.WithIdentifier(newToken));
                        var newArg = arg.WithNameColon(newNameColon);

                        _ctx.RecordApplied(
                            _rule,
                            arg.NameColon.Span,
                            originalText: _rule.OldParameterName + ":",
                            replacementText: _rule.NewParameterName + ":",
                            line: RewriterHelpers.LineOf(arg));

                        newArgs.Add(newArg);
                        anyChanged = true;
                        continue;
                    }
                }
                else if (arg.NameColon is null &&
                         _rule.OldParameterType is not null &&
                         _rule.NewParameterType is not null)
                {
                    // Positional argument with a type change — cannot safely rewrite
                    _ctx.RecordSkipped(
                        _rule,
                        arg.Span,
                        line: RewriterHelpers.LineOf(arg),
                        reason: $"type change from '{_rule.OldParameterType}' to " +
                                $"'{_rule.NewParameterType}' requires semantic conversion — " +
                                "manual review required");
                }

                newArgs.Add(arg);
            }

            if (!anyChanged)
                return base.VisitInvocationExpression(node);

            Changed = true;
            var newArgList = argList.WithArguments(
                SyntaxFactory.SeparatedList(newArgs, argList.Arguments.GetSeparators()));
            return node.WithArgumentList(newArgList);
        }

        private bool IsTargetMethodCall(InvocationExpressionSyntax node)
        {
            // Simple heuristic: check the method name matches regardless of receiver type
            // (syntax-only; we cannot resolve the declaring type without SemanticModel)
            return node.Expression switch
            {
                MemberAccessExpressionSyntax mae =>
                    mae.Name.Identifier.ValueText == _rule.MethodName,
                IdentifierNameSyntax id =>
                    id.Identifier.ValueText == _rule.MethodName,
                _ => false,
            };
        }
    }
}
