using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using WrapGod.Manifest;

namespace WrapGod.Extractor;

/// <summary>
/// Incremental cache for <see cref="AssemblyExtractor"/> results.
/// Cache key: assembly path + file hash + extractor version.
/// Storage: JSON files in a configurable cache directory.
/// </summary>
public sealed class ExtractorCache
{
    /// <summary>
    /// Bump this when extractor logic changes to invalidate all cached entries.
    /// </summary>
    internal const string ExtractorVersion = "1";

    private static readonly JsonSerializerOptions CacheJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _cacheDirectory;

    /// <summary>
    /// Creates a new <see cref="ExtractorCache"/> using the specified directory.
    /// </summary>
    /// <param name="cacheDirectory">
    /// Directory where cache files are stored.
    /// Defaults to <c>.wrapgod-cache/</c> under the current working directory.
    /// </param>
    public ExtractorCache(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(Directory.GetCurrentDirectory(), ".wrapgod-cache");
    }

    /// <summary>
    /// Returns a cached <see cref="ApiManifest"/> if the cache entry is still valid
    /// (same file hash and extractor version). Returns <c>null</c> on cache miss.
    /// </summary>
    public ApiManifest? TryGetCached(string assemblyPath)
    {
        var cacheFile = GetCacheFilePath(assemblyPath);

        if (!File.Exists(cacheFile))
            return null;

        try
        {
            var json = File.ReadAllText(cacheFile);
            var entry = JsonSerializer.Deserialize<CacheEntry>(json, CacheJsonOptions);

            if (entry is null)
                return null;

            // Validate extractor version
            if (entry.ExtractorVersion != ExtractorVersion)
                return null;

            // Validate file hash
            var currentHash = ComputeFileHash(assemblyPath);
            if (entry.FileHash != currentHash)
                return null;

            return entry.Manifest;
        }
        catch (JsonException)
        {
            // Corrupted cache entry — treat as miss
            return null;
        }
    }

    /// <summary>
    /// Persists the extraction result to the cache.
    /// </summary>
    public void Store(string assemblyPath, ApiManifest manifest)
    {
        var cacheFile = GetCacheFilePath(assemblyPath);
        Directory.CreateDirectory(_cacheDirectory);

        var entry = new CacheEntry
        {
            AssemblyPath = Path.GetFullPath(assemblyPath),
            FileHash = ComputeFileHash(assemblyPath),
            ExtractorVersion = ExtractorVersion,
            CachedAt = DateTimeOffset.UtcNow,
            Manifest = manifest,
        };

        var json = JsonSerializer.Serialize(entry, CacheJsonOptions);
        File.WriteAllText(cacheFile, json);
    }

    /// <summary>
    /// Removes a cached entry for the given assembly path.
    /// </summary>
    public void Invalidate(string assemblyPath)
    {
        var cacheFile = GetCacheFilePath(assemblyPath);

        if (File.Exists(cacheFile))
            File.Delete(cacheFile);
    }

    private string GetCacheFilePath(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        var hash = ComputeStringHash(fullPath);
        return Path.Combine(_cacheDirectory, $"{hash}.json");
    }

    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeStringHash(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal sealed class CacheEntry
    {
        public string AssemblyPath { get; set; } = string.Empty;
        public string FileHash { get; set; } = string.Empty;
        public string ExtractorVersion { get; set; } = string.Empty;
        public DateTimeOffset CachedAt { get; set; }
        public ApiManifest Manifest { get; set; } = new();
    }
}
