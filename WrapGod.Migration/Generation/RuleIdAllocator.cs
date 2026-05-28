namespace WrapGod.Migration.Generation;

/// <summary>
/// Deterministic sequential rule-ID allocator.
/// IDs are stable across re-runs for the same <c>(VersionDiff, library, options)</c> triple
/// because callers must sort candidate rules before allocating.
/// </summary>
internal sealed class RuleIdAllocator
{
    private readonly string _prefix;
    private int _counter;

    public RuleIdAllocator(string library, string? overridePrefix)
    {
        if (string.IsNullOrWhiteSpace(library)) throw new ArgumentException("library is required", nameof(library));

        if (!string.IsNullOrWhiteSpace(overridePrefix))
        {
            _prefix = overridePrefix!;
        }
        else
        {
            // Upper-case, remove dots, truncate to 6 chars
            string clean = library.ToUpperInvariant().Replace(".", "");
            _prefix = clean.Length > 6 ? clean.Substring(0, 6) : clean;
        }
    }

    /// <summary>Returns the next sequential ID, e.g. <c>MUD-001</c>.</summary>
    public string Next()
    {
        _counter++;
        return $"{_prefix}-{_counter:D3}";
    }
}
