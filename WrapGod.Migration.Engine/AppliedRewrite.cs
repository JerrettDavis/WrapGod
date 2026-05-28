namespace WrapGod.Migration.Engine;

/// <summary>
/// Represents a rewrite that was successfully applied to a source file.
/// </summary>
/// <param name="RuleId">The identifier of the <see cref="WrapGod.Migration.MigrationRule"/> that was applied.</param>
/// <param name="File">Path to the source file that was modified.</param>
/// <param name="Line">1-based line number of the rewrite site.</param>
/// <param name="OriginalText">The original source text that was replaced.</param>
/// <param name="ReplacedWith">The replacement source text.</param>
public sealed record AppliedRewrite(
    string RuleId,
    string File,
    int Line,
    string OriginalText,
    string ReplacedWith);
