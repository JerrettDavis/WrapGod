namespace WrapGod.Generator;

/// <summary>
/// Controls how the generator handles members that exist in only a subset
/// of the targeted API versions.
/// </summary>
internal enum CompatibilityMode
{
    /// <summary>
    /// Lowest Common Denominator: emit only members present in ALL versions
    /// (introduced in the earliest version and never removed).
    /// </summary>
    Lcd,

    /// <summary>
    /// Targeted: emit members present in a single selected version.
    /// </summary>
    Targeted,

    /// <summary>
    /// Adaptive: emit all members, decorating version-specific ones with
    /// runtime availability guards so callers can branch on version.
    /// </summary>
    Adaptive,
}
