using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.State;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.State;

[Feature("MigrationState: serialization, hash, and state logic")]
public sealed class MigrationStateTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MigrationState FullState() => new()
    {
        Schema = "/path/to/schema.json",
        SchemaHash = "sha256:abc123",
        StartedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
        LastRunAt = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero),
        Summary = new MigrationStateSummary { TotalRules = 3, Applied = 1, Skipped = 1, Manual = 1 },
        Applied = [new AppliedRewrite("rule-1", "/src/Foo.cs", 10, "OldType", "NewType")],
        Skipped = [new SkippedRewrite("rule-2", "/src/Bar.cs", 5, "no match")],
        Manual  = [new ManualRewrite("rule-3", "Manual step required", ["/src/Baz.cs"])],
    };

    private static MigrationState EmptyState() => new()
    {
        Schema = "schema.json",
        SchemaHash = "sha256:0000",
        StartedAt = DateTimeOffset.UtcNow,
        LastRunAt = DateTimeOffset.UtcNow,
    };

    // ══════════════════════════════════════════════════════════════════════════
    // Group: happy
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Happy-01: full state round-trips through serializer")]
    [Fact]
    public Task Serializer_RoundTrips_FullState() =>
        Given("a fully populated MigrationState serialized to JSON", () =>
        {
            var json = MigrationStateSerializer.Serialize(FullState());
            return MigrationStateSerializer.Deserialize(json);
        })
        .Then("deserialized state is not null", r => r != null)
        .And("Schema matches", r => r!.Schema == "/path/to/schema.json")
        .And("SchemaHash matches", r => r!.SchemaHash == "sha256:abc123")
        .And("Applied has 1 entry", r => r!.Applied.Count == 1)
        .And("Applied[0].RuleId is rule-1", r => r!.Applied[0].RuleId == "rule-1")
        .And("Skipped has 1 entry", r => r!.Skipped.Count == 1)
        .And("Manual has 1 entry", r => r!.Manual.Count == 1)
        .And("Summary.TotalRules == 3", r => r!.Summary.TotalRules == 3)
        .AssertPassed();

    [Scenario("Happy-02: minimal state round-trips through serializer")]
    [Fact]
    public Task Serializer_EmptyState_RoundTrips() =>
        Given("a minimal MigrationState serialized to JSON", () =>
        {
            var json = MigrationStateSerializer.Serialize(EmptyState());
            return MigrationStateSerializer.Deserialize(json);
        })
        .Then("deserialized state is not null", r => r != null)
        .And("Schema matches", r => r!.Schema == "schema.json")
        .And("Applied is empty", r => r!.Applied.Count == 0)
        .And("Skipped is empty", r => r!.Skipped.Count == 0)
        .And("Manual is empty", r => r!.Manual.Count == 0)
        .AssertPassed();

    [Scenario("Happy-03: null JSON returns null from deserializer")]
    [Fact]
    public Task Serializer_NullJson_ReturnsNull() =>
        Given("the JSON literal 'null'", () => MigrationStateSerializer.Deserialize("null"))
        .Then("result is null", r => r == null)
        .AssertPassed();

    [Scenario("Happy-04: schema hash stable across CRLF vs LF line endings")]
    [Fact]
    public Task Hash_LineEndingNormalised() =>
        Given("same content with CRLF and LF line endings hashed", () =>
        (
            crlf: MigrationStateStore.ComputeSchemaHash("line1\r\nline2\r\n"),
            lf:   MigrationStateStore.ComputeSchemaHash("line1\nline2\n")
        ))
        .Then("both hashes are equal", t => t.crlf == t.lf)
        .AssertPassed();

    [Scenario("Happy-05: hash format is 'sha256:' prefix + lowercase hex")]
    [Fact]
    public Task Hash_Format_HasSha256Prefix() =>
        Given("ComputeSchemaHash called on 'some content'", () =>
            MigrationStateStore.ComputeSchemaHash("some content"))
        .Then("hash starts with 'sha256:'", h => h.StartsWith("sha256:", StringComparison.Ordinal))
        .And("hex portion is 64 chars", h => h["sha256:".Length..].Length == 64)
        .And("hex portion is all lowercase hex digits",
            h => h["sha256:".Length..].All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
        .AssertPassed();

    [Scenario("Happy-06: IsAlreadyApplied returns true for matching ruleId + file")]
    [Fact]
    public Task IsAlreadyApplied_MatchingEntry_ReturnsTrue() =>
        Given("state with applied entry for rule-1, /src/Foo.cs", () => FullState())
        .Then("IsAlreadyApplied('rule-1', '/src/Foo.cs') is true",
            s => s.IsAlreadyApplied("rule-1", "/src/Foo.cs"))
        .AssertPassed();

    [Scenario("Happy-07: IsAlreadyApplied returns false when file differs")]
    [Fact]
    public Task IsAlreadyApplied_DifferentFile_ReturnsFalse() =>
        Given("state with applied entry for rule-1, /src/Foo.cs", () => FullState())
        .Then("IsAlreadyApplied('rule-1', '/src/Other.cs') is false",
            s => !s.IsAlreadyApplied("rule-1", "/src/Other.cs"))
        .AssertPassed();

    [Scenario("Happy-08: SchemaHasChanged returns true when hash differs")]
    [Fact]
    public Task SchemaHasChanged_DifferentHash_ReturnsTrue() =>
        Given("state with hash 'sha256:abc123'", () => FullState())
        .Then("SchemaHasChanged('sha256:def456') returns true",
            s => s.SchemaHasChanged("sha256:def456"))
        .AssertPassed();

    [Scenario("Happy-09: SchemaHasChanged returns false when hash matches")]
    [Fact]
    public Task SchemaHasChanged_SameHash_ReturnsFalse() =>
        Given("state with hash 'sha256:abc123'", () => FullState())
        .Then("SchemaHasChanged('sha256:abc123') returns false",
            s => !s.SchemaHasChanged("sha256:abc123"))
        .AssertPassed();

    [Scenario("Happy-10: Merge appends new applied entries to existing")]
    [Fact]
    public Task Merge_AppendsNewAppliedEntriesToExisting() =>
        Given("existing state with 1 applied + result with 1 new applied entry", () =>
        {
            var original = FullState();
            var newApplied = new List<AppliedRewrite>
            {
                new("rule-99", "/src/New.cs", 42, "A", "B"),
            };
            var result = new MigrationResult(
                applied: newApplied, skipped: [], manual: [],
                rewrittenFiles: new Dictionary<string, string>(), dryRun: false);
            return original.Merge(result, "sha256:newHash");
        })
        .Then("merged has 2 applied entries", m => m.Applied.Count == 2)
        .And("hash updated to sha256:newHash", m => m.SchemaHash == "sha256:newHash")
        .And("original entry preserved", m => m.Applied.Any(a => a.RuleId == "rule-1"))
        .And("new entry present", m => m.Applied.Any(a => a.RuleId == "rule-99"))
        .AssertPassed();

    [Scenario("Happy-11: Merge replaces Skipped and Manual lists wholesale")]
    [Fact]
    public Task Merge_ReplacesSkippedAndManualLists() =>
        Given("existing state with 1 skipped + 1 manual; result with 2 skipped + 1 manual", () =>
        {
            var original = FullState();
            var newSkipped = new List<SkippedRewrite>
            {
                new("rule-X", "/src/X.cs", 1, "new reason"),
                new("rule-Y", "/src/Y.cs", 2, "another reason"),
            };
            var newManual = new List<ManualRewrite>
            {
                new("rule-Z", "New manual note", ["/src/Z.cs"]),
            };
            var result = new MigrationResult(
                applied: [], skipped: newSkipped, manual: newManual,
                rewrittenFiles: new Dictionary<string, string>(), dryRun: false);
            return original.Merge(result, "sha256:abc123");
        })
        .Then("Skipped list replaced with 2 entries", m => m.Skipped.Count == 2)
        .And("Manual list replaced with 1 entry", m => m.Manual.Count == 1)
        .And("Manual entry is rule-Z", m => m.Manual[0].RuleId == "rule-Z")
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Group: sad
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Sad-01: corrupted JSON returns null from Deserialize")]
    [Fact]
    public Task Serializer_CorruptJson_ReturnsNull() =>
        Given("invalid JSON input deserialized", () =>
            MigrationStateSerializer.Deserialize("{ not valid json ]"))
        .Then("result is null", r => r == null)
        .AssertPassed();

    [Scenario("Sad-02: hash of different content produces different hash")]
    [Fact]
    public Task Hash_DifferentContent_ProducesDifferentHash() =>
        Given("two different schema content strings hashed", () =>
        (
            h1: MigrationStateStore.ComputeSchemaHash("content A"),
            h2: MigrationStateStore.ComputeSchemaHash("content B")
        ))
        .Then("hashes differ", t => t.h1 != t.h2)
        .AssertPassed();

    [Scenario("Sad-03: trailing whitespace per line is trimmed before hashing")]
    [Fact]
    public Task Hash_TrailingWhitespaceTrimmedPerLine() =>
        Given("same content with and without trailing spaces per line", () =>
        (
            clean:    MigrationStateStore.ComputeSchemaHash("line1\nline2"),
            trailing: MigrationStateStore.ComputeSchemaHash("line1   \nline2   ")
        ))
        .Then("both hashes are equal", t => t.clean == t.trailing)
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Group: edge
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Edge-01: GetStatePath places state file sibling to schema")]
    [Fact]
    public Task GetStatePath_ReturnsSiblingPath() =>
        Given("schema path '/migrations/myschema.json'", () =>
            MigrationStateStore.GetStatePath("/migrations/myschema.json"))
        .Then("state path is '/migrations/myschema.json.state.json'",
            p => p == "/migrations/myschema.json.state.json")
        .AssertPassed();

    [Scenario("Edge-02: Merge de-duplicates applied entries by ruleId+file")]
    [Fact]
    public Task Merge_DeduplicatesAppliedByRuleIdAndFile() =>
        Given("existing state with rule-1/Foo.cs applied; result with same rule-1/Foo.cs", () =>
        {
            var original = FullState();
            var duplicate = new List<AppliedRewrite>
            {
                new("rule-1", "/src/Foo.cs", 10, "OldType", "NewType"),
            };
            var result = new MigrationResult(
                applied: duplicate, skipped: [], manual: [],
                rewrittenFiles: new Dictionary<string, string>(), dryRun: false);
            return original.Merge(result, "sha256:abc123");
        })
        .Then("merged state has exactly 1 applied entry (de-duped)", m => m.Applied.Count == 1)
        .AssertPassed();

    [Scenario("Edge-03: large state (10000 entries) deserializes under 500ms")]
    [Fact]
    public Task Deserialize_LargeState_CompletesUnder500ms() =>
        Given("a state JSON with 10000 applied entries serialized", () =>
        {
            var big = new MigrationState
            {
                Schema = "big.json",
                SchemaHash = "sha256:big",
                StartedAt = DateTimeOffset.UtcNow,
                LastRunAt = DateTimeOffset.UtcNow,
                Applied = Enumerable.Range(0, 10_000)
                    .Select(i => new AppliedRewrite($"r-{i}", $"/src/File{i}.cs", i, "old", "new"))
                    .ToList(),
            };
            return MigrationStateSerializer.Serialize(big);
        })
        .Then("deserialization completes under 500ms with 10000 entries", json =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = MigrationStateSerializer.Deserialize(json);
            sw.Stop();
            return result is not null
                && result.Applied.Count == 10_000
                && sw.ElapsedMilliseconds < 500;
        })
        .AssertPassed();

    [Scenario("Edge-04: Merge updates LastRunAt")]
    [Fact]
    public Task Merge_UpdatesLastRunAt() =>
        Given("existing state with a known LastRunAt; Merge called with MigrationResult.Empty", () =>
        {
            var original = FullState();
            System.Threading.Thread.Sleep(1);
            return (original: original.LastRunAt,
                    merged: original.Merge(MigrationResult.Empty, "sha256:newhash").LastRunAt);
        })
        .Then("merged LastRunAt >= original LastRunAt", t => t.merged >= t.original)
        .AssertPassed();
}
