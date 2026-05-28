using Microsoft.CodeAnalysis;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine;

/// <summary>
/// A rule rewriter transforms a single <see cref="SyntaxNode"/> according to a
/// <see cref="MigrationRule"/>, producing a rewritten node or returning <see langword="null"/>
/// when the rule does not apply to that node.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Trivia contract:</strong> every replacement node MUST preserve leading and trailing
/// trivia from the original node by calling <c>newNode.WithTriviaFrom(originalNode)</c>
/// (or the token-level equivalent). Failure to do so will corrupt whitespace and comments
/// in the output file.
/// </para>
/// <para>
/// <strong>No-match contract:</strong> return <see langword="null"/> when the rule does not
/// match the current node. Never throw; never return a partially-modified node that is
/// semantically incorrect. If the match is ambiguous, record a <see cref="SkippedRewrite"/>
/// via <see cref="RewriteContext"/> and return <see langword="null"/>.
/// </para>
/// </remarks>
public interface IRuleRewriter
{
    /// <summary>
    /// The <see cref="MigrationRuleKind"/> name (camelCase, matches the JSON schema
    /// discriminator) that this rewriter handles, e.g. <c>"renameType"</c>.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Attempts to rewrite <paramref name="node"/> according to <paramref name="rule"/>.
    /// </summary>
    /// <param name="node">The syntax node to potentially rewrite.</param>
    /// <param name="rule">The migration rule to apply.</param>
    /// <param name="ctx">The rewrite context for recording applied/skipped results.</param>
    /// <returns>
    /// A replacement <see cref="SyntaxNode"/> with trivia preserved via
    /// <c>WithTriviaFrom</c>, or <see langword="null"/> if this rule does not match
    /// <paramref name="node"/>.
    /// </returns>
    SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx);
}
