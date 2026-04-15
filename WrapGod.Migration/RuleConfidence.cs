namespace WrapGod.Migration;

/// <summary>
/// Indicates how confident the tooling is that a migration rule can be applied automatically.
/// </summary>
public enum RuleConfidence
{
    /// <summary>The fix can be applied fully automatically without human review.</summary>
    Auto,

    /// <summary>The fix has been manually reviewed and confirmed correct.</summary>
    Verified,

    /// <summary>The fix requires a human to apply it; automated application is not safe.</summary>
    Manual,
}
