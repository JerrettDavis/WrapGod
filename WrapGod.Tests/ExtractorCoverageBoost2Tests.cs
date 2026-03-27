using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Extractor coverage boost 2: RfcExtractorCache, MultiVersionExtractor, NuGetExtractor multi-version")]
public sealed class ExtractorCoverageBoost2Tests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static readonly string CoreLibPath = typeof(object).Assembly.Location;

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wrapgod-ecb2-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RfcExtractorCache via AssemblyExtractor.ExtractWithCache
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("RfcExtractorCache: cache hit with index and payload files")]
    [Fact]
    public Task RfcCache_HitWithIndexAndPayload()
    {
        var cacheDir = CreateTempDir();
        var indexDir = Path.Combine(cacheDir, "index");
        try
        {
            var options = new ExtractorCacheOptions
            {
                Enabled = true,
                SharedCacheRoot = cacheDir,
                ProjectCacheIndexRoot = indexDir,
            };

            // First call: cold extraction, writes both index and payload
            var m1 = AssemblyExtractor.ExtractWithCache(CoreLibPath, options);

            // Verify files were created
            var payloadFiles = Directory.GetFiles(cacheDir, "*.manifest.json");
            var indexFiles = Directory.GetFiles(indexDir, "*.index.json");

            // Second call: should hit cache (both index and payload exist)
            var m2 = AssemblyExtractor.ExtractWithCache(CoreLibPath, options);

            return Given("two extractions via RfcExtractorCache", () => (m1, m2, payloadFiles.Length, indexFiles.Length))
                .Then("first extraction produces manifest", t => t.m1.Types.Count > 0)
                .And("payload file was created", t => t.Item3 > 0)
                .And("index file was created", t => t.Item4 > 0)
                .And("second extraction produces same assembly name", t =>
                    t.m1.Assembly.Name == t.m2.Assembly.Name)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    [Scenario("RfcExtractorCache: payload exists but index doesn't (reconstructs index)")]
    [Fact]
    public Task RfcCache_PayloadExistsNoIndex()
    {
        var cacheDir = CreateTempDir();
        var indexDir = Path.Combine(cacheDir, "index");
        try
        {
            var options = new ExtractorCacheOptions
            {
                Enabled = true,
                SharedCacheRoot = cacheDir,
                ProjectCacheIndexRoot = indexDir,
            };

            // First: populate cache
            AssemblyExtractor.ExtractWithCache(CoreLibPath, options);

            // Delete index files but keep payload
            if (Directory.Exists(indexDir))
                Directory.Delete(indexDir, true);

            // Second call: payload exists but no index — should reconstruct
            var m2 = AssemblyExtractor.ExtractWithCache(CoreLibPath, options);

            // Verify index was reconstructed
            var indexFiles = Directory.Exists(indexDir)
                ? Directory.GetFiles(indexDir, "*.index.json")
                : Array.Empty<string>();

            return Given("extraction with missing index but existing payload", () => (m2, indexFiles.Length))
                .Then("extraction succeeds", t => t.m2.Types.Count > 0)
                .And("index was reconstructed", t => t.Item2 > 0)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    [Scenario("RfcExtractorCache: corrupt payload triggers re-extraction")]
    [Fact]
    public Task RfcCache_CorruptPayload()
    {
        var cacheDir = CreateTempDir();
        var indexDir = Path.Combine(cacheDir, "index");
        try
        {
            var options = new ExtractorCacheOptions
            {
                Enabled = true,
                SharedCacheRoot = cacheDir,
                ProjectCacheIndexRoot = indexDir,
            };

            // Populate cache
            AssemblyExtractor.ExtractWithCache(CoreLibPath, options);

            // Corrupt payload files
            foreach (var file in Directory.GetFiles(cacheDir, "*.manifest.json"))
            {
                File.WriteAllText(file, "{{corrupt json");
            }

            // Re-extract should handle corruption gracefully
            var m = AssemblyExtractor.ExtractWithCache(CoreLibPath, options);

            return Given("extraction after corrupted payload", () => m)
                .Then("still produces valid manifest", manifest => manifest.Types.Count > 0)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MultiVersionExtractor via ExtractWithCache
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("MultiVersionExtractor.ExtractWithCache: single version")]
    [Fact]
    public Task MultiVersionExtractor_ExtractWithCache_SingleVersion()
    {
        var cacheDir = CreateTempDir();
        try
        {
            var options = new ExtractorCacheOptions
            {
                Enabled = true,
                SharedCacheRoot = cacheDir,
                ProjectCacheIndexRoot = Path.Combine(cacheDir, "index"),
            };

            var versions = new List<MultiVersionExtractor.VersionInput>
            {
                new("1.0", CoreLibPath),
            };

            var result = MultiVersionExtractor.ExtractWithCache(versions, options);

            return Given("multi-version extraction with cache on one version", () => result)
                .Then("merged manifest has types", r => r.MergedManifest.Types.Count > 0)
                .And("diff has one version", r => r.Diff.Versions.Count == 1)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    [Scenario("MultiVersionExtractor.ExtractWithCache: cache disabled falls back")]
    [Fact]
    public Task MultiVersionExtractor_ExtractWithCache_Disabled()
    {
        var options = new ExtractorCacheOptions { Enabled = false };
        var versions = new List<MultiVersionExtractor.VersionInput>
        {
            new("1.0", CoreLibPath),
        };

        var result = MultiVersionExtractor.ExtractWithCache(versions, options);

        return Given("multi-version extract with cache disabled", () => result)
            .Then("still produces result", r => r.MergedManifest.Types.Count > 0)
            .AssertPassed();
    }

    [Scenario("MultiVersionExtractor.ExtractWithCache: empty versions throws")]
    [Fact]
    public Task MultiVersionExtractor_ExtractWithCache_EmptyThrows()
        => Given("empty versions", () => true)
            .Then("ExtractWithCache throws", _ =>
            {
                try
                {
                    MultiVersionExtractor.ExtractWithCache(new List<MultiVersionExtractor.VersionInput>());
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            })
            .AssertPassed();

    [Scenario("MultiVersionExtractor.ExtractWithCache: cache hit on second call")]
    [Fact]
    public Task MultiVersionExtractor_ExtractWithCache_CacheHit()
    {
        var cacheDir = CreateTempDir();
        try
        {
            var options = new ExtractorCacheOptions
            {
                Enabled = true,
                SharedCacheRoot = cacheDir,
                ProjectCacheIndexRoot = Path.Combine(cacheDir, "index"),
            };

            var versions = new List<MultiVersionExtractor.VersionInput>
            {
                new("1.0", CoreLibPath),
            };

            // First call: cold
            var r1 = MultiVersionExtractor.ExtractWithCache(versions, options);
            // Second call: should hit cache
            var r2 = MultiVersionExtractor.ExtractWithCache(versions, options);

            return Given("two multi-version extractions", () => (r1, r2))
                .Then("both produce manifests", t =>
                    t.r1.MergedManifest.Types.Count > 0 && t.r2.MergedManifest.Types.Count > 0)
                .And("assembly names match", t =>
                    t.r1.MergedManifest.Assembly.Name == t.r2.MergedManifest.Assembly.Name)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    [Scenario("MultiVersionExtractor: Merge with empty list throws")]
    [Fact]
    public Task MultiVersionExtractor_Merge_EmptyThrows()
        => Given("empty manifest list", () => true)
            .Then("Merge throws", _ =>
            {
                try
                {
                    MultiVersionExtractor.Merge(new List<(string, ApiManifest)>());
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            })
            .AssertPassed();

    [Scenario("MultiVersionExtractor.Extract: empty list throws")]
    [Fact]
    public Task MultiVersionExtractor_Extract_EmptyThrows()
        => Given("empty versions", () => true)
            .Then("Extract throws", _ =>
            {
                try
                {
                    MultiVersionExtractor.Extract(new List<MultiVersionExtractor.VersionInput>());
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            })
            .AssertPassed();

    [Scenario("MultiVersionExtractor.ExtractWithCache: corrupt multi-version cache")]
    [Fact]
    public Task MultiVersionExtractor_CorruptCache()
    {
        var cacheDir = CreateTempDir();
        try
        {
            var options = new ExtractorCacheOptions
            {
                Enabled = true,
                SharedCacheRoot = cacheDir,
                ProjectCacheIndexRoot = Path.Combine(cacheDir, "index"),
            };

            var versions = new List<MultiVersionExtractor.VersionInput>
            {
                new("1.0", CoreLibPath),
            };

            // First call populates
            MultiVersionExtractor.ExtractWithCache(versions, options);

            // Corrupt multi-version cache files
            var mvDir = Path.Combine(cacheDir, "multiversion");
            if (Directory.Exists(mvDir))
            {
                foreach (var file in Directory.GetFiles(mvDir, "*.multiversion.json"))
                {
                    File.WriteAllText(file, "{{broken");
                }
            }

            // Re-extract should handle gracefully
            var result = MultiVersionExtractor.ExtractWithCache(versions, options);

            return Given("re-extraction after corrupted multi-version cache", () => result)
                .Then("still produces valid result", r => r.MergedManifest.Types.Count > 0)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  NuGetExtractor.ExtractMultiVersionAsync
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("NuGetExtractor.ExtractMultiVersionAsync: empty versions throws")]
    [Fact]
    public Task NuGetExtractor_MultiVersion_EmptyThrows()
        => Given("a NuGet extractor", () => new NuGetExtractor())
            .Then("ExtractMultiVersionAsync with empty versions throws", (Func<NuGetExtractor, Task<bool>>)(async ext =>
            {
                try
                {
                    await ext.ExtractMultiVersionAsync("Pkg", new List<string>());
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            }))
            .AssertPassed();

    [Scenario("NuGetExtractor: default constructor works")]
    [Fact]
    public Task NuGetExtractor_DefaultConstructor()
        => Given("a NuGet extractor with default resolver", () => new NuGetExtractor())
            .Then("it is not null", ext => ext is not null)
            .AssertPassed();

    [Scenario("NuGetExtractor: custom resolver constructor")]
    [Fact]
    public Task NuGetExtractor_CustomResolver()
    {
        var resolver = new NuGetPackageResolver(CreateTempDir());
        return Given("a NuGet extractor with custom resolver", () => new NuGetExtractor(resolver))
            .Then("it is not null", ext => ext is not null)
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ExtractorCacheKey: canonical JSON with custom options
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("ExtractorCacheKey: canonical JSON includes custom options")]
    [Fact]
    public Task CacheKey_CanonicalJsonWithCustom()
    {
        var options = new ExtractorCacheOptions
        {
            Custom = new Dictionary<string, string?>
            {
                ["alpha"] = "one",
                ["beta"] = null,
            },
        };
        var key = ExtractorCacheKey.CreateForAssembly(CoreLibPath, options);
        var json = key.ToCanonicalJson();

        return Given("a cache key with custom options", () => json)
            .Then("JSON contains alpha", j => j.Contains("\"alpha\":\"one\"", StringComparison.Ordinal))
            .And("JSON contains beta as null", j => j.Contains("\"beta\":null", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("ExtractorCacheKey properties")]
    [Fact]
    public Task CacheKey_Properties()
    {
        var key = ExtractorCacheKey.CreateForAssembly(CoreLibPath, ExtractorCacheOptions.Default);

        return Given("a cache key for core lib", () => key)
            .Then("KeySchema is set", k => k.KeySchema == "wg.extractor.cache.v1")
            .And("ManifestSchema is set", k => k.ManifestSchema == "wrapgod.manifest.v1")
            .And("ExtractorAlgoVersion is set", k => k.ExtractorAlgoVersion == "1")
            .And("Source.Sha256 is not empty", k => !string.IsNullOrEmpty(k.Source.Sha256))
            .And("Source.Mvid is not empty", k => !string.IsNullOrEmpty(k.Source.Mvid))
            .And("Source.TargetFramework is null", k => k.Source.TargetFramework is null)
            .And("Options.PublicOnly is true", k => k.Options.PublicOnly)
            .And("Options.IncludeObsoleteDetails is false", k => !k.Options.IncludeObsoleteDetails)
            .AssertPassed();
    }
}
