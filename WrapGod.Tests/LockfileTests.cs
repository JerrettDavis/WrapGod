using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Manifest.Lockfile;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("wrapgod.lock.json reproducibility lockfile")]
public sealed class LockfileTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static WrapGodLockfile CreateSampleLockfile() => new()
    {
        Version = 1,
        GeneratedAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero),
        ToolchainVersion = "0.1.0-alpha",
        Sources =
        [
            new LockfileSource
            {
                PackageId = "Newtonsoft.Json",
                Version = "13.0.3",
                Feed = "https://api.nuget.org/v3/index.json",
                FileHash = "sha256:abc123def456",
                Tfm = "netstandard2.0",
                ResolvedPath = "packages/newtonsoft.json/13.0.3/lib/netstandard2.0/Newtonsoft.Json.dll",
            },
            new LockfileSource
            {
                PackageId = "Serilog",
                Version = "4.0.0",
                Feed = "https://api.nuget.org/v3/index.json",
                FileHash = "sha256:789ghi012jkl",
                Tfm = "net8.0",
                ResolvedPath = "packages/serilog/4.0.0/lib/net8.0/Serilog.dll",
            },
        ],
    };

    [Scenario("Write and read lockfile roundtrip preserves all data")]
    [Fact]
    public Task WriteReadRoundtrip() =>
        Given("a sample lockfile serialized and deserialized", () =>
        {
            var original = CreateSampleLockfile();
            var json = WrapGodLockfileWriter.Serialize(original);
            var deserialized = WrapGodLockfileReader.Deserialize(json);
            return (Original: original, Roundtrip: deserialized, Json: json);
        })
        .Then("version is preserved", r => r.Roundtrip.Version == 1)
        .And("generatedAt is preserved", r => r.Roundtrip.GeneratedAt == r.Original.GeneratedAt)
        .And("toolchainVersion is preserved", r => r.Roundtrip.ToolchainVersion == "0.1.0-alpha")
        .And("source count is preserved", r => r.Roundtrip.Sources.Count == 2)
        .And("first source packageId is preserved", r =>
            r.Roundtrip.Sources[0].PackageId == "Newtonsoft.Json")
        .And("first source version is preserved", r =>
            r.Roundtrip.Sources[0].Version == "13.0.3")
        .And("first source feed is preserved", r =>
            r.Roundtrip.Sources[0].Feed == "https://api.nuget.org/v3/index.json")
        .And("first source fileHash is preserved", r =>
            r.Roundtrip.Sources[0].FileHash == "sha256:abc123def456")
        .And("first source tfm is preserved", r =>
            r.Roundtrip.Sources[0].Tfm == "netstandard2.0")
        .And("first source resolvedPath is preserved", r =>
            r.Roundtrip.Sources[0].ResolvedPath ==
                "packages/newtonsoft.json/13.0.3/lib/netstandard2.0/Newtonsoft.Json.dll")
        .And("second source packageId is preserved", r =>
            r.Roundtrip.Sources[1].PackageId == "Serilog")
        .And("JSON is valid", r => r.Json.Contains("\"version\": 1"))
        .AssertPassed();

    [Scenario("Drift detection reports hash change")]
    [Fact]
    public Task DriftDetection_HashChange() =>
        Given("a locked and current lockfile with different hash", () =>
        {
            var current = CreateSampleLockfile();
            current.Sources[0].FileHash = "sha256:CHANGED";
            return DriftDetector.Detect(CreateSampleLockfile(), current);
        })
        .Then("exactly one drift is reported", drifts => drifts.Count == 1)
        .And("drift field is fileHash", drifts => drifts[0].Field == "fileHash")
        .And("expected value is original hash", drifts =>
            drifts[0].Expected == "sha256:abc123def456")
        .And("actual value is changed hash", drifts =>
            drifts[0].Actual == "sha256:CHANGED")
        .And("drift packageId is Newtonsoft.Json", drifts =>
            drifts[0].PackageId == "Newtonsoft.Json")
        .AssertPassed();

    [Scenario("Drift detection reports version change")]
    [Fact]
    public Task DriftDetection_VersionChange() =>
        Given("a locked and current lockfile with different version", () =>
        {
            var current = CreateSampleLockfile();
            current.Sources[1].Version = "5.0.0";
            return DriftDetector.Detect(CreateSampleLockfile(), current);
        })
        .Then("exactly one drift is reported", drifts => drifts.Count == 1)
        .And("drift field is version", drifts => drifts[0].Field == "version")
        .And("expected value is 4.0.0", drifts => drifts[0].Expected == "4.0.0")
        .And("actual value is 5.0.0", drifts => drifts[0].Actual == "5.0.0")
        .And("drift packageId is Serilog", drifts =>
            drifts[0].PackageId == "Serilog")
        .AssertPassed();

    [Scenario("Drift detection reports added package")]
    [Fact]
    public Task DriftDetection_AddedPackage() =>
        Given("a current lockfile with an extra package", () =>
        {
            var current = CreateSampleLockfile();
            current.Sources.Add(new LockfileSource
            {
                PackageId = "NewPackage",
                Version = "1.0.0",
                Feed = "https://api.nuget.org/v3/index.json",
                FileHash = "sha256:new",
                Tfm = "net8.0",
                ResolvedPath = "packages/newpackage/1.0.0/lib/net8.0/NewPackage.dll",
            });
            return DriftDetector.Detect(CreateSampleLockfile(), current);
        })
        .Then("exactly one drift is reported", drifts => drifts.Count == 1)
        .And("drift field is presence", drifts => drifts[0].Field == "presence")
        .And("drift indicates addition", drifts => drifts[0].Actual == "present")
        .AssertPassed();

    [Scenario("Drift detection reports removed package")]
    [Fact]
    public Task DriftDetection_RemovedPackage() =>
        Given("a current lockfile missing a package", () =>
        {
            var current = CreateSampleLockfile();
            current.Sources.RemoveAt(1);
            return DriftDetector.Detect(CreateSampleLockfile(), current);
        })
        .Then("exactly one drift is reported", drifts => drifts.Count == 1)
        .And("drift field is presence", drifts => drifts[0].Field == "presence")
        .And("drift indicates removal", drifts => drifts[0].Actual == "absent")
        .And("drift packageId is Serilog", drifts => drifts[0].PackageId == "Serilog")
        .AssertPassed();

    [Scenario("No drift when lockfiles match")]
    [Fact]
    public Task NoDrift_WhenIdentical() =>
        Given("two identical lockfiles", () =>
            DriftDetector.Detect(CreateSampleLockfile(), CreateSampleLockfile()))
        .Then("no drifts are reported", drifts => drifts.Count == 0)
        .AssertPassed();

    [Scenario("Reader rejects invalid lockfile version")]
    [Fact]
    public Task Reader_RejectsInvalidVersion() =>
        Given("a lockfile JSON with version 0", () =>
        {
            var lockfile = CreateSampleLockfile();
            lockfile.Version = 0;
            return WrapGodLockfileWriter.Serialize(lockfile);
        })
        .Then("deserialization throws InvalidOperationException", json =>
        {
            try
            {
                WrapGodLockfileReader.Deserialize(json);
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        })
        .AssertPassed();

    [Scenario("Reader rejects source with empty packageId")]
    [Fact]
    public Task Reader_RejectsEmptyPackageId() =>
        Given("a lockfile JSON with empty packageId", () =>
        {
            var lockfile = CreateSampleLockfile();
            lockfile.Sources[0].PackageId = "";
            return WrapGodLockfileWriter.Serialize(lockfile);
        })
        .Then("deserialization throws InvalidOperationException", json =>
        {
            try
            {
                WrapGodLockfileReader.Deserialize(json);
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        })
        .AssertPassed();
}
