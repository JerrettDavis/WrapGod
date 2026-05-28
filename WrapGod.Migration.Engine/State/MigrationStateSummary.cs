namespace WrapGod.Migration.Engine.State;

/// <summary>
/// Aggregated counts from a migration run, stored in the state file for quick inspection
/// without deserializing the full <see cref="AppliedRewrite"/>, <see cref="SkippedRewrite"/>,
/// and <see cref="ManualRewrite"/> lists.
/// </summary>
public sealed class MigrationStateSummary
{
    /// <summary>Total number of auto-confidence rules evaluated during the last run.</summary>
    public int TotalRules { get; set; }

    /// <summary>Number of rewrites successfully applied during the last run.</summary>
    public int Applied { get; set; }

    /// <summary>Number of rewrite sites skipped during the last run.</summary>
    public int Skipped { get; set; }

    /// <summary>Number of manual-confidence rules identified during the last run.</summary>
    public int Manual { get; set; }
}
