namespace WrapGod.Migration.Engine.State;

/// <summary>
/// Persistent migration state written alongside the schema file after each
/// <c>apply</c> run. Enables idempotent re-runs and powers <c>migrate status</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>File location:</strong> Same directory as the schema file, with filename
/// <c>{schemaFilename}.state.json</c>. For example,
/// <c>mudblazor.6.0-to-7.0.wrapgod-migration.json</c> produces
/// <c>mudblazor.6.0-to-7.0.wrapgod-migration.json.state.json</c>.
/// </para>
/// <para>
/// <strong>Idempotence:</strong> On each run the engine computes a SHA-256 hash of the
/// schema file content (after normalising line endings). If the hash matches
/// <see cref="SchemaHash"/>, rules whose <c>(RuleId, File)</c> pair appears in
/// <see cref="Applied"/> are skipped. When the hash differs all rules are re-evaluated.
/// </para>
/// <para>
/// <strong>List semantics:</strong>
/// <list type="bullet">
///   <item><description><see cref="Applied"/> — append-only, de-duplicated by <c>(RuleId, File)</c>.</description></item>
///   <item><description><see cref="Skipped"/> — replaced wholesale each run.</description></item>
///   <item><description><see cref="Manual"/> — replaced wholesale each run.</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class MigrationState
{
    /// <summary>
    /// Path to the schema file this state was generated from.
    /// May be relative or absolute; used for display and orphan detection.
    /// </summary>
    public string Schema { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the schema file content (format: <c>sha256:&lt;lowercase-hex&gt;</c>).
    /// Computed after normalising line endings to <c>\n</c> and trimming trailing whitespace
    /// per line so the hash is insensitive to git autocrlf behaviour.
    /// </summary>
    public string SchemaHash { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the first <c>apply</c> run began for this schema.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>UTC timestamp when the most recent <c>apply</c> run completed.</summary>
    public DateTimeOffset LastRunAt { get; set; }

    /// <summary>Aggregated counts from the last run for quick summary display.</summary>
    public MigrationStateSummary Summary { get; set; } = new();

    /// <summary>
    /// Rewrites that were successfully applied across all runs.
    /// Append-only and de-duplicated by <c>(RuleId, File)</c>.
    /// </summary>
    public List<AppliedRewrite> Applied { get; set; } = [];

    /// <summary>
    /// Rewrites that were skipped during the most recent run.
    /// Replaced wholesale each run.
    /// </summary>
    public List<SkippedRewrite> Skipped { get; set; } = [];

    /// <summary>
    /// Manual-confidence rules identified during the most recent run.
    /// Replaced wholesale each run.
    /// </summary>
    public List<ManualRewrite> Manual { get; set; } = [];

    // ── State logic ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when a rewrite for <paramref name="ruleId"/>
    /// has already been applied to <paramref name="file"/> in a prior run.
    /// </summary>
    public bool IsAlreadyApplied(string ruleId, string file) =>
        Applied.Any(a =>
            string.Equals(a.RuleId, ruleId, StringComparison.Ordinal) &&
            string.Equals(a.File, file, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="newSchemaHash"/> differs
    /// from <see cref="SchemaHash"/>, indicating the schema has been edited since
    /// this state was written.
    /// </summary>
    public bool SchemaHasChanged(string newSchemaHash) =>
        !string.Equals(SchemaHash, newSchemaHash, StringComparison.Ordinal);

    /// <summary>
    /// Produces an updated <see cref="MigrationState"/> by merging
    /// <paramref name="newResult"/> with the current state.
    /// </summary>
    /// <param name="newResult">The result of the latest <c>apply</c> run.</param>
    /// <param name="schemaHash">The current schema hash to record.</param>
    /// <returns>A new <see cref="MigrationState"/> instance with merged data.</returns>
    public MigrationState Merge(MigrationResult newResult, string schemaHash)
    {
        ArgumentNullException.ThrowIfNull(newResult);
        ArgumentNullException.ThrowIfNull(schemaHash);

        // Applied: append-only, de-duplicated by (RuleId, File).
        var seen = new HashSet<(string RuleId, string File)>(
            Applied.Select(a => (a.RuleId, a.File)),
            EqualityComparer<(string, string)>.Default);

        var mergedApplied = new List<AppliedRewrite>(Applied);
        foreach (var a in newResult.Applied)
        {
            var key = (a.RuleId, a.File);
            if (seen.Add(key))
                mergedApplied.Add(a);
        }

        // Skipped + Manual: replaced wholesale.
        var newManual = newResult.Manual
            .Select(m => new ManualRewrite(m.RuleId, m.Note, m.MatchedFiles))
            .ToList();

        var newSummary = new MigrationStateSummary
        {
            TotalRules = mergedApplied.Count + newResult.Skipped.Count + newManual.Count,
            Applied = mergedApplied.Count,
            Skipped = newResult.Skipped.Count,
            Manual  = newManual.Count,
        };

        return new MigrationState
        {
            Schema    = Schema,
            SchemaHash = schemaHash,
            StartedAt = StartedAt == default ? DateTimeOffset.UtcNow : StartedAt,
            LastRunAt  = DateTimeOffset.UtcNow,
            Summary    = newSummary,
            Applied    = mergedApplied,
            Skipped    = [.. newResult.Skipped],
            Manual     = newManual,
        };
    }
}
