namespace WrapGod.Migration.Engine;

/// <summary>
/// Represents a migration rule with <see cref="WrapGod.Migration.RuleConfidence.Manual"/>
/// confidence that was identified as potentially applicable but not automatically applied.
/// </summary>
/// <param name="RuleId">The identifier of the <see cref="WrapGod.Migration.MigrationRule"/>.</param>
/// <param name="Note">Human-readable guidance for the manual migration step.</param>
/// <param name="MatchedFiles">
/// Files where this rule's pattern was syntactically detected.
/// May be empty if no syntactic match was found.
/// </param>
public sealed record ManualRewrite(
    string RuleId,
    string Note,
    IReadOnlyList<string> MatchedFiles);
