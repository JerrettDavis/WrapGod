using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("NuGet package extraction")]
public sealed class NuGetExtractorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static readonly string TestCacheRoot =
        Path.Combine(Path.GetTempPath(), "wrapgod-test-cache", Guid.NewGuid().ToString("N"));

    private static readonly byte[] MzHeaderStub = [0x4D, 0x5A];
    private static readonly string[] MockTfms = ["netstandard2.0", "netstandard2.1", "net8.0"];

    private static NuGetPackageResolver CreateResolver() => new(TestCacheRoot);

    private static Task<string> ResolveNewtonsoftJson()
    {
        var resolver = CreateResolver();
        return resolver.ResolveAsync("Newtonsoft.Json", "13.0.3");
    }

    private static Task<ApiManifest> ExtractNewtonsoftJson()
    {
        var resolver = CreateResolver();
        var extractor = new NuGetExtractor(resolver);
        return extractor.ExtractFromPackageAsync("Newtonsoft.Json", "13.0.3");
    }

    // ── Integration Scenarios (require network) ─────────────────────

    [Scenario("Resolve a known public NuGet package")]
    [Fact]
    [Trait("Category", "Integration")]
    public Task Resolve_KnownPackage_ReturnsDllPath()
        => Given("a NuGet package resolver for Newtonsoft.Json 13.0.3", ResolveNewtonsoftJson)
            .Then("the resolved path exists", path => File.Exists(path))
            .And("the resolved path is a DLL", path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .And("the DLL filename contains the package name", path =>
                Path.GetFileNameWithoutExtension(path)
                    .Equals("Newtonsoft.Json", StringComparison.OrdinalIgnoreCase))
            .AssertPassed();

    [Scenario("Extract a manifest from a NuGet package")]
    [Fact]
    [Trait("Category", "Integration")]
    public Task Extract_KnownPackage_ProducesManifest()
        => Given("an extracted manifest from Newtonsoft.Json 13.0.3", ExtractNewtonsoftJson)
            .Then("the manifest is not null", manifest => manifest is not null)
            .And("the manifest contains types", manifest => manifest.Types.Count > 0)
            .And("the assembly name is Newtonsoft.Json", manifest =>
                manifest.Assembly.Name == "Newtonsoft.Json")
            .AssertPassed();

    [Scenario("Cache hit avoids re-download")]
    [Fact]
    [Trait("Category", "Integration")]
    public Task CacheHit_AvoidsRedownload()
        => Given("two resolutions of the same package from the same cache", (Func<Task<(string Path1, string Path2)>>)(async () =>
            {
                var resolver = CreateResolver();
                var path1 = await resolver.ResolveAsync("Newtonsoft.Json", "13.0.3");
                var path2 = await resolver.ResolveAsync("Newtonsoft.Json", "13.0.3");
                return (Path1: path1, Path2: path2);
            }))
            .Then("both paths are identical", result => result.Path1 == result.Path2)
            .And("the file still exists", result => File.Exists(result.Path1))
            .AssertPassed();

    // ── Unit Scenarios (no network) ─────────────────────────────────

    [Scenario("TFM selection picks best match from available folders")]
    [Fact]
    public Task TfmSelection_PicksBestMatch()
        => Given("a mock package directory with multiple TFMs", () =>
            {
                var resolver = CreateResolver();
                var pkgDir = resolver.GetPackageDirectory("MockPkg", "1.0.0");

                // Create mock lib structure with multiple TFMs.
                foreach (var tfm in MockTfms)
                {
                    var tfmDir = Path.Combine(pkgDir, "lib", tfm);
                    Directory.CreateDirectory(tfmDir);
                    File.WriteAllBytes(Path.Combine(tfmDir, "MockPkg.dll"), MzHeaderStub);
                }

                // Write extraction marker.
                File.WriteAllText(Path.Combine(pkgDir, ".extracted"), "done");
                return resolver;
            })
            .Then("ResolveAsync selects net8.0", (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
            {
                var path = await resolver.ResolveAsync("MockPkg", "1.0.0");
                return path.Contains("net8.0", StringComparison.OrdinalIgnoreCase);
            }))
            .AssertPassed();

    [Scenario("Invalid package throws InvalidOperationException")]
    [Fact]
    [Trait("Category", "Integration")]
    public Task InvalidPackage_Throws()
        => Given("a resolver for a non-existent package", () => CreateResolver())
            .Then("resolving throws InvalidOperationException", (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
            {
                try
                {
                    await resolver.ResolveAsync("NonExistent_Package_ZZZ_12345", "99.99.99");
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            }))
            .AssertPassed();
}
