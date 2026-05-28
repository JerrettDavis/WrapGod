using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters.Structural;

/// <summary>
/// Handles <see cref="ExtractParameterObjectRule"/>. Rewrites call sites where the method
/// matches <see cref="ExtractParameterObjectRule.MethodName"/> by collapsing the extracted
/// parameters into a <c>new ParamObjType { Prop = value, ... }</c> argument.
/// Only named arguments are accepted; positional-only calls that cannot be mapped
/// unambiguously emit a <see cref="SkippedRewrite"/>.
/// Extra (non-extracted) arguments are preserved and the new parameter object is appended.
/// </summary>
internal sealed class ExtractParameterObjectRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "extractParameterObject";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not ExtractParameterObjectRule typed)
            return null;

        var walker = new ExtractParameterObjectWalker(typed, ctx, node);
        var result = walker.Visit(node);
        return walker.AppliedCount > 0 ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class ExtractParameterObjectWalker : CSharpSyntaxRewriter
    {
        private readonly ExtractParameterObjectRule _rule;
        private readonly RewriteContext _ctx;
        private readonly SyntaxNode _root;
        private readonly string _declTypeShort;
        internal int AppliedCount;

        internal ExtractParameterObjectWalker(
            ExtractParameterObjectRule rule,
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
            // Recurse first
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;

            if (!IsTargetInvocation(visited, out _))
                return visited;

            var args = visited.ArgumentList.Arguments;

            // Separate extracted from non-extracted arguments
            var extractedParams = new HashSet<string>(_rule.ExtractedParameters, StringComparer.OrdinalIgnoreCase);
            var namedExtracted = new List<ArgumentSyntax>();
            var remaining = new List<ArgumentSyntax>();
            bool allPositional = args.All(a => a.NameColon == null);
            bool anyNamed = args.Any(a => a.NameColon is not null);
            bool anyPositional = args.Any(a => a.NameColon is null);

            // Mixed positional + named arguments cannot be safely rewritten by a
            // syntax-only rewriter — we can't know which positional arg corresponds
            // to which extracted parameter. Emit SkippedRewrite to preserve the
            // "ambiguity → skip, never wrong rewrite" contract (no silent data loss).
            if (anyNamed && anyPositional)
            {
                _ctx.RecordSkipped(
                    _rule,
                    visited.Span,
                    line: RewriterHelpers.LineOf(visited),
                    reason: "mixed positional and named arguments not supported: " +
                            "cannot unambiguously map positional args to extracted parameters");
                return visited;
            }

            foreach (var arg in args)
            {
                if (arg.NameColon is not null)
                {
                    var paramName = arg.NameColon.Name.Identifier.ValueText;
                    if (extractedParams.Contains(paramName))
                        namedExtracted.Add(arg);
                    else
                        remaining.Add(arg);
                }
                // Positional args in all-positional calls are handled in the block below.
                // Positional args in mixed calls are already excluded above.
            }

            // Handle all-positional case
            if (allPositional && extractedParams.Count > 0)
            {
                var positionalArgs = args.ToList();
                if (positionalArgs.Count == extractedParams.Count)
                {
                    // Map positionally — match order from ExtractedParameters list
                    namedExtracted.AddRange(positionalArgs);
                    remaining.Clear();
                }
                else if (positionalArgs.Count > extractedParams.Count)
                {
                    // Some positionals are not extracted — ambiguous mapping
                    _ctx.RecordSkipped(
                        _rule,
                        visited.Span,
                        line: RewriterHelpers.LineOf(visited),
                        reason: "positional arguments require named-argument migration: " +
                                "cannot unambiguously map positional args to extracted parameters");
                    return visited;
                }
                else
                {
                    // Fewer positionals than extracted params — also ambiguous
                    _ctx.RecordSkipped(
                        _rule,
                        visited.Span,
                        line: RewriterHelpers.LineOf(visited),
                        reason: "positional arguments require named-argument migration: " +
                                "positional arg count does not match extracted parameter count");
                    return visited;
                }
            }

            if (namedExtracted.Count == 0 && extractedParams.Count > 0)
            {
                // No matching args at all — not a match for this call site
                return visited;
            }

            // Build the object initializer: new ParamObjType { Prop = value, ... }
            var shortTypeName = RewriterHelpers.ShortName(_rule.ParameterObjectType);
            var extractedList = _rule.ExtractedParameters;

            var assignments = new List<ExpressionSyntax>();
            for (int i = 0; i < namedExtracted.Count; i++)
            {
                var arg = namedExtracted[i];
                // Property name: capitalize the parameter name
                string propName;
                if (arg.NameColon is not null)
                {
                    var pName = arg.NameColon.Name.Identifier.ValueText;
                    propName = char.ToUpperInvariant(pName[0]) + pName[1..];
                }
                else
                {
                    // positional — use the extracted param name from rule, capitalized
                    var pName = i < extractedList.Count ? extractedList[i] : $"Param{i}";
                    propName = char.ToUpperInvariant(pName[0]) + pName[1..];
                }

                // Build: Prop = value with single spaces around =
                // (Trailing space on = token; value expression with stripped leading trivia.)
                var valueExpr = arg.Expression
                    .WithLeadingTrivia(SyntaxTriviaList.Empty)
                    .WithTrailingTrivia(SyntaxTriviaList.Empty);

                var assignment = SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(propName),
                    SyntaxFactory.Token(SyntaxKind.EqualsToken)
                        .WithLeadingTrivia(SyntaxFactory.Space)
                        .WithTrailingTrivia(SyntaxFactory.Space),
                    valueExpr);

                assignments.Add(assignment);
            }

            // Build the separator list with spaces after commas for readability
            var separatedAssignments = SyntaxFactory.SeparatedList<ExpressionSyntax>(
                assignments,
                Enumerable.Repeat(
                    SyntaxFactory.Token(SyntaxKind.CommaToken)
                        .WithTrailingTrivia(SyntaxFactory.Space),
                    Math.Max(0, assignments.Count - 1)));

            var initializer = SyntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                    .WithLeadingTrivia(SyntaxFactory.Space)
                    .WithTrailingTrivia(SyntaxFactory.Space),
                separatedAssignments,
                SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                    .WithLeadingTrivia(SyntaxFactory.Space));

            // Build: new TypeName { Prop = value, ... }
            var typeNameSyntax = SyntaxFactory.IdentifierName(shortTypeName)
                .WithLeadingTrivia(SyntaxFactory.Space);

            var objectCreation = SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.Token(SyntaxKind.NewKeyword),
                typeNameSyntax,
                null,
                initializer);

            var newObjectArg = SyntaxFactory.Argument(objectCreation);

            // Build final argument list: remaining (non-extracted) + new object
            var finalArgs = new List<ArgumentSyntax>(remaining) { newObjectArg };

            var newArgList = visited.ArgumentList.WithArguments(
                SyntaxFactory.SeparatedList(finalArgs));

            var replacement = visited.WithArgumentList(newArgList);

            _ctx.RecordApplied(
                _rule,
                visited.Span,
                originalText: visited.ToString(),
                replacementText: replacement.ToString(),
                line: RewriterHelpers.LineOf(visited));

            AppliedCount++;
            return replacement;
        }

        private bool IsTargetInvocation(
            InvocationExpressionSyntax inv,
            out MemberAccessExpressionSyntax mae)
        {
            mae = null!;

            if (inv.Expression is not MemberAccessExpressionSyntax memberAccess)
                return false;

            if (memberAccess.Name.Identifier.ValueText != _rule.MethodName)
                return false;

            var inferredType = RewriterHelpers.TryInferReceiverTypeName(memberAccess, _root);
            if (inferredType is not null &&
                !string.Equals(inferredType, _declTypeShort, StringComparison.Ordinal))
            {
                return false;
            }

            mae = memberAccess;
            return true;
        }
    }
}
