namespace WrapGod.Manifest.Config;

/// <summary>
/// Controls the aggressiveness of automatic code fix suggestions during migration.
/// </summary>
public enum MigrationPolicyMode
{
    /// <summary>
    /// Only offer deterministic, safe rewrites (type replacements where the wrapper
    /// is a drop-in substitute). Never offers risky or ambiguous fixes.
    /// </summary>
    Safe = 0,

    /// <summary>
    /// Offer safe auto-fixes plus suggest manual review items. Shows diagnostic
    /// hints for patterns that could be migrated with human oversight.
    /// </summary>
    Assisted = 1,

    /// <summary>
    /// Offer all auto-fixes including risky ones (reflection, dynamic dispatch,
    /// complex generic constructions). Use with caution.
    /// </summary>
    Aggressive = 2,
}

/// <summary>
/// Evaluates whether a code fix should be offered based on the active migration policy.
/// </summary>
public static class MigrationPolicyEvaluator
{
    /// <summary>
    /// Determines whether a code fix with the given risk level should be offered
    /// under the specified policy.
    /// </summary>
    /// <param name="policy">The active migration policy mode.</param>
    /// <param name="fixRisk">The risk classification of the proposed fix.</param>
    /// <returns><c>true</c> if the fix should be offered; otherwise <c>false</c>.</returns>
    public static bool ShouldOfferFix(MigrationPolicyMode policy, FixRiskLevel fixRisk)
    {
        return policy switch
        {
            MigrationPolicyMode.Safe => fixRisk == FixRiskLevel.Safe,
            MigrationPolicyMode.Assisted => fixRisk <= FixRiskLevel.Assisted,
            MigrationPolicyMode.Aggressive => true,
            _ => fixRisk == FixRiskLevel.Safe,
        };
    }
}

/// <summary>
/// Risk classification for a code fix action.
/// </summary>
public enum FixRiskLevel
{
    /// <summary>Deterministic, safe rewrite with no behavioral change.</summary>
    Safe = 0,

    /// <summary>Likely safe but may need manual review in edge cases.</summary>
    Assisted = 1,

    /// <summary>Risky rewrite that may change behavior (reflection, dynamic, etc.).</summary>
    Risky = 2,
}
