using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.State;
using WrapGod.Tests.Migration.Engine.Fixtures;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.State;

[Feature("StatefulMigrationEngine: idempotent re-runs and state integration")]
public sealed class StatefulMigrationEngineTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IRuleRewriter[] DefaultRewriters() =>
    [
        new WrapGod.Migration.Engine.Rewriters.RenameTypeRewriter(),
        new WrapGod.Migration.Engine.Rewriters.RenameNamespaceRewriter(),
        new WrapGod.Migration.Engine.Rewriters.RenameMemberRewriter(),
        new WrapGod.Migration.Engine.Rewriters.ChangeParameterRewriter(),
        new WrapGod.Migration.Engine.Rewriters.RemoveMemberRewriter(),
        new WrapGod.Migration.Engine.Rewriters.AddRequiredParameterRewriter(),
        new WrapGod.Migration.Engine.Rewriters.ChangeTypeReferenceRewriter(),
    ];

    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    private static MigrationSchema OneRenameSchema(string id = "RT-001") => new()
    {
        Library = "Test", From = "1.0", To = "2.0",
        Rules =
        [
            new RenameTypeRule
            {
                Id = id, OldName = "OldWidget", NewName = "NewWidget",
                Confidence = RuleConfidence.Auto,
            }
        ]
    };

    private static string WriteSchemaFile(string dir, MigrationSchema schema)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "schema.json");
        File.WriteAllText(path, WrapGod.Migration.MigrationSchemaSerializer.Serialize(schema));
        return path;
    }

    private static StatefulMigrationEngine BuildStateful(IMigrationFileSystem fs) =>
        new(new MigrationEngine(DefaultRewriters(), fs));

    private const string SourceWithOldWidget =
        "using System;\nclass OldWidget { }\nclass Consumer { OldWidget w; }";

    // ══════════════════════════════════════════════════════════════════════════
    // Group: happy
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Happy-01: first run writes state file with applied entries")]
    [Fact]
    public Task ApplyWithState_FirstRun_WritesStateFile() =>
        Given("schema file + source file; ApplyWithState called for the first time", () =>
        {
            var dir = TempDir();
            var schemaPath = WriteSchemaFile(dir, OneRenameSchema());
            var fs = new InMemoryFileSystem().WithFile("/src/Consumer.cs", SourceWithOldWidget);
            BuildStateful(fs).ApplyWithState(schemaPath, OneRenameSchema(), ["/src/Consumer.cs"]);
            var state = MigrationStateStore.Load(schemaPath);
            return (stateExists: state != null, appliedNotEmpty: state?.Applied.Count > 0);
        })
        .Then("state file was created", t => t.stateExists)
        .And("state has at least one applied entry", t => t.appliedNotEmpty)
        .AssertPassed();

    [Scenario("Happy-02: second run with same schema skips already-applied rules")]
    [Fact]
    public Task ApplyWithState_SecondRunSameSchema_SkipsAlreadyApplied() =>
        Given("prior state with RT-001 applied for /src/Consumer.cs; second run", () =>
        {
            var dir = TempDir();
            var schema = OneRenameSchema();
            var schemaPath = WriteSchemaFile(dir, schema);
            var schemaJson = File.ReadAllText(schemaPath);
            var schemaHash = MigrationStateStore.ComputeSchemaHash(schemaJson);

            var priorState = new MigrationState
            {
                Schema = schemaPath, SchemaHash = schemaHash,
                StartedAt = DateTimeOffset.UtcNow, LastRunAt = DateTimeOffset.UtcNow,
                Applied = [new AppliedRewrite("RT-001", "/src/Consumer.cs", 3, "OldWidget", "NewWidget")],
            };
            MigrationStateStore.Save(schemaPath, priorState);

            var fs = new InMemoryFileSystem().WithFile("/src/Consumer.cs", SourceWithOldWidget);
            var result = BuildStateful(fs).ApplyWithState(schemaPath, schema, ["/src/Consumer.cs"]);
            return result;
        })
        .Then("result has no new applied entries for RT-001 + /src/Consumer.cs",
            r => !r.Applied.Any(a => a.RuleId == "RT-001" && a.File == "/src/Consumer.cs"))
        .AssertPassed();

    [Scenario("Happy-03: schema change triggers re-evaluation and updates state hash")]
    [Fact]
    public Task ApplyWithState_SchemaHashChanged_UpdatesStateHash() =>
        Given("stale state (hash mismatch); ApplyWithState re-evaluates and persists new hash", () =>
        {
            var dir = TempDir();
            var schema = OneRenameSchema();
            var schemaPath = WriteSchemaFile(dir, schema);

            var staleState = new MigrationState
            {
                Schema = schemaPath, SchemaHash = "sha256:stale-hash-does-not-match",
                StartedAt = DateTimeOffset.UtcNow, LastRunAt = DateTimeOffset.UtcNow,
                Applied = [new AppliedRewrite("RT-001", "/src/Consumer.cs", 3, "OldWidget", "NewWidget")],
            };
            MigrationStateStore.Save(schemaPath, staleState);

            var fs = new InMemoryFileSystem().WithFile("/src/Consumer.cs", SourceWithOldWidget);
            BuildStateful(fs).ApplyWithState(schemaPath, schema, ["/src/Consumer.cs"]);

            var updatedState = MigrationStateStore.Load(schemaPath);
            var expectedHash = MigrationStateStore.ComputeSchemaHash(File.ReadAllText(schemaPath));
            return (updatedState?.SchemaHash, expectedHash);
        })
        .Then("state hash is updated to current schema hash",
            t => t.Item1 == t.expectedHash)
        .AssertPassed();

    [Scenario("Happy-04: full run when no prior state file exists")]
    [Fact]
    public Task ApplyWithState_NoStateFile_RunsFullMigration() =>
        Given("schema file; no prior state; ApplyWithState called", () =>
        {
            var dir = TempDir();
            var schemaPath = WriteSchemaFile(dir, OneRenameSchema());
            var fs = new InMemoryFileSystem().WithFile("/src/Consumer.cs", SourceWithOldWidget);
            return BuildStateful(fs).ApplyWithState(schemaPath, OneRenameSchema(), ["/src/Consumer.cs"]);
        })
        .Then("result has applied entries", r => r.Applied.Count > 0)
        .AssertPassed();

    [Scenario("Happy-05: DryRunWithState does not write state file")]
    [Fact]
    public Task DryRunWithState_DoesNotWriteStateFile() =>
        Given("schema file; DryRunWithState called", () =>
        {
            var dir = TempDir();
            var schemaPath = WriteSchemaFile(dir, OneRenameSchema());
            var fs = new InMemoryFileSystem().WithFile("/src/Consumer.cs", SourceWithOldWidget);
            BuildStateful(fs).DryRunWithState(schemaPath, OneRenameSchema(), ["/src/Consumer.cs"]);
            return MigrationStateStore.GetStatePath(schemaPath);
        })
        .Then("no state file written to disk", p => !File.Exists(p))
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Group: sad
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Sad-01: ApplyWithState with null schemaPath throws ArgumentNullException")]
    [Fact]
    public Task ApplyWithState_NullSchemaPath_ThrowsArgumentNullException() =>
        Given("null schemaPath passed to ApplyWithState", () =>
        {
            try
            {
                var stateful = new StatefulMigrationEngine(new MigrationEngine(DefaultRewriters()));
                stateful.ApplyWithState(null!, OneRenameSchema(), []);
                return false;
            }
            catch (ArgumentNullException) { return true; }
        })
        .Then("ArgumentNullException was thrown", threw => threw)
        .AssertPassed();

    [Scenario("Sad-02: corrupt state file is recovered — run proceeds as a fresh run")]
    [Fact]
    public Task ApplyWithState_CorruptStateFile_RecoversFreshRun() =>
        Given("corrupt state file; ApplyWithState recovers and writes valid state", () =>
        {
            var dir = TempDir();
            var schemaPath = WriteSchemaFile(dir, OneRenameSchema());
            var statePath = MigrationStateStore.GetStatePath(schemaPath);
            File.WriteAllText(statePath, "{ CORRUPT JSON ]]]");

            var fs = new InMemoryFileSystem().WithFile("/src/Consumer.cs", SourceWithOldWidget);
            BuildStateful(fs).ApplyWithState(schemaPath, OneRenameSchema(), ["/src/Consumer.cs"]);

            return MigrationStateStore.Load(schemaPath);
        })
        .Then("run completed and valid state file exists", s => s != null)
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Group: edge
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Edge-01: manual rules from prior run are preserved in merged state")]
    [Fact]
    public Task ApplyWithState_ManualRulesPreserved_InMergedState() =>
        Given("schema with Manual-confidence rule; ApplyWithState called", () =>
        {
            var dir = TempDir();
            var schemaWithManual = new MigrationSchema
            {
                Library = "Test", From = "1.0", To = "2.0",
                Rules =
                [
                    new RenameTypeRule
                    {
                        Id = "RT-001", OldName = "OldWidget", NewName = "NewWidget",
                        Confidence = RuleConfidence.Manual, Note = "Manual step required",
                    }
                ]
            };
            var schemaPath = WriteSchemaFile(dir, schemaWithManual);
            var fs = new InMemoryFileSystem().WithFile("/src/Consumer.cs", SourceWithOldWidget);
            BuildStateful(fs).ApplyWithState(schemaPath, schemaWithManual, ["/src/Consumer.cs"]);

            var state = MigrationStateStore.Load(schemaPath);
            return (hasManual: state?.Manual.Count > 0,
                    manualRuleId: state?.Manual.FirstOrDefault()?.RuleId);
        })
        .Then("state contains Manual entry", t => t.hasManual)
        .And("Manual entry has ruleId RT-001", t => t.manualRuleId == "RT-001")
        .AssertPassed();

    [Scenario("Edge-02: prior applied entries from FileA are preserved when FileB is processed")]
    [Fact]
    public Task ApplyWithState_PriorAppliedEntries_PreservedOnNextRun() =>
        Given("prior state with RT-001 applied for FileA; second run with FileB", () =>
        {
            var dir = TempDir();
            var schema = OneRenameSchema("RT-001");
            var schemaPath = WriteSchemaFile(dir, schema);
            var schemaJson = File.ReadAllText(schemaPath);
            var schemaHash = MigrationStateStore.ComputeSchemaHash(schemaJson);

            var priorState = new MigrationState
            {
                Schema = schemaPath, SchemaHash = schemaHash,
                StartedAt = DateTimeOffset.UtcNow, LastRunAt = DateTimeOffset.UtcNow,
                Applied = [new AppliedRewrite("RT-001", "/src/FileA.cs", 3, "OldWidget", "NewWidget")],
            };
            MigrationStateStore.Save(schemaPath, priorState);

            var fs = new InMemoryFileSystem().WithFile("/src/FileB.cs", SourceWithOldWidget);
            BuildStateful(fs).ApplyWithState(schemaPath, schema, ["/src/FileB.cs"]);

            var state = MigrationStateStore.Load(schemaPath);
            return (hasFileA: state?.Applied.Any(a => a.File == "/src/FileA.cs") ?? false,
                    hasFileB: state?.Applied.Any(a => a.File == "/src/FileB.cs") ?? false);
        })
        .Then("state retains prior FileA entry", t => t.hasFileA)
        .And("state has new FileB entry", t => t.hasFileB)
        .AssertPassed();
}
