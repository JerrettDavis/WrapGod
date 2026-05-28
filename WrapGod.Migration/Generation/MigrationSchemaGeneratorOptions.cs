namespace WrapGod.Migration.Generation;

/// <summary>
/// Tuning knobs for <see cref="MigrationSchemaGenerator.FromDiff"/>.
/// </summary>
public sealed class MigrationSchemaGeneratorOptions
{
    /// <summary>
    /// Minimum Jaro-Winkler similarity (0.0–1.0) required to treat a "remove + add" pair
    /// as a rename rather than independent operations. Default: 0.65.
    /// </summary>
    public double RenameSimilarityThreshold { get; init; } = 0.65;

    /// <summary>
    /// Similarity at or above which a rename rule's confidence is set to
    /// <see cref="RuleConfidence.Verified"/> (otherwise <see cref="RuleConfidence.Auto"/>).
    /// Default: 0.85.
    /// </summary>
    public double VerifiedSimilarityThreshold { get; init; } = 0.85;

    /// <summary>
    /// Prefix used when auto-generating rule IDs, e.g. <c>"MUD"</c> → <c>"MUD-001"</c>.
    /// If null, the prefix is derived from the <c>library</c> parameter (upper-cased, truncated to 6 chars).
    /// </summary>
    public string? RuleIdPrefix { get; init; }

    /// <summary>
    /// When <see langword="true"/>, rename detection is completely suppressed.
    /// Every removed type/member emits a <see cref="RemoveMemberRule"/>; added entries are skipped.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool DisableRenameDetection { get; init; }
}
