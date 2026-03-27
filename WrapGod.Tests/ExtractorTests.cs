using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Assembly extraction")]
public partial class ExtractorTests : TinyBddXunitBase
{
    private static readonly string CoreLibPath = typeof(object).Assembly.Location;

    public ExtractorTests(ITestOutputHelper output) : base(output) { }

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

    [Scenario("Extracting CoreLib produces a non-empty manifest")]
    [Fact]
    public async Task Extract_CoreLib_ProducesNonEmptyManifest()
    {
        await Flow.Given("a CoreLib assembly path", ExtractCoreLib)
            .Then("the manifest is not null", manifest => Assert.NotNull(manifest))
            .And("the manifest contains types", manifest => Assert.NotEmpty(manifest.Types))
            .And("the source hash is populated", manifest =>
            {
                Assert.NotNull(manifest.SourceHash);
                Assert.NotEmpty(manifest.SourceHash);
            })
            .AssertPassed();
    }

    [Scenario("Extracting CoreLib populates assembly identity")]
    [Fact]
    public async Task Extract_CoreLib_AssemblyIdentityIsPopulated()
    {
        await Flow.Given("a CoreLib manifest", ExtractCoreLib)
            .Then("assembly name is populated", manifest =>
                Assert.False(string.IsNullOrEmpty(manifest.Assembly.Name)))
            .And("assembly version is populated", manifest =>
                Assert.False(string.IsNullOrEmpty(manifest.Assembly.Version)))
            .AssertPassed();
    }

    [Scenario("Extracted types have stable IDs")]
    [Fact]
    public async Task Extract_CoreLib_TypesHaveStableIds()
    {
        await Flow.Given("a CoreLib manifest", ExtractCoreLib)
            .Then("every type has a stable ID", manifest =>
            {
                foreach (var type in manifest.Types)
                {
                    Assert.False(string.IsNullOrEmpty(type.StableId),
                        $"Type {type.FullName} has no stable ID.");
                }
            })
            .AssertPassed();
    }

    [Scenario("Extracted members have stable IDs")]
    [Fact]
    public async Task Extract_CoreLib_MembersHaveStableIds()
    {
        await Flow.Given("a CoreLib manifest", ExtractCoreLib)
            .Then("every member has a stable ID", manifest =>
            {
                var members = manifest.Types.SelectMany(t => t.Members).Take(200).ToList();
                Assert.NotEmpty(members);

                foreach (var member in members)
                {
                    Assert.False(string.IsNullOrEmpty(member.StableId),
                        $"Member {member.Name} has no stable ID.");
                }
            })
            .AssertPassed();
    }

    [Scenario("Extraction is deterministic across runs")]
    [Fact]
    public async Task Extract_IsDeterministic()
    {
        await Flow.Given("two extractions of the same assembly", ExtractTwice)
            .Then("source hashes match", pair =>
                Assert.Equal(pair.First.SourceHash, pair.Second.SourceHash))
            .And("type counts match", pair =>
                Assert.Equal(pair.First.Types.Count, pair.Second.Types.Count))
            .And("all type stable IDs and member stable IDs match", pair =>
            {
                for (int i = 0; i < pair.First.Types.Count; i++)
                {
                    Assert.Equal(pair.First.Types[i].StableId, pair.Second.Types[i].StableId);
                    Assert.Equal(pair.First.Types[i].Members.Count, pair.Second.Types[i].Members.Count);

                    for (int j = 0; j < pair.First.Types[i].Members.Count; j++)
                    {
                        Assert.Equal(
                            pair.First.Types[i].Members[j].StableId,
                            pair.Second.Types[i].Members[j].StableId);
                    }
                }
            })
            .AssertPassed();
    }

    [Scenario("Type IDs are unique within an assembly")]
    [Fact]
    public async Task Extract_CoreLib_TypeIdsAreUnique()
    {
        await Flow.Given("a CoreLib manifest", ExtractCoreLib)
            .Then("all type stable IDs are unique", manifest =>
            {
                var ids = manifest.Types.Select(t => t.StableId).ToList();
                var uniqueIds = ids.Distinct().ToList();
                Assert.Equal(ids.Count, uniqueIds.Count);
            })
            .AssertPassed();
    }

    [Scenario("Missing assembly throws FileNotFoundException")]
    [Fact]
    public async Task Extract_MissingAssembly_ThrowsFileNotFoundException()
    {
        await Flow.Given("a non-existent assembly path", () => "/nonexistent/path/fake.dll")
            .Then("extraction throws FileNotFoundException", path =>
                Assert.Throws<FileNotFoundException>(
                    () => AssemblyExtractor.Extract(path)))
            .AssertPassed();
    }

    [Scenario("ManifestSerializer round-trips correctly")]
    [Fact]
    public async Task ManifestSerializer_RoundTrips()
    {
        await Flow.Given("a serialized and deserialized CoreLib manifest", RoundTripSerialize)
            .Then("deserialized manifest is not null", pair =>
                Assert.NotNull(pair.Deserialized))
            .And("type counts match", pair =>
                Assert.Equal(pair.Original.Types.Count, pair.Deserialized.Types.Count))
            .And("assembly names match", pair =>
                Assert.Equal(pair.Original.Assembly.Name, pair.Deserialized.Assembly.Name))
            .AssertPassed();
    }
}
