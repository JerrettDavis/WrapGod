using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Extractor cache")]
public sealed class ExtractorCacheTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static readonly string CoreLibPath = typeof(object).Assembly.Location;

    private static string CreateTempCacheDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wrapgod-cache-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ── Scenario data ────────────────────────────────────────────────

    internal sealed record CacheMissResult(string CacheDir, ExtractorCache Cache, ApiManifest Manifest);

    private static CacheMissResult PerformCacheMissExtraction()
    {
        var cacheDir = CreateTempCacheDir();
        var cache = new ExtractorCache(cacheDir);

        // First call — cache is empty, should extract and store
        var manifest = AssemblyExtractor.Extract(CoreLibPath, useCache: true, cache);

        return new CacheMissResult(cacheDir, cache, manifest);
    }

    internal sealed record CacheHitResult(string CacheDir, ApiManifest FirstManifest, ApiManifest SecondManifest);

    private static CacheHitResult PerformCacheHitExtraction()
    {
        var cacheDir = CreateTempCacheDir();
        var cache = new ExtractorCache(cacheDir);

        // First call — populates cache
        var first = AssemblyExtractor.Extract(CoreLibPath, useCache: true, cache);

        // Second call — should return from cache
        var second = AssemblyExtractor.Extract(CoreLibPath, useCache: true, cache);

        return new CacheHitResult(cacheDir, first, second);
    }

    internal sealed record ModifiedAssemblyResult(bool CacheReturnedNullAfterModification);

    private static ModifiedAssemblyResult SimulateModifiedAssembly()
    {
        var cacheDir = CreateTempCacheDir();
        try
        {
            var cache = new ExtractorCache(cacheDir);

            // Store a manifest with the current assembly hash
            var manifest = AssemblyExtractor.Extract(CoreLibPath);
            cache.Store(CoreLibPath, manifest);

            // Tamper with the cache file to simulate a different file hash
            var cacheFiles = Directory.GetFiles(cacheDir, "*.json");
            foreach (var file in cacheFiles)
            {
                var content = File.ReadAllText(file);
                content = content.Replace(manifest.SourceHash!, "0000000000000000000000000000000000000000000000000000000000000000");
                File.WriteAllText(file, content);
            }

            // Now TryGetCached should return null because hash doesn't match
            var result = cache.TryGetCached(CoreLibPath);

            return new ModifiedAssemblyResult(result is null);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    internal sealed record InvalidateResult(bool EntryExistedBefore, bool EntryGoneAfter);

    private static InvalidateResult PerformInvalidation()
    {
        var cacheDir = CreateTempCacheDir();
        try
        {
            var cache = new ExtractorCache(cacheDir);
            var manifest = AssemblyExtractor.Extract(CoreLibPath);
            cache.Store(CoreLibPath, manifest);

            var existedBefore = cache.TryGetCached(CoreLibPath) is not null;
            cache.Invalidate(CoreLibPath);
            var goneAfter = cache.TryGetCached(CoreLibPath) is null;

            return new InvalidateResult(existedBefore, goneAfter);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("Cache miss extracts and stores")]
    [Fact]
    public Task CacheMiss_ExtractsAndStores()
        => Given("a fresh cache and an assembly to extract", PerformCacheMissExtraction)
            .Then("a manifest is returned", result => result.Manifest is not null)
            .And("the manifest contains types", result => result.Manifest.Types.Count > 0)
            .And("a cache file was created", result =>
                Directory.GetFiles(result.CacheDir, "*.json").Length > 0)
            .And("the cached entry can be retrieved", result =>
                result.Cache.TryGetCached(CoreLibPath) is not null)
            .AssertPassed();

    [Scenario("Cache hit returns stored manifest without re-extraction")]
    [Fact]
    public Task CacheHit_ReturnsStoredManifest()
        => Given("two consecutive extractions with cache enabled", PerformCacheHitExtraction)
            .Then("both manifests are non-null", result =>
                result.FirstManifest is not null && result.SecondManifest is not null)
            .And("type counts match", result =>
                result.FirstManifest.Types.Count == result.SecondManifest.Types.Count)
            .And("assembly names match", result =>
                result.FirstManifest.Assembly.Name == result.SecondManifest.Assembly.Name)
            .And("source hashes match", result =>
                result.FirstManifest.SourceHash == result.SecondManifest.SourceHash)
            .AssertPassed();

    [Scenario("Modified assembly invalidates cache (different hash)")]
    [Fact]
    public Task ModifiedAssembly_InvalidatesCache()
        => Given("a cached entry with a tampered file hash", SimulateModifiedAssembly)
            .Then("the cache returns null for the modified assembly", result =>
                result.CacheReturnedNullAfterModification)
            .AssertPassed();

    [Scenario("Invalidate removes cached entry")]
    [Fact]
    public Task Invalidate_RemovesCachedEntry()
        => Given("an invalidation of a cached entry", PerformInvalidation)
            .Then("the entry existed before invalidation", result =>
                result.EntryExistedBefore)
            .And("the entry is gone after invalidation", result =>
                result.EntryGoneAfter)
            .AssertPassed();
}
