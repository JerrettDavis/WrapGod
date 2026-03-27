using WrapGod.Extractor;
using WrapGod.Manifest;

namespace WrapGod.Tests;

public class ExtractorTests
{
    private static readonly string CoreLibPath = typeof(object).Assembly.Location;

    [Fact]
    public void Extract_CoreLib_ProducesNonEmptyManifest()
    {
        var manifest = AssemblyExtractor.Extract(CoreLibPath);

        Assert.NotNull(manifest);
        Assert.NotEmpty(manifest.Types);
        Assert.NotNull(manifest.SourceHash);
        Assert.NotEmpty(manifest.SourceHash);
    }

    [Fact]
    public void Extract_CoreLib_AssemblyIdentityIsPopulated()
    {
        var manifest = AssemblyExtractor.Extract(CoreLibPath);

        Assert.False(string.IsNullOrEmpty(manifest.Assembly.Name));
        Assert.False(string.IsNullOrEmpty(manifest.Assembly.Version));
    }

    [Fact]
    public void Extract_CoreLib_TypesHaveStableIds()
    {
        var manifest = AssemblyExtractor.Extract(CoreLibPath);

        foreach (var type in manifest.Types)
        {
            Assert.False(string.IsNullOrEmpty(type.StableId),
                $"Type {type.FullName} has no stable ID.");
        }
    }

    [Fact]
    public void Extract_CoreLib_MembersHaveStableIds()
    {
        var manifest = AssemblyExtractor.Extract(CoreLibPath);

        var members = manifest.Types.SelectMany(t => t.Members).Take(200).ToList();
        Assert.NotEmpty(members);

        foreach (var member in members)
        {
            Assert.False(string.IsNullOrEmpty(member.StableId),
                $"Member {member.Name} has no stable ID.");
        }
    }

    [Fact]
    public void Extract_IsDeterministic()
    {
        var manifest1 = AssemblyExtractor.Extract(CoreLibPath);
        var manifest2 = AssemblyExtractor.Extract(CoreLibPath);

        var json1 = ManifestSerializer.Serialize(manifest1);
        var json2 = ManifestSerializer.Serialize(manifest2);

        // The only non-deterministic field is GeneratedAt; normalize it for comparison.
        // Instead, compare the structural content: types, members, hash.
        Assert.Equal(manifest1.SourceHash, manifest2.SourceHash);
        Assert.Equal(manifest1.Types.Count, manifest2.Types.Count);

        for (int i = 0; i < manifest1.Types.Count; i++)
        {
            Assert.Equal(manifest1.Types[i].StableId, manifest2.Types[i].StableId);
            Assert.Equal(manifest1.Types[i].Members.Count, manifest2.Types[i].Members.Count);

            for (int j = 0; j < manifest1.Types[i].Members.Count; j++)
            {
                Assert.Equal(
                    manifest1.Types[i].Members[j].StableId,
                    manifest2.Types[i].Members[j].StableId);
            }
        }
    }

    [Fact]
    public void Extract_CoreLib_TypeIdsAreUnique()
    {
        var manifest = AssemblyExtractor.Extract(CoreLibPath);

        var ids = manifest.Types.Select(t => t.StableId).ToList();
        var uniqueIds = ids.Distinct().ToList();

        Assert.Equal(ids.Count, uniqueIds.Count);
    }

    [Fact]
    public void Extract_MissingAssembly_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(
            () => AssemblyExtractor.Extract("/nonexistent/path/fake.dll"));
    }

    [Fact]
    public void ManifestSerializer_RoundTrips()
    {
        var manifest = AssemblyExtractor.Extract(CoreLibPath);

        var json = ManifestSerializer.Serialize(manifest);
        var deserialized = ManifestSerializer.Deserialize(json);

        Assert.NotNull(deserialized);
        Assert.Equal(manifest.Types.Count, deserialized.Types.Count);
        Assert.Equal(manifest.Assembly.Name, deserialized.Assembly.Name);
    }
}
