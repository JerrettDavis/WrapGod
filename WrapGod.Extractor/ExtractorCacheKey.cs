using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;

namespace WrapGod.Extractor;

public sealed class ExtractorCacheKey
{
    public required string KeySchema { get; init; }
    public required string ManifestSchema { get; init; }
    public required string ExtractorVersion { get; init; }
    public required string ExtractorAlgoVersion { get; init; }
    public required ExtractorCacheKeySource Source { get; init; }
    public required ExtractorCacheKeyOptions Options { get; init; }

    public static ExtractorCacheKey CreateForAssembly(string assemblyPath, ExtractorCacheOptions options)
    {
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException("Assembly file not found.", assemblyPath);

        return new ExtractorCacheKey
        {
            KeySchema = "wg.extractor.cache.v1",
            ManifestSchema = "wrapgod.manifest.v1",
            ExtractorVersion = GetExtractorVersion(),
            ExtractorAlgoVersion = "1",
            Source = new ExtractorCacheKeySource
            {
                Sha256 = ComputeFileHash(assemblyPath),
                Mvid = ReadModuleVersionId(assemblyPath),
                TargetFramework = null,
            },
            Options = new ExtractorCacheKeyOptions
            {
                PublicOnly = options.PublicOnly,
                IncludeObsoleteDetails = options.IncludeObsoleteDetails,
                Custom = options.Custom.OrderBy(k => k.Key, StringComparer.Ordinal)
                    .ToDictionary(k => k.Key, v => v.Value, StringComparer.Ordinal),
            },
        };
    }

    public string ComputeHash()
    {
        var canonical = ToCanonicalJson();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public string ToCanonicalJson()
    {
        var customCanonical = string.Join(",", Options.Custom.OrderBy(k => k.Key, StringComparer.Ordinal).Select(kv => $"\"{kv.Key}\":{(kv.Value is null ? "null" : $"\"{kv.Value}\"")}"));
        var tfm = Source.TargetFramework is null ? "null" : $"\"{Source.TargetFramework}\"";

        return $"{{\"keySchema\":\"{KeySchema}\",\"manifestSchema\":\"{ManifestSchema}\",\"extractorVersion\":\"{ExtractorVersion}\",\"extractorAlgoVersion\":\"{ExtractorAlgoVersion}\",\"source\":{{\"sha256\":\"{Source.Sha256}\",\"mvid\":\"{Source.Mvid}\",\"targetFramework\":{tfm}}},\"options\":{{\"publicOnly\":{Options.PublicOnly.ToString().ToLowerInvariant()},\"includeObsoleteDetails\":{Options.IncludeObsoleteDetails.ToString().ToLowerInvariant()},\"custom\":{{{customCanonical}}}}}}}";
    }

    private static string GetExtractorVersion()
    {
        var assembly = typeof(AssemblyExtractor).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
    }

    private static string ReadModuleVersionId(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var metadataReader = peReader.GetMetadataReader();
        var moduleDef = metadataReader.GetModuleDefinition();
        var mvidHandle = moduleDef.Mvid;
        return mvidHandle.IsNil ? string.Empty : metadataReader.GetGuid(mvidHandle).ToString("D").ToLowerInvariant();
    }

    private static string ComputeFileHash(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public sealed class ExtractorCacheKeySource
{
    public required string Sha256 { get; init; }
    public required string Mvid { get; init; }
    public string? TargetFramework { get; init; }
}

public sealed class ExtractorCacheKeyOptions
{
    public required bool PublicOnly { get; init; }
    public required bool IncludeObsoleteDetails { get; init; }
    public required IReadOnlyDictionary<string, string?> Custom { get; init; }
}
