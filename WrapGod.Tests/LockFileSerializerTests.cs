using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Lockfile serialization")]
public sealed class LockFileSerializerTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Lockfile serialize/deserialize roundtrip")]
    [Fact]
    public Task Lockfile_Roundtrips()
        => Given("a lockfile", () => new WrapGodLockFile
            {
                Sources = [new LockSourceEntry
                {
                    PackageId = "Newtonsoft.Json",
                    Version = "13.0.3",
                    SourceFeed = "https://api.nuget.org/v3/index.json",
                    PackageSha256 = new string('a', 64),
                    TargetFramework = "net8.0",
                    DllRelativePath = "lib/net8.0/Newtonsoft.Json.dll",
                    DllSha256 = new string('b', 64)
                }]
            })
            .When("serialized and deserialized", lf => LockFileSerializer.Deserialize(LockFileSerializer.Serialize(lf)))
            .Then("identity is preserved", parsed => parsed is not null && parsed.Sources.Count == 1 && parsed.Sources[0].PackageId == "Newtonsoft.Json")
            .AssertPassed();
}
