using System.Text.Json;
using WrapGod.Manifest;

namespace WrapGod.Extractor;

internal static class RfcExtractorCache
{
    public static ApiManifest ExtractOrAdd(string assemblyPath, ExtractorCacheOptions options, Func<ApiManifest> coldExtract)
    {
        var key = ExtractorCacheKey.CreateForAssembly(assemblyPath, options);
        var keyHash = key.ComputeHash();

        var indexRoot = options.ProjectCacheIndexRoot
            ?? Path.Combine(Path.GetDirectoryName(assemblyPath) ?? Directory.GetCurrentDirectory(), "obj", "wrapgod", "cache-index");

        var sharedRoot = options.SharedCacheRoot ?? GetDefaultSharedCacheRoot();
        var indexPath = Path.Combine(indexRoot, $"{keyHash}.index.json");
        var payloadPath = Path.Combine(sharedRoot, $"{keyHash}.manifest.json");

        try
        {
            if (File.Exists(indexPath) && File.Exists(payloadPath))
            {
                var cached = JsonSerializer.Deserialize<ExtractorCacheEnvelope>(File.ReadAllText(payloadPath));
                if (cached is not null && IsValid(cached, keyHash, key))
                    return cached.Manifest;
            }
            else if (File.Exists(payloadPath))
            {
                var cached = JsonSerializer.Deserialize<ExtractorCacheEnvelope>(File.ReadAllText(payloadPath));
                if (cached is not null && IsValid(cached, keyHash, key))
                {
                    Directory.CreateDirectory(indexRoot);
                    File.WriteAllText(indexPath, JsonSerializer.Serialize(new ExtractorCacheIndexRecord
                    {
                        CacheKeyHash = keyHash,
                        PayloadHash = keyHash,
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                    }));
                    return cached.Manifest;
                }
            }
        }
        catch
        {
            TryDelete(payloadPath);
            TryDelete(indexPath);
        }

        var cold = coldExtract();

        try
        {
            Directory.CreateDirectory(sharedRoot);
            Directory.CreateDirectory(indexRoot);

            var envelope = new ExtractorCacheEnvelope
            {
                CacheKeyHash = keyHash,
                CacheKey = key,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Manifest = cold,
            };

            File.WriteAllText(payloadPath, JsonSerializer.Serialize(envelope));
            File.WriteAllText(indexPath, JsonSerializer.Serialize(new ExtractorCacheIndexRecord
            {
                CacheKeyHash = keyHash,
                PayloadHash = keyHash,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            }));
        }
        catch
        {
            // best effort only
        }

        return cold;
    }

    private static bool IsValid(ExtractorCacheEnvelope envelope, string keyHash, ExtractorCacheKey key)
        => string.Equals(envelope.CacheKeyHash, keyHash, StringComparison.Ordinal)
           && string.Equals(envelope.CacheKey.ComputeHash(), keyHash, StringComparison.Ordinal)
           && string.Equals(envelope.CacheKey.ToCanonicalJson(), key.ToCanonicalJson(), StringComparison.Ordinal);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }

    private static string GetDefaultSharedCacheRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "WrapGod", "cache", "extractor");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
            return Path.Combine(xdg, "wrapgod", "extractor");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".cache", "wrapgod", "extractor");
    }
}
