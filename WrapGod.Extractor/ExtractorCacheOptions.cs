namespace WrapGod.Extractor;

public sealed class ExtractorCacheOptions
{
    public static ExtractorCacheOptions Default { get; } = new();

    public bool Enabled { get; init; } = true;
    public string? ProjectCacheIndexRoot { get; init; }
    public string? SharedCacheRoot { get; init; }
    public bool PublicOnly { get; init; } = true;
    public bool IncludeObsoleteDetails { get; init; } = false;
    public IReadOnlyDictionary<string, string?> Custom { get; init; } = new Dictionary<string, string?>();
}
