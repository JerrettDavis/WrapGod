using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Assembly extraction")]
public sealed class ExtractorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static readonly string CoreLibPath = typeof(object).Assembly.Location;

    private static ApiManifest ExtractCoreLib() => AssemblyExtractor.Extract(CoreLibPath);

    private static SerializerRoundTripResult RoundTripSerialize()
    {
        var manifest = ExtractCoreLib();
        var json = ManifestSerializer.Serialize(manifest);
        var deserialized = ManifestSerializer.Deserialize(json);
        return new SerializerRoundTripResult(manifest, deserialized!);
    }

    internal sealed record SerializerRoundTripResult(ApiManifest Original, ApiManifest Deserialized);

    internal sealed record DeterminismPair(ApiManifest First, ApiManifest Second);

    private static DeterminismPair ExtractTwice() => new(ExtractCoreLib(), ExtractCoreLib());

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("Extracting CoreLib produces a non-empty manifest")]
    [Fact]
    public Task Extract_CoreLib_ProducesNonEmptyManifest()
        => Given("a CoreLib assembly path", ExtractCoreLib)
            .Then("the manifest is not null", manifest => manifest is not null)
            .And("the manifest contains types", manifest => manifest.Types.Count > 0)
            .And("the source hash is populated", manifest =>
                !string.IsNullOrEmpty(manifest.SourceHash))
            .AssertPassed();

    [Scenario("Extracting CoreLib populates assembly identity")]
    [Fact]
    public Task Extract_CoreLib_AssemblyIdentityIsPopulated()
        => Given("a CoreLib manifest", ExtractCoreLib)
            .Then("assembly name is populated", manifest =>
                !string.IsNullOrEmpty(manifest.Assembly.Name))
            .And("assembly version is populated", manifest =>
                !string.IsNullOrEmpty(manifest.Assembly.Version))
            .AssertPassed();

    [Scenario("Extracted types have stable IDs")]
    [Fact]
    public Task Extract_CoreLib_TypesHaveStableIds()
        => Given("a CoreLib manifest", ExtractCoreLib)
            .Then("every type has a stable ID", manifest =>
                manifest.Types.All(t => !string.IsNullOrEmpty(t.StableId)))
            .AssertPassed();

    [Scenario("Extracted members have stable IDs")]
    [Fact]
    public Task Extract_CoreLib_MembersHaveStableIds()
        => Given("a CoreLib manifest", ExtractCoreLib)
            .Then("sampled members all have a stable ID", manifest =>
                manifest.Types
                    .SelectMany(t => t.Members)
                    .Take(200)
                    .All(m => !string.IsNullOrEmpty(m.StableId)))
            .AssertPassed();

    [Scenario("Extraction is deterministic across runs")]
    [Fact]
    public Task Extract_IsDeterministic()
        => Given("two extractions of the same assembly", ExtractTwice)
            .Then("source hashes match", pair =>
                pair.First.SourceHash == pair.Second.SourceHash)
            .And("type counts match", pair =>
                pair.First.Types.Count == pair.Second.Types.Count)
            .And("all type stable IDs match", pair =>
                pair.First.Types.Zip(pair.Second.Types)
                    .All(p => p.First.StableId == p.Second.StableId))
            .And("all member counts match per type", pair =>
                pair.First.Types.Zip(pair.Second.Types)
                    .All(p => p.First.Members.Count == p.Second.Members.Count))
            .And("all member stable IDs match", pair =>
                pair.First.Types.Zip(pair.Second.Types)
                    .All(p => p.First.Members.Zip(p.Second.Members)
                        .All(m => m.First.StableId == m.Second.StableId)))
            .AssertPassed();

    [Scenario("Type IDs are unique within an assembly")]
    [Fact]
    public Task Extract_CoreLib_TypeIdsAreUnique()
        => Given("a CoreLib manifest", ExtractCoreLib)
            .Then("all type stable IDs are unique", manifest =>
                manifest.Types.Select(t => t.StableId).Distinct().Count() == manifest.Types.Count)
            .AssertPassed();

    [Scenario("Missing assembly throws FileNotFoundException")]
    [Fact]
    public Task Extract_MissingAssembly_ThrowsFileNotFoundException()
        => Given("a non-existent assembly path", () => "/nonexistent/path/fake.dll")
            .Then("extraction throws FileNotFoundException", path =>
            {
                try { AssemblyExtractor.Extract(path); return false; }
                catch (FileNotFoundException) { return true; }
            })
            .AssertPassed();

    [Scenario("ManifestSerializer round-trips correctly")]
    [Fact]
    public Task ManifestSerializer_RoundTrips()
        => Given("a serialized and deserialized CoreLib manifest", RoundTripSerialize)
            .Then("deserialized manifest is not null", pair => pair.Deserialized is not null)
            .And("type counts match", pair =>
                pair.Original.Types.Count == pair.Deserialized.Types.Count)
            .And("assembly names match", pair =>
                pair.Original.Assembly.Name == pair.Deserialized.Assembly.Name)
            .AssertPassed();
}
