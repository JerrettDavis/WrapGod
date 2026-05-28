using Microsoft.CodeAnalysis;

namespace WrapGod.Migration.Engine;

/// <summary>
/// Convenience extensions for preserving Roslyn syntax trivia (whitespace, comments,
/// directives) when rewriting syntax nodes and tokens.
/// </summary>
public static class TriviaPreservation
{
    /// <summary>
    /// Replaces <paramref name="oldToken"/> with <paramref name="newToken"/> inside
    /// <paramref name="node"/>, copying the leading and trailing trivia from
    /// <paramref name="oldToken"/> to <paramref name="newToken"/> so that surrounding
    /// whitespace and comments are preserved.
    /// </summary>
    /// <typeparam name="T">The concrete <see cref="SyntaxNode"/> type.</typeparam>
    /// <param name="node">The node that contains <paramref name="oldToken"/>.</param>
    /// <param name="oldToken">The token to replace.</param>
    /// <param name="newToken">The replacement token (trivia will be overwritten).</param>
    /// <returns>
    /// A new <typeparamref name="T"/> with the token replaced and trivia preserved.
    /// </returns>
    public static T WithReplacedToken<T>(this T node, SyntaxToken oldToken, SyntaxToken newToken)
        where T : SyntaxNode
    {
        var triviaPreservedToken = newToken
            .WithLeadingTrivia(oldToken.LeadingTrivia)
            .WithTrailingTrivia(oldToken.TrailingTrivia);

        return node.ReplaceToken(oldToken, triviaPreservedToken);
    }
}
