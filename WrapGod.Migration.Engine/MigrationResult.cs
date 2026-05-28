namespace WrapGod.Migration.Engine;

/// <summary>
/// Aggregates the results of a migration run across all processed files.
/// </summary>
public sealed class MigrationResult
{
    /// <summary>All rewrites that were successfully applied.</summary>
    public IReadOnlyList<AppliedRewrite> Applied { get; }

    /// <summary>All rewrites that were evaluated but not applied.</summary>
    public IReadOnlyList<SkippedRewrite> Skipped { get; }

    /// <summary>
    /// Rules with <see cref="WrapGod.Migration.RuleConfidence.Manual"/> confidence that
    /// require human intervention.
    /// </summary>
    public IReadOnlyList<ManualRewrite> Manual { get; }

    /// <summary>
    /// Per-file rewritten source text (path → new text).
    /// Populated even during a dry run so callers can inspect the would-be output.
    /// </summary>
    public IReadOnlyDictionary<string, string> RewrittenFiles { get; }

    /// <summary>
    /// <see langword="true"/> if this result was produced by a dry-run pass that did not
    /// write files to disk.
    /// </summary>
    public bool DryRun { get; }

    /// <summary>Total number of rewrites applied across all files.</summary>
    public int AppliedCount => Applied.Count;

    /// <summary>Total number of rewrites skipped across all files.</summary>
    public int SkippedCount => Skipped.Count;

    /// <summary>Total number of manual rules identified across all files.</summary>
    public int ManualCount => Manual.Count;

    /// <summary>
    /// Initializes a new <see cref="MigrationResult"/>.
    /// </summary>
    /// <param name="applied">Applied rewrites. Must not be <see langword="null"/>.</param>
    /// <param name="skipped">Skipped rewrites. Must not be <see langword="null"/>.</param>
    /// <param name="manual">Manual rewrites. Must not be <see langword="null"/>.</param>
    /// <param name="rewrittenFiles">Per-file rewritten text. Must not be <see langword="null"/>.</param>
    /// <param name="dryRun">Whether this was a dry-run pass.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any of <paramref name="applied"/>, <paramref name="skipped"/>,
    /// <paramref name="manual"/>, or <paramref name="rewrittenFiles"/> is
    /// <see langword="null"/>.
    /// </exception>
    public MigrationResult(
        IReadOnlyList<AppliedRewrite> applied,
        IReadOnlyList<SkippedRewrite> skipped,
        IReadOnlyList<ManualRewrite> manual,
        IReadOnlyDictionary<string, string> rewrittenFiles,
        bool dryRun)
    {
        ArgumentNullException.ThrowIfNull(applied);
        ArgumentNullException.ThrowIfNull(skipped);
        ArgumentNullException.ThrowIfNull(manual);
        ArgumentNullException.ThrowIfNull(rewrittenFiles);

        Applied = applied;
        Skipped = skipped;
        Manual = manual;
        RewrittenFiles = rewrittenFiles;
        DryRun = dryRun;
    }

    /// <summary>
    /// Returns an empty <see cref="MigrationResult"/> with no applied, skipped, or manual
    /// rewrites and <see cref="DryRun"/> set to <see langword="false"/>.
    /// </summary>
    public static MigrationResult Empty { get; } = new(
        applied: [],
        skipped: [],
        manual: [],
        rewrittenFiles: new Dictionary<string, string>(),
        dryRun: false);
}
