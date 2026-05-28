namespace WrapGod.Migration.Engine;

/// <summary>
/// Represents a rewrite that was evaluated but not applied.
/// </summary>
/// <param name="RuleId">The identifier of the <see cref="WrapGod.Migration.MigrationRule"/> that was evaluated.</param>
/// <param name="File">Path to the source file where the potential rewrite site was found.</param>
/// <param name="Line">1-based line number of the potential rewrite site.</param>
/// <param name="Reason">Human-readable explanation of why the rewrite was not applied.</param>
public sealed record SkippedRewrite(
    string RuleId,
    string File,
    int Line,
    string Reason);
