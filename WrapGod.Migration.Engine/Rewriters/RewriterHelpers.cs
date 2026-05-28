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
    /// Searches, in order:
    /// (1) local variable declarations and parameters in the enclosing method body,
    /// (2) field and property declarations on the enclosing type,
    /// (3) the receiver identifier itself as a static type name (uppercase-start).
    /// Returns the short type name when found, otherwise <see langword="null"/>.
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

        if (enclosingBody is not null)
        {
            // Scan local variable declarations: skip fields (those are scanned separately
            // below). Only inspect locals declared inside the method body.
            foreach (var local in enclosingBody.DescendantNodes().OfType<VariableDeclarationSyntax>())
            {
                if (local.Parent is FieldDeclarationSyntax)
                    continue;

                foreach (var variable in local.Variables)
                {
                    if (variable.Identifier.ValueText != receiverName)
                        continue;

                    var inferred = ExtractShortTypeName(local.Type);
                    if (inferred is not null)
                        return inferred;
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

                    var inferred = ExtractShortTypeName(param.Type);
                    if (inferred is not null)
                        return inferred;
                }
            }
        }

        // Walk up to the enclosing type declaration to scan fields and properties.
        var enclosingType = memberAccess
            .Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();

        if (enclosingType is not null)
        {
            // Fields: e.g. `private readonly MyService _service;`
            foreach (var field in enclosingType.Members.OfType<FieldDeclarationSyntax>())
            {
                foreach (var variable in field.Declaration.Variables)
                {
                    if (variable.Identifier.ValueText != receiverName)
                        continue;

                    var inferred = ExtractShortTypeName(field.Declaration.Type);
                    if (inferred is not null)
                        return inferred;
                }
            }

            // Properties: e.g. `public MyService Service { get; }`
            foreach (var prop in enclosingType.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (prop.Identifier.ValueText != receiverName)
                    continue;

                var inferred = ExtractShortTypeName(prop.Type);
                if (inferred is not null)
                    return inferred;
            }
        }

        // Static-style receiver: if the receiver identifier text starts with an uppercase
        // letter and no locals/fields/properties/parameters with that name were found, treat
        // it as a type-of-name reference (e.g. `MyStatic.OldMethod()` calls a static member).
        if (!string.IsNullOrEmpty(receiverName) && char.IsUpper(receiverName[0]))
            return receiverName;

        return null;
    }

    /// <summary>
    /// Extracts the short (unqualified) type name from a <see cref="TypeSyntax"/>,
    /// peeling off nullable annotations and arrays. Returns <see langword="null"/> when
    /// the type cannot be reduced to a simple short name.
    /// </summary>
    private static string? ExtractShortTypeName(TypeSyntax typeSyntax)
    {
        if (typeSyntax is NullableTypeSyntax nullable)
            typeSyntax = nullable.ElementType;

        if (typeSyntax is ArrayTypeSyntax array)
            typeSyntax = array.ElementType;

        return typeSyntax switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax qn => qn.Right.Identifier.ValueText,
            GenericNameSyntax gn => gn.Identifier.ValueText,
            _ => null,
        };
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
