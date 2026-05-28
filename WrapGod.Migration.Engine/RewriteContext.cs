using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis.Text;
using WrapGod.Migration;

namespace WrapGod.Migration.Engine;

/// <summary>
/// Carries per-file context for a rewrite pass and accumulates the audit trail of
/// applied and skipped rewrites.
/// </summary>
public sealed class RewriteContext
{
    private readonly List<AppliedRewrite> _applied = [];
    private readonly List<SkippedRewrite> _skipped = [];
    private readonly ReadOnlyCollection<AppliedRewrite> _appliedView;
    private readonly ReadOnlyCollection<SkippedRewrite> _skippedView;

    /// <summary>Absolute or relative path to the source file being rewritten.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Read-only view of rewrites that were successfully applied.
    /// The returned collection cannot be cast to a mutable type.
    /// </summary>
    public IReadOnlyList<AppliedRewrite> Applied => _appliedView;

    /// <summary>
    /// Read-only view of rewrites that were skipped (ambiguous, no-match, etc.).
    /// The returned collection cannot be cast to a mutable type.
    /// </summary>
    public IReadOnlyList<SkippedRewrite> Skipped => _skippedView;

    /// <summary>Initializes a new <see cref="RewriteContext"/> for the given file.</summary>
    /// <param name="filePath">Absolute or relative path to the source file.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="filePath"/> is <see langword="null"/>.
    /// </exception>
    public RewriteContext(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        FilePath = filePath;
        _appliedView = _applied.AsReadOnly();
        _skippedView = _skipped.AsReadOnly();
    }

    /// <summary>Records a successfully applied rewrite.</summary>
    /// <param name="rule">The rule that was applied.</param>
    /// <param name="original">The text span of the original code.</param>
    /// <param name="originalText">The original source text.</param>
    /// <param name="replacementText">The replacement source text.</param>
    /// <param name="line">1-based line number of the rewrite site.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="rule"/>, <paramref name="originalText"/>,
    /// or <paramref name="replacementText"/> is <see langword="null"/>.
    /// </exception>
    public void RecordApplied(
        MigrationRule rule,
        TextSpan original,
        string originalText,
        string replacementText,
        int line)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(originalText);
        ArgumentNullException.ThrowIfNull(replacementText);

        _applied.Add(new AppliedRewrite(rule.Id, FilePath, line, originalText, replacementText));
    }

    /// <summary>Records a rewrite that was skipped.</summary>
    /// <param name="rule">The rule that was evaluated.</param>
    /// <param name="location">The text span of the potential rewrite site.</param>
    /// <param name="line">1-based line number of the potential rewrite site.</param>
    /// <param name="reason">Human-readable explanation of why the rewrite was skipped.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="rule"/> or <paramref name="reason"/> is
    /// <see langword="null"/>.
    /// </exception>
    public void RecordSkipped(
        MigrationRule rule,
        TextSpan location,
        int line,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(reason);

        _skipped.Add(new SkippedRewrite(rule.Id, FilePath, line, reason));
    }
}
