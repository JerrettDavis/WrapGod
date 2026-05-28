using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WrapGod.Migration.Engine.Rewriters;

/// <summary>
/// Shared syntax-only utilities used by the A-level rewriters.
/// </summary>
internal static class RewriterHelpers
{
    /// <summary>
    /// Returns the short (unqualified) name from a fully-qualified type name.
    /// e.g. <c>"Foo.Bar.Baz"</c> → <c>"Baz"</c>.
    /// </summary>
    internal static string ShortName(string fullyQualified)
    {
        var dot = fullyQualified.LastIndexOf('.');
        return dot < 0 ? fullyQualified : fullyQualified[(dot + 1)..];
    }

    /// <summary>
    /// Returns the namespace portion from a fully-qualified type name.
    /// e.g. <c>"Foo.Bar.Baz"</c> → <c>"Foo.Bar"</c>. Returns empty string when
    /// there is no namespace (bare name).
    /// </summary>
    internal static string Namespace(string fullyQualified)
    {
        var dot = fullyQualified.LastIndexOf('.');
        return dot < 0 ? string.Empty : fullyQualified[..dot];
    }

    /// <summary>
    /// Attempts to infer the declared type name of the receiver expression in a
    /// <see cref="MemberAccessExpressionSyntax"/> using syntax-only heuristics.
    /// Returns the simple identifier text if the receiver is a simple name for which
    /// a local/field declaration with an explicit type is visible in the same method
    /// body, otherwise returns <see langword="null"/>.
    /// </summary>
    internal static string? TryInferReceiverTypeName(
        MemberAccessExpressionSyntax memberAccess,
        SyntaxNode root)
    {
        // Only handle the simplest case: receiver is a plain identifier
        if (memberAccess.Expression is not IdentifierNameSyntax receiverIdentifier)
            return null;

        var receiverName = receiverIdentifier.Identifier.ValueText;

        // Walk upward to the nearest method or property body to find local variable declarations
        var enclosingBody = memberAccess
            .Ancestors()
            .FirstOrDefault(a =>
                a is MethodDeclarationSyntax or
                PropertyDeclarationSyntax or
                ConstructorDeclarationSyntax or
                AccessorDeclarationSyntax);

        if (enclosingBody is null)
            return null;

        // Scan local variable declarations: look for "TypeName varName =" or "TypeName varName;"
        foreach (var local in enclosingBody.DescendantNodes().OfType<VariableDeclarationSyntax>())
        {
            foreach (var variable in local.Variables)
            {
                if (variable.Identifier.ValueText != receiverName)
                    continue;

                // Extract the declared type text (e.g., "MyService", "MyService?", etc.)
                var typeSyntax = local.Type;
                if (typeSyntax is NullableTypeSyntax nullable)
                    typeSyntax = nullable.ElementType;

                if (typeSyntax is IdentifierNameSyntax idType)
                    return idType.Identifier.ValueText;

                if (typeSyntax is QualifiedNameSyntax qn)
                    return qn.Right.Identifier.ValueText;

                if (typeSyntax is GenericNameSyntax gn)
                    return gn.Identifier.ValueText;
            }
        }

        // Also check method / constructor parameters
        var paramLists = enclosingBody
            .ChildNodes()
            .OfType<ParameterListSyntax>()
            .Concat(enclosingBody.AncestorsAndSelf()
                .OfType<MethodDeclarationSyntax>()
                .Select(m => m.ParameterList))
            .Concat(enclosingBody.AncestorsAndSelf()
                .OfType<ConstructorDeclarationSyntax>()
                .Select(c => c.ParameterList));

        foreach (var paramList in paramLists)
        {
            foreach (var param in paramList.Parameters)
            {
                if (param.Identifier.ValueText != receiverName)
                    continue;

                if (param.Type is null)
                    continue;

                var pType = param.Type;
                if (pType is NullableTypeSyntax npt)
                    pType = npt.ElementType;

                if (pType is IdentifierNameSyntax pid)
                    return pid.Identifier.ValueText;

                if (pType is QualifiedNameSyntax pqn)
                    return pqn.Right.Identifier.ValueText;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the 1-based line number for a <see cref="SyntaxNode"/>.
    /// </summary>
    internal static int LineOf(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    /// <summary>
    /// Returns the 1-based line number for a <see cref="SyntaxToken"/>.
    /// </summary>
    internal static int LineOf(SyntaxToken token) =>
        token.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    /// <summary>
    /// Creates an identifier token with the given text, suitable for use as a
    /// replacement identifier while preserving trivia from the original token.
    /// </summary>
    internal static SyntaxToken IdentifierWithTrivia(SyntaxToken original, string newText) =>
        SyntaxFactory.Identifier(newText)
            .WithLeadingTrivia(original.LeadingTrivia)
            .WithTrailingTrivia(original.TrailingTrivia);
}
