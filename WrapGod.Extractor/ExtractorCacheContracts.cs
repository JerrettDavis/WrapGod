using WrapGod.Manifest;

namespace WrapGod.Extractor;

public interface IExtractorCacheStore
{
    bool TryReadPayload(string payloadHash, out ExtractorCacheEnvelope? envelope);
    void WritePayload(string payloadHash, ExtractorCacheEnvelope envelope);
    void DeletePayload(string payloadHash);
}

public interface IExtractorCacheIndex
{
    bool TryRead(string cacheKeyHash, out ExtractorCacheIndexRecord? record);
    void Upsert(ExtractorCacheIndexRecord record);
    void Delete(string cacheKeyHash);
}

public sealed class ExtractorCacheIndexRecord
{
    public required string CacheKeyHash { get; init; }
    public required string PayloadHash { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed class ExtractorCacheEnvelope
{
    public required string CacheKeyHash { get; init; }
    public required ExtractorCacheKey CacheKey { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required ApiManifest Manifest { get; init; }
}
