using System.CommandLine;
using System.Text.Json;
using TinyBDD;
using WrapGod.Cli;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.State;

namespace WrapGod.Tests;

/// <summary>
/// In-process BDD-style tests for <c>wrap-god migrate status</c>.
/// Uses the same helper pattern as MigrateGenerateCliTests (Console redirect + Command.InvokeAsync).
/// </summary>
[Feature("CLI: migrate status command reports migration progress from state file")]
[Collection("CLI")]
public sealed class MigrateStatusCliTests
{
    // ── Helper: invoke via migrate parent ────────────────────────────────────────────────────

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(string args)
    {
        var command = MigrateCommandBuilder.Build();
        var previousOut = Console.Out;
        var previousErr = Console.Error;
        var previousExitCode = Environment.ExitCode;
        using var stdOut = new StringWriter();
        using var stdErr = new StringWriter();

        try
        {
            Console.SetOut(stdOut);
            Console.SetError(stdErr);
            Environment.ExitCode = 0;

            var invokeCode = await command.InvokeAsync(args);
            var effectiveExitCode = Environment.ExitCode == 0 ? invokeCode : Environment.ExitCode;
            return (effectiveExitCode, stdOut.ToString(), stdErr.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousErr);
            Environment.ExitCode = previousExitCode;
        }
    }

    // ── Fixture helpers ──────────────────────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wrapgod-status-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { /* best-effort */ }
    }

    /// <summary>Writes a minimal schema JSON file and returns its path.</summary>
    private static async Task<string> WriteSchemaAsync(string dir, string fileName = "schema.wrapgod-migration.json")
    {
        var schemaPath = Path.Combine(dir, fileName);
        await File.WriteAllTextAsync(schemaPath, """
            {
              "schemaVersion": "1.0",
              "library": "TestLib",
              "from": "1.0.0",
              "to": "2.0.0",
              "rules": []
            }
            """);
        return schemaPath;
    }

    /// <summary>Writes a state file sibling to the schema and returns the schema path.</summary>
    private static async Task<string> WriteStateAsync(
        string dir,
        MigrationState state,
        string schemaFileName = "schema.wrapgod-migration.json")
    {
        var schemaPath = await WriteSchemaAsync(dir, schemaFileName);
        MigrationStateStore.Save(schemaPath, state);
        return schemaPath;
    }

    private static MigrationState BuildState(
        int appliedCount = 38,
        int skippedCount = 6,
        int manualCount = 3,
        string schemaHash = "sha256:aabbccdd")
    {
        var applied = Enumerable.Range(1, appliedCount)
            .Select(i => new AppliedRewrite($"R-{i:D3}", $"src/File{i}.cs", i * 10, $"OldType{i}", $"NewType{i}"))
            .ToList();

        var skipped = Enumerable.Range(1, skippedCount)
            .Select(i => new SkippedRewrite($"S-{i:D3}", $"src/Skip{i}.cs", i * 5, $"Ambiguous: reason {i}"))
            .ToList();

        var manual = Enumerable.Range(1, manualCount)
            .Select(i => new ManualRewrite($"M-{i:D3}", $"Manual action {i}", [$"src/Manual{i}A.cs", $"src/Manual{i}B.cs"]))
            .ToList();

        return new MigrationState
        {
            Schema = "schema.wrapgod-migration.json",
            SchemaHash = schemaHash,
            StartedAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
            LastRunAt = new DateTimeOffset(2026, 4, 2, 9, 14, 33, TimeSpan.Zero),
            Summary = new MigrationStateSummary
            {
                TotalRules = appliedCount + skippedCount + manualCount,
                Applied = appliedCount,
                Skipped = skippedCount,
                Manual = manualCount,
            },
            Applied = applied,
            Skipped = skipped,
            Manual = manual,
        };
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Group: happy
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SCENARIO: Happy-01 — state with applied/skipped/manual entries exits 2 and shows counts.
    /// </summary>
    [Scenario("Happy-01: status with applied/skipped/manual entries exits 2, stdout has counts")]
    [Fact]
    public async Task Status_HappyPath_PrintsProgress()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState(38, 6, 3));
            var (exitCode, stdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\"");

            Assert.Equal(2, exitCode);
            Assert.Contains("38", stdout);
            Assert.Contains("6", stdout);
            Assert.Contains("3", stdout);
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Happy-02 — --json flag produces valid JSON with required fields.
    /// </summary>
    [Scenario("Happy-02: --json flag produces valid JSON with required fields")]
    [Fact]
    public async Task Status_Json_ParsesAsJson()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState(38, 6, 3));
            var (exitCode, stdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\" --json");

            Assert.Equal(2, exitCode);
            var doc = JsonDocument.Parse(stdout); // throws if not valid JSON
            Assert.True(doc.RootElement.TryGetProperty("applied", out _), "JSON must have 'applied' field");
            Assert.True(doc.RootElement.TryGetProperty("skipped", out _), "JSON must have 'skipped' field");
            Assert.True(doc.RootElement.TryGetProperty("manual", out _), "JSON must have 'manual' field");
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Happy-03 — state with 0 manual entries exits 0.
    /// </summary>
    [Scenario("Happy-03: state with zero manual entries exits 0")]
    [Fact]
    public async Task Status_NoManual_ExitsZero()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState(10, 2, 0));
            var (exitCode, _, _) = await InvokeAsync($"status --schema \"{schemaPath}\"");

            Assert.Equal(0, exitCode);
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Happy-04 — state with manual entries exits 2.
    /// </summary>
    [Scenario("Happy-04: state with manual entries exits 2")]
    [Fact]
    public async Task Status_ManualPresent_ExitsTwo()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState(5, 1, 3));
            var (exitCode, _, _) = await InvokeAsync($"status --schema \"{schemaPath}\"");

            Assert.Equal(2, exitCode);
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Happy-05 — --verbose includes per-rule detail lines (rule IDs from applied/skipped/manual).
    /// </summary>
    [Scenario("Happy-05: --verbose includes per-rule detail lines")]
    [Fact]
    public async Task Status_Verbose_IncludesRuleDetails()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState(3, 1, 1));
            var (exitCode, stdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\" --verbose");

            Assert.Equal(2, exitCode);
            Assert.Contains("R-001", stdout); // applied rule
            Assert.Contains("S-001", stdout); // skipped rule
            Assert.Contains("M-001", stdout); // manual rule
        }
        finally { SafeDelete(dir); }
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Group: sad
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SCENARIO: Sad-01 — no state file exits 0 with friendly message.
    /// </summary>
    [Scenario("Sad-01: missing state file exits 0 with friendly message")]
    [Fact]
    public async Task Status_NoState_PrintsFriendlyMessage()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteSchemaAsync(dir); // schema exists, but no .state.json
            var (exitCode, stdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\"");

            Assert.Equal(0, exitCode);
            Assert.Contains("No migration runs recorded", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Sad-02 — corrupt state file exits 1.
    /// </summary>
    [Scenario("Sad-02: corrupt state file exits 1")]
    [Fact]
    public async Task Status_CorruptState_Fails()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteSchemaAsync(dir);
            var statePath = MigrationStateStore.GetStatePath(schemaPath);
            await File.WriteAllTextAsync(statePath, "{ this is not valid json !!!");

            var (exitCode, stdout, stderr) = await InvokeAsync($"status --schema \"{schemaPath}\"");

            Assert.Equal(1, exitCode);
            // Must mention corrupt/invalid somewhere
            var combined = stdout + stderr;
            Assert.True(
                combined.Contains("corrupt", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("invalid", StringComparison.OrdinalIgnoreCase),
                $"Expected 'corrupt' or 'invalid' in output. Actual output: {combined}");
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Sad-03 — schema file does not exist exits 1.
    /// </summary>
    [Scenario("Sad-03: non-existent schema path exits 1")]
    [Fact]
    public async Task Status_MissingSchema_Fails()
    {
        var (exitCode, _, _) = await InvokeAsync("status --schema does-not-exist-schema.json");
        Assert.Equal(1, exitCode);
    }

    /// <summary>
    /// SCENARIO: Sad-04 — missing --schema flag exits with non-zero (parse error).
    /// </summary>
    [Scenario("Sad-04: missing --schema flag causes parse error exit")]
    [Fact]
    public async Task Status_NoSchemaFlag_Fails()
    {
        var (exitCode, _, _) = await InvokeAsync("status");
        Assert.NotEqual(0, exitCode);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Group: edge
    // ════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// SCENARIO: Edge-01 — schema hash mismatch warns user.
    /// </summary>
    [Scenario("Edge-01: schema hash mismatch produces schema-changed warning")]
    [Fact]
    public async Task Status_SchemaHashMismatch_Warns()
    {
        var dir = CreateTempDir();
        try
        {
            // Write a state with a hash that won't match the actual schema content
            var state = BuildState(10, 2, 0);
            state.SchemaHash = "sha256:deadbeef00000000000000000000000000000000000000000000000000000000";
            var schemaPath = await WriteStateAsync(dir, state);

            var (exitCode, stdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\"");

            Assert.Equal(0, exitCode); // no manual
            Assert.True(
                stdout.Contains("schema has changed", StringComparison.OrdinalIgnoreCase) ||
                stdout.Contains("schema changed", StringComparison.OrdinalIgnoreCase),
                $"Expected schema-change warning in output. Actual: {stdout}");
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Edge-02 — empty state (0 applied, 0 skipped, 0 manual) exits 0.
    /// </summary>
    [Scenario("Edge-02: empty state (all zero counts) exits 0")]
    [Fact]
    public async Task Status_EmptyState_ExitsZero()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState(0, 0, 0));
            var (exitCode, _, _) = await InvokeAsync($"status --schema \"{schemaPath}\"");

            Assert.Equal(0, exitCode);
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Edge-03 — manual rule with empty MatchedFiles shows placeholder.
    /// </summary>
    [Scenario("Edge-03: manual rule with empty MatchedFiles shows placeholder text")]
    [Fact]
    public async Task Status_ManualWithEmptyMatched_PrintsPlaceholder()
    {
        var dir = CreateTempDir();
        try
        {
            var state = new MigrationState
            {
                Schema = "schema.wrapgod-migration.json",
                SchemaHash = "sha256:aabbccdd",
                StartedAt = DateTimeOffset.UtcNow,
                LastRunAt = DateTimeOffset.UtcNow,
                Summary = new MigrationStateSummary { TotalRules = 1, Applied = 0, Skipped = 0, Manual = 1 },
                Applied = [],
                Skipped = [],
                Manual = [new ManualRewrite("M-001", "Manual action with no files", [])],
            };

            var schemaPath = await WriteStateAsync(dir, state);
            var (exitCode, stdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\" --verbose");

            Assert.Equal(2, exitCode);
            Assert.Contains("no files matched", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Edge-04 — --json with no state file outputs sentinel JSON.
    /// </summary>
    [Scenario("Edge-04: --json with no state file outputs sentinel JSON { status: no-runs-recorded }")]
    [Fact]
    public async Task Status_Json_NoState_OutputsSentinel()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteSchemaAsync(dir);
            var (exitCode, stdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\" --json");

            Assert.Equal(0, exitCode);
            var doc = JsonDocument.Parse(stdout);
            Assert.True(
                doc.RootElement.TryGetProperty("status", out var statusEl) &&
                statusEl.GetString() == "no-runs-recorded",
                $"Expected JSON {{ \"status\": \"no-runs-recorded\" }}. Actual: {stdout}");
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Edge-05 — state with synthetic &lt;state&gt; SkippedRewrite highlights recovery.
    /// </summary>
    [Scenario("Edge-05: state with synthetic <state> SkippedRewrite highlights state recovery")]
    [Fact]
    public async Task Status_SyntheticStateSkippedRewrite_HighlightsRecovery()
    {
        var dir = CreateTempDir();
        try
        {
            var state = new MigrationState
            {
                Schema = "schema.wrapgod-migration.json",
                SchemaHash = "sha256:aabbccdd",
                StartedAt = DateTimeOffset.UtcNow,
                LastRunAt = DateTimeOffset.UtcNow,
                Summary = new MigrationStateSummary { TotalRules = 2, Applied = 1, Skipped = 1, Manual = 0 },
                Applied = [new AppliedRewrite("R-001", "src/File.cs", 5, "OldType", "NewType")],
                Skipped = [new SkippedRewrite("<state>", "<state>", 0, "Previous state was corrupt; recovery run from scratch.")],
                Manual = [],
            };

            var schemaPath = await WriteStateAsync(dir, state);
            var (exitCode, stdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\"");

            Assert.Equal(0, exitCode); // no manual
            Assert.True(
                stdout.Contains("state recovery", StringComparison.OrdinalIgnoreCase) ||
                stdout.Contains("recovery", StringComparison.OrdinalIgnoreCase) ||
                stdout.Contains("<state>", StringComparison.OrdinalIgnoreCase),
                $"Expected recovery mention in output. Actual: {stdout}");
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Edge-06 — help text lists expected flags.
    /// </summary>
    [Scenario("Edge-06: help text lists --schema, --json, --verbose, --project-dir flags")]
    [Fact]
    public async Task Status_Help_ListsAllFlags()
    {
        var (exitCode, stdout, _) = await InvokeAsync("status --help");
        Assert.Equal(0, exitCode);
        Assert.Contains("--schema", stdout);
        Assert.Contains("--json", stdout);
        Assert.Contains("--verbose", stdout);
        Assert.Contains("--project-dir", stdout);
    }

    /// <summary>
    /// SCENARIO: Edge-07 — corrupt state archived to .bak is reported.
    /// MigrationStateStore.Load archives corrupt state to a .bak file.
    /// The status command should detect wasCorrupt==true and report it.
    /// </summary>
    [Scenario("Edge-07: corrupt state archived to .bak — exit 1 with backup/corrupt mention")]
    [Fact]
    public async Task Status_CorruptState_MentionsBackup()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteSchemaAsync(dir);
            var statePath = MigrationStateStore.GetStatePath(schemaPath);
            await File.WriteAllTextAsync(statePath, "{ CORRUPT JSON ");

            var (exitCode, stdout, stderr) = await InvokeAsync($"status --schema \"{schemaPath}\"");

            Assert.Equal(1, exitCode);
            var combined = stdout + stderr;
            Assert.True(
                combined.Contains(".bak", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("backup", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("archived", StringComparison.OrdinalIgnoreCase) ||
                combined.Contains("corrupt", StringComparison.OrdinalIgnoreCase),
                $"Expected .bak/backup/corrupt mention. Actual: {combined}");
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Edge-08 — --project-dir resolves relative --schema.
    /// </summary>
    [Scenario("Edge-08: --project-dir resolves relative --schema path")]
    [Fact]
    public async Task Status_ProjectDir_ResolvesRelativeSchema()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState(5, 1, 0));
            var schemaFileName = Path.GetFileName(schemaPath);

            var (exitCode, stdout, _) = await InvokeAsync(
                $"status --schema \"{schemaFileName}\" --project-dir \"{dir}\"");

            Assert.Equal(0, exitCode); // no manual
            Assert.Contains("5", stdout);
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Edge-09 — schema with zero rules: human output shows "n/a", JSON
    /// has <c>progressPct: null</c>. Per plan §200.c table row "Status_ZeroRules_HandlesNa".
    /// </summary>
    [Scenario("Edge-09: schema with zero rules -> human output 'n/a', JSON progressPct null")]
    [Fact]
    public async Task Status_ZeroRules_HandlesNa()
    {
        var dir = CreateTempDir();
        try
        {
            // Default schema fixture in WriteSchemaAsync already has "rules": [] (0 rules)
            // and the BuildState helper here uses (0,0,0) to match.
            var schemaPath = await WriteStateAsync(dir, BuildState(0, 0, 0));

            // Human-readable mode → "n/a"
            var (humanExit, humanStdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\"");
            Assert.Equal(0, humanExit);
            Assert.Contains("n/a", humanStdout, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("0 / 0", humanStdout);

            // JSON mode → progressPct is null
            var (jsonExit, jsonStdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\" --json");
            Assert.Equal(0, jsonExit);
            using var doc = JsonDocument.Parse(jsonStdout);
            Assert.True(doc.RootElement.TryGetProperty("progressPct", out var pctEl),
                "JSON must have 'progressPct' field");
            Assert.Equal(JsonValueKind.Null, pctEl.ValueKind);

            // Schema metadata is included
            Assert.True(doc.RootElement.TryGetProperty("library", out var libEl),
                "JSON must have 'library' field");
            Assert.Equal("TestLib", libEl.GetString());
            Assert.True(doc.RootElement.TryGetProperty("from", out var fromEl),
                "JSON must have 'from' field");
            Assert.Equal("1.0.0", fromEl.GetString());
            Assert.True(doc.RootElement.TryGetProperty("to", out var toEl),
                "JSON must have 'to' field");
            Assert.Equal("2.0.0", toEl.GetString());
        }
        finally { SafeDelete(dir); }
    }

    /// <summary>
    /// SCENARIO: Edge-10 — progressPct + ratio line populated when totalRules > 0.
    /// Belt-and-suspenders for plan §200.b Happy-01 + Happy-02. Verifies the
    /// "N / M rules applied (P%)" human-mode line and the JSON progressPct numeric value.
    /// </summary>
    [Scenario("Edge-10: progressPct + ratio line — totalRules>0, applied<total")]
    [Fact]
    public async Task Status_ProgressRatio_PopulatedWhenTotalRulesNonZero()
    {
        var dir = CreateTempDir();
        try
        {
            // Write a schema with 5 rules; state has 3 applied (distinct rule IDs)
            var schemaPath = Path.Combine(dir, "schema.wrapgod-migration.json");
            await File.WriteAllTextAsync(schemaPath, """
                {
                  "schemaVersion": "1.0",
                  "library": "Serilog",
                  "from": "2.12.0",
                  "to": "3.1.1",
                  "rules": [
                    { "kind": "renameType", "id": "R-001", "from": "Serilog.Foo", "to": "Serilog.Bar" },
                    { "kind": "renameType", "id": "R-002", "from": "Serilog.Baz", "to": "Serilog.Qux" },
                    { "kind": "renameType", "id": "R-003", "from": "Serilog.A", "to": "Serilog.B" },
                    { "kind": "renameType", "id": "R-004", "from": "Serilog.C", "to": "Serilog.D" },
                    { "kind": "renameType", "id": "R-005", "from": "Serilog.E", "to": "Serilog.F" }
                  ]
                }
                """);

            // 3 applied rule IDs (distinct), 0 skipped, 0 manual
            var state = new MigrationState
            {
                Schema = "schema.wrapgod-migration.json",
                SchemaHash = "sha256:aabbccdd",
                StartedAt = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero),
                LastRunAt = new DateTimeOffset(2026, 4, 2, 9, 14, 33, TimeSpan.Zero),
                Summary = new MigrationStateSummary { TotalRules = 5, Applied = 3, Skipped = 0, Manual = 0 },
                Applied =
                [
                    new AppliedRewrite("R-001", "src/a.cs", 10, "old", "new"),
                    new AppliedRewrite("R-002", "src/b.cs", 11, "old", "new"),
                    new AppliedRewrite("R-003", "src/c.cs", 12, "old", "new"),
                ],
                Skipped = [],
                Manual = [],
            };
            MigrationStateStore.Save(schemaPath, state);

            // Human-readable mode
            var (humanExit, humanStdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\"");
            Assert.Equal(0, humanExit);
            Assert.Contains("3 / 5 rules applied", humanStdout);
            Assert.Contains("60%", humanStdout); // 3/5 = 0.60
            Assert.Contains("Serilog 2.12.0 -> 3.1.1", humanStdout);

            // JSON mode
            var (jsonExit, jsonStdout, _) = await InvokeAsync($"status --schema \"{schemaPath}\" --json");
            Assert.Equal(0, jsonExit);
            using var doc = JsonDocument.Parse(jsonStdout);
            Assert.True(doc.RootElement.TryGetProperty("progressPct", out var pctEl));
            Assert.Equal(JsonValueKind.Number, pctEl.ValueKind);
            Assert.Equal(0.6, pctEl.GetDouble(), precision: 5);
            Assert.Equal("Serilog", doc.RootElement.GetProperty("library").GetString());
            Assert.Equal("2.12.0", doc.RootElement.GetProperty("from").GetString());
            Assert.Equal("3.1.1", doc.RootElement.GetProperty("to").GetString());
            Assert.Equal(5, doc.RootElement.GetProperty("totalRules").GetInt32());
            Assert.Equal(3, doc.RootElement.GetProperty("appliedRules").GetInt32());
        }
        finally { SafeDelete(dir); }
    }
}
