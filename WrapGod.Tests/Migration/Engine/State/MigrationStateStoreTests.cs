using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.State;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.State;

[Feature("MigrationStateStore: load/save with real temp file system")]
public sealed class MigrationStateStoreTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string TempSchemaPath()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "schema.json");
    }

    private static MigrationState SampleState() => new()
    {
        Schema = "schema.json",
        SchemaHash = "sha256:aabbcc",
        StartedAt = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
        LastRunAt = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
        Summary = new MigrationStateSummary { TotalRules = 2, Applied = 1, Skipped = 1 },
        Applied = [new AppliedRewrite("r-1", "file.cs", 5, "old", "new")],
        Skipped = [new SkippedRewrite("r-2", "file.cs", 8, "no match")],
    };

    // ══════════════════════════════════════════════════════════════════════════
    // Group: happy
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Happy-01: Load returns null when state file does not exist")]
    [Fact]
    public Task Load_NonExistentFile_ReturnsNull() =>
        Given("schema path where no state file exists", () =>
            MigrationStateStore.Load(TempSchemaPath()))
        .Then("result is null", r => r == null)
        .AssertPassed();

    [Scenario("Happy-02: Save then Load round-trips state correctly")]
    [Fact]
    public Task SaveThenLoad_RoundTrips() =>
        Given("schema path; Save called with SampleState; Load returns the state", () =>
        {
            var path = TempSchemaPath();
            MigrationStateStore.Save(path, SampleState());
            return MigrationStateStore.Load(path);
        })
        .Then("loaded state is not null", s => s != null)
        .And("SchemaHash matches", s => s!.SchemaHash == "sha256:aabbcc")
        .And("Applied has 1 entry", s => s!.Applied.Count == 1)
        .And("Applied[0].RuleId is r-1", s => s!.Applied[0].RuleId == "r-1")
        .And("Skipped has 1 entry", s => s!.Skipped.Count == 1)
        .AssertPassed();

    [Scenario("Happy-03: Save creates parent directory if it does not exist")]
    [Fact]
    public Task Save_CreatesParentDirectory() =>
        Given("a schema path in a non-existent subdirectory; Save called", () =>
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "sub", "dir");
            var schemaPath = Path.Combine(root, "schema.json");
            MigrationStateStore.Save(schemaPath, SampleState());
            return MigrationStateStore.GetStatePath(schemaPath);
        })
        .Then("state file exists", p => File.Exists(p))
        .AssertPassed();

    [Scenario("Happy-04: Save writes atomically — no .tmp file left behind")]
    [Fact]
    public Task Save_AtomicWrite_NoTmpFileLeft() =>
        Given("schema path; Save completes", () =>
        {
            var path = TempSchemaPath();
            MigrationStateStore.Save(path, SampleState());
            return MigrationStateStore.GetStatePath(path);
        })
        .Then("state file exists", p => File.Exists(p))
        .And("no .tmp file left beside the state file", p => !File.Exists(p + ".tmp"))
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Group: sad
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Sad-01: Load returns null for corrupt state file")]
    [Fact]
    public Task Load_CorruptStateFile_ReturnsNull() =>
        Given("state file containing invalid JSON; Load called", () =>
        {
            var path = TempSchemaPath();
            var statePath = MigrationStateStore.GetStatePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            File.WriteAllText(statePath, "{ this is NOT valid json ]");
            return MigrationStateStore.Load(path);
        })
        .Then("result is null", r => r == null)
        .AssertPassed();

    [Scenario("Sad-02: Load returns null for empty state file")]
    [Fact]
    public Task Load_EmptyStateFile_ReturnsNull() =>
        Given("state file that is empty; Load called", () =>
        {
            var path = TempSchemaPath();
            var statePath = MigrationStateStore.GetStatePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            File.WriteAllText(statePath, string.Empty);
            return MigrationStateStore.Load(path);
        })
        .Then("result is null", r => r == null)
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Group: edge
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Edge-01: Overwriting existing state file saves the new version")]
    [Fact]
    public Task Save_Overwrite_PersistsNewVersion() =>
        Given("state file already saved with 'sha256:aabbcc'; resaved with 'sha256:updated'", () =>
        {
            var path = TempSchemaPath();
            MigrationStateStore.Save(path, SampleState());
            var updated = SampleState();
            updated.SchemaHash = "sha256:updated";
            MigrationStateStore.Save(path, updated);
            return MigrationStateStore.Load(path);
        })
        .Then("loaded state is not null", s => s != null)
        .And("SchemaHash is 'sha256:updated'", s => s!.SchemaHash == "sha256:updated")
        .AssertPassed();
}
