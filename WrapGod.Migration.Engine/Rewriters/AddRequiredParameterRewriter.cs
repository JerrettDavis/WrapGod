using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters;

/// <summary>
/// Handles <see cref="AddRequiredParameterRule"/>. Inserts a <c>default</c> keyword
/// (or <c>null</c> for reference types) at the specified position in the argument list
/// of every matching invocation, annotated with a TODO comment so developers know to
/// supply the correct value. Records each insertion as an <see cref="AppliedRewrite"/>.
/// </summary>
internal sealed class AddRequiredParameterRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "addRequiredParameter";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not AddRequiredParameterRule typed)
            return null;

        var walker = new AddRequiredParameterWalker(typed, ctx);
        var result = walker.Visit(node);
        return walker.Changed ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class AddRequiredParameterWalker : CSharpSyntaxRewriter
    {
        private readonly AddRequiredParameterRule _rule;
        private readonly RewriteContext _ctx;
        internal bool Changed;

        internal AddRequiredParameterWalker(AddRequiredParameterRule rule, RewriteContext ctx)
        {
            _rule = rule;
            _ctx = ctx;
        }

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (!IsTargetMethodCall(node))
                return base.VisitInvocationExpression(node);

            var argList = node.ArgumentList;
            var args = argList.Arguments;
            var position = _rule.Position;

            // Build the default argument with a TODO comment
            var todoComment = SyntaxFactory.Comment(
                $"/* TODO MIGRATION: {_rule.Id} required arg '{_rule.ParameterName}' ({_rule.ParameterType}) added */");

            var defaultExpression = SyntaxFactory
                .LiteralExpression(SyntaxKind.DefaultLiteralExpression)
                .WithLeadingTrivia(SyntaxFactory.Space)
                .WithTrailingTrivia(SyntaxFactory.Space, todoComment);

            var newArg = SyntaxFactory.Argument(defaultExpression);

            // Clamp position to valid range
            var insertAt = Math.Clamp(position, 0, args.Count);

            var newArgsList = args.Insert(insertAt, newArg);
            // Rebuild separators: take existing separators and add one for the new arg
            // The simplest approach: use SeparatedList which handles separators automatically
            var newArgList = argList.WithArguments(newArgsList);

            _ctx.RecordApplied(
                _rule,
                argList.Span,
                originalText: argList.ToString(),
                replacementText: newArgList.ToString(),
                line: RewriterHelpers.LineOf(argList));

            Changed = true;
            return node.WithArgumentList(newArgList);
        }

        private bool IsTargetMethodCall(InvocationExpressionSyntax node)
        {
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
