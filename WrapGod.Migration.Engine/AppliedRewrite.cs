namespace WrapGod.Migration.Engine;

/// <summary>
/// Represents a rewrite that was successfully applied to a source file.
/// </summary>
/// <param name="RuleId">The identifier of the <see cref="WrapGod.Migration.MigrationRule"/> that was applied.</param>
/// <param name="File">Path to the source file that was modified.</param>
/// <param name="Line">1-based line number of the rewrite site.</param>
/// <param name="OriginalText">The original source text that was replaced.</param>
/// <param name="ReplacedWith">The replacement source text.</param>
/// <param name="AppliedAt">
/// UTC timestamp when the rewrite was recorded by the engine. Used by downstream
/// consumers (e.g. <c>migrate verify</c>) to disambiguate which rule is the
/// most recently-applied when multiple rewrites are equidistant from a compiler
/// diagnostic line. Defaults to <see cref="DateTimeOffset.MinValue"/> when not
/// supplied — older state files that predate this field will deserialize with
/// the default value, in which case index-order tiebreaking still applies.
/// </param>
public sealed record AppliedRewrite(
    string RuleId,
    string File,
    int Line,
    string OriginalText,
    string ReplacedWith,
    DateTimeOffset AppliedAt = default);
