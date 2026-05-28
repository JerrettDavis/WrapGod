using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine.Rewriters;

/// <summary>
/// Handles <see cref="ChangeTypeReferenceRule"/>. Replaces all occurrences of
/// <see cref="ChangeTypeReferenceRule.OldType"/> (by short name) in type positions
/// (field declarations, local variable declarations, casts, <c>typeof</c>, generic
/// type arguments, base lists) with <see cref="ChangeTypeReferenceRule.NewType"/>.
/// </summary>
internal sealed class ChangeTypeReferenceRewriter : IRuleRewriter
{
    /// <inheritdoc/>
    public string Kind => "changeTypeReference";

    /// <inheritdoc/>
    public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx)
    {
        if (rule is not ChangeTypeReferenceRule typed)
            return null;

        var walker = new ChangeTypeReferenceWalker(typed, ctx);
        var result = walker.Visit(node);
        return walker.Changed ? result : null;
    }

    // ── Inner CSharpSyntaxRewriter ────────────────────────────────────────────

    private sealed class ChangeTypeReferenceWalker : CSharpSyntaxRewriter
    {
        private readonly ChangeTypeReferenceRule _rule;
        private readonly RewriteContext _ctx;
        private readonly string _oldShort;
        private readonly string _newShort;
        internal bool Changed;

        internal ChangeTypeReferenceWalker(ChangeTypeReferenceRule rule, RewriteContext ctx)
        {
            _rule = rule;
            _ctx = ctx;
            _oldShort = RewriterHelpers.ShortName(rule.OldType);
            _newShort = RewriterHelpers.ShortName(rule.NewType);
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.ValueText != _oldShort)
                return base.VisitIdentifierName(node);

            // Only rewrite nodes that appear in a type-context position
            if (!IsInTypeContext(node))
                return base.VisitIdentifierName(node);

            var newToken = RewriterHelpers.IdentifierWithTrivia(node.Identifier, _newShort);
            var replacement = node.WithIdentifier(newToken);

            _ctx.RecordApplied(
                _rule,
                node.Span,
                originalText: node.ToString(),
                replacementText: replacement.ToString(),
                line: RewriterHelpers.LineOf(node));

            Changed = true;
            return replacement;
        }

        public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
        {
            // Handle generic type references like IList<string> where the identifier
            // is the generic name's identifier, e.g. "IList" in IList<string>
            if (node.Identifier.ValueText != _oldShort)
                return base.VisitGenericName(node);

            if (!IsInTypeContext(node))
                return base.VisitGenericName(node);

            var newToken = RewriterHelpers.IdentifierWithTrivia(node.Identifier, _newShort);
            var replacement = node.WithIdentifier(newToken);

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
            // Handle fully-qualified references
            var nodeText = node.ToString();
            if (nodeText != _rule.OldType)
                return base.VisitQualifiedName(node);

            if (!IsInTypeContext(node))
                return base.VisitQualifiedName(node);

            var replacement = SyntaxFactory.ParseName(_rule.NewType)
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

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="node"/> appears in a
        /// syntactic position that is unambiguously a type reference (not an identifier
        /// used as a variable name or method name).
        /// </summary>
        private static bool IsInTypeContext(SyntaxNode node)
        {
            var parent = node.Parent;
            return parent is
                // Variable / field declaration: int foo (type is direct child of VariableDeclarationSyntax)
                VariableDeclarationSyntax or
                // Method return type (type is direct child of MethodDeclarationSyntax)
                MethodDeclarationSyntax or
                // Parameter type
                ParameterSyntax or
                // Cast expression: (TypeName)x
                CastExpressionSyntax or
                // typeof(TypeName)
                TypeOfExpressionSyntax or
                // Generic type argument (e.g. T in List<T>)
                TypeArgumentListSyntax or
                // Base list: class Foo : Bar
                SimpleBaseTypeSyntax or
                // Object creation: new TypeName()
                ObjectCreationExpressionSyntax or
                // Array type: TypeName[]
                ArrayTypeSyntax or
                // Nullable type: TypeName?
                NullableTypeSyntax or
                // Property type
                PropertyDeclarationSyntax;
        }
    }
}
