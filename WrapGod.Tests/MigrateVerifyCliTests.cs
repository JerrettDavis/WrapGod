using System.CommandLine;
using System.Text.Json;
using TinyBDD;
using WrapGod.Cli;
using WrapGod.Cli.Verification;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.State;

namespace WrapGod.Tests;

/// <summary>
/// BDD-style tests for <c>wrap-god migrate verify</c>.
///
/// All scenarios use a <see cref="StubBuildRunner"/> to avoid spawning a real
/// <c>dotnet build</c> process. The stub returns pre-canned output covering the
/// full range of diagnostic lines described in Issue #201.
///
/// Exit code contract per MIGRATION-ENGINE-PLAN.md §201.a:
///   0 – verify ran (even if there are attributed errors; verify is non-gating)
///   1 – IO error (baseline missing, schema not found, etc.)
///   2 – bad arguments
/// </summary>
[Feature("CLI: migrate verify command performs semantic verification post-apply")]
[Collection("CLI")]
public sealed class MigrateVerifyCliTests
{
    // ── StubBuildRunner ──────────────────────────────────────────────────────

    /// <summary>
    /// A test double for <see cref="IBuildRunner"/> that returns a pre-canned
    /// <see cref="BuildResult"/> without spawning any process.
    /// </summary>
    private sealed class StubBuildRunner(BuildResult result) : IBuildRunner
    {
        public Task<BuildResult> RunAsync(string projectDir, string buildConfig, CancellationToken ct)
            => Task.FromResult(result);
    }

    // ── Invoke helpers ───────────────────────────────────────────────────────

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(
        string args, IBuildRunner? buildRunner = null)
    {
        var runner  = buildRunner ?? new StubBuildRunner(BuildResult.Completed(string.Empty, 0));
        var command = MigrateVerifyCommand.Create(runner);

        var previousOut      = Console.Out;
        var previousErr      = Console.Error;
        var previousExitCode = Environment.ExitCode;
        using var stdOut = new StringWriter();
        using var stdErr = new StringWriter();

        try
        {
            Console.SetOut(stdOut);
            Console.SetError(stdErr);
            Environment.ExitCode = 0;

            var code = await command.InvokeAsync(args);
            var effectiveCode = Environment.ExitCode == 0 ? code : Environment.ExitCode;
            return (effectiveCode, stdOut.ToString(), stdErr.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousErr);
            Environment.ExitCode = previousExitCode;
        }
    }

    // ── Fixture helpers ──────────────────────────────────────────────────────

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wrapgod-verify-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { /* best-effort */ }
    }

    /// <summary>Creates a minimal schema file and returns its path.</summary>
    private static async Task<string> WriteSchemaAsync(string dir, string name = "schema.wrapgod-migration.json")
    {
        var path = Path.Combine(dir, name);
        await File.WriteAllTextAsync(path, """
            {
              "schema": "wrapgod-migration/1.0",
              "library": "TestLib",
              "from": "1.0.0",
              "to": "2.0.0",
              "rules": []
            }
            """);
        return path;
    }

    /// <summary>Saves a state file sibling to the given schema and returns the schema path.</summary>
    private static async Task<string> WriteStateAsync(
        string dir,
        MigrationState state,
        string name = "schema.wrapgod-migration.json")
    {
        var schemaPath = await WriteSchemaAsync(dir, name);
        MigrationStateStore.Save(schemaPath, state);
        return schemaPath;
    }

    private static MigrationState BuildState(IEnumerable<AppliedRewrite>? applied = null)
    {
        var a = (applied ?? []).ToList();
        return new MigrationState
        {
            Schema     = "schema.wrapgod-migration.json",
            SchemaHash = "sha256:aabbccdd",
            StartedAt  = DateTimeOffset.UtcNow,
            LastRunAt  = DateTimeOffset.UtcNow,
            Summary    = new MigrationStateSummary
            {
                TotalRules = a.Count,
                Applied    = a.Count,
            },
            Applied = a,
        };
    }

    /// <summary>
    /// Formats a compiler diagnostic line in Roslyn/MSBuild format:
    /// <c>file(line,col): severity CODE: message</c>
    /// </summary>
    private static string DiagLine(
        string file, int line, int col, string severity, string code, string message) =>
        $"{file}({line},{col}): {severity} {code}: {message}";

    // ── Scenario 1: Help text lists all flags ────────────────────────────────

    [Scenario("Verify --help lists all registered option flags")]
    [Fact]
    public async Task Verify_Help_ListsAllFlags()
    {
        var (_, stdout, _) = await InvokeAsync("--help");

        Assert.Contains("--schema",       stdout);
        Assert.Contains("--no-build",     stdout);
        Assert.Contains("--json",         stdout);
        Assert.Contains("--verbose",      stdout);
        Assert.Contains("--build-config", stdout);
        Assert.Contains("--baseline",     stdout);
    }

    // ── Scenario 2: Diagnostic within ±3 lines is attributed ────────────────

    [Scenario("A compiler error within 3 lines of an applied rewrite is attributed to that rule")]
    [Fact]
    public async Task Verify_AttributesDiagnosticWithin3Lines()
    {
        var dir = CreateTempDir();
        try
        {
            // Rewrite at line 14; diagnostic at line 16 (delta = 2, within ±3)
            var applied    = new AppliedRewrite("R-001", "Foo.cs", 14, "OldWidget", "NewWidget");
            var schemaPath = await WriteStateAsync(dir, BuildState([applied]));

            // Use the same path that will normalise to "Foo.cs"
            var diagLine = DiagLine("Foo.cs", 16, 5, "error", "CS0103", "X does not exist");
            var stub = new StubBuildRunner(BuildResult.Completed(diagLine, 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);                               // verify is non-gating
            Assert.Contains("R-001", stdout);                        // attribution visible
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 3: Diagnostic beyond ±3 lines is NOT attributed ────────────

    [Scenario("A compiler error more than 3 lines away from any rewrite is not attributed")]
    [Fact]
    public async Task Verify_DoesNotAttributeBeyond3Lines()
    {
        var dir = CreateTempDir();
        try
        {
            // Rewrite at line 10; diagnostic at line 18 (delta = 8, outside ±3)
            var applied    = new AppliedRewrite("R-001", "Foo.cs", 10, "OldWidget", "NewWidget");
            var schemaPath = await WriteStateAsync(dir, BuildState([applied]));

            var diagLine = DiagLine("Foo.cs", 18, 1, "error", "CS0103", "X missing");
            var stub = new StubBuildRunner(BuildResult.Completed(diagLine, 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("Unattributed", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 4: Baseline subtracts pre-existing diagnostics ─────────────

    [Scenario("Diagnostics present in the baseline are classified as pre-existing")]
    [Fact]
    public async Task Verify_BaselineSubtractsPreExisting()
    {
        var dir = CreateTempDir();
        try
        {
            const string file = "Foo.cs";
            var applied    = new AppliedRewrite("R-001", file, 14, "OldWidget", "NewWidget");
            var schemaPath = await WriteStateAsync(dir, BuildState([applied]));

            var diagLine = DiagLine(file, 16, 5, "error", "CS0103", "X missing");

            // Write baseline JSON with the same diagnostic
            var baselineJson = JsonSerializer.Serialize(new[]
            {
                new { filePath = file, line = 16, column = 5, severity = "error", code = "CS0103", message = "X missing" },
            });
            var baselinePath = Path.Combine(dir, "baseline.json");
            await File.WriteAllTextAsync(baselinePath, baselineJson);

            var stub = new StubBuildRunner(BuildResult.Completed(diagLine, 1));
            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\" --baseline \"{baselinePath}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("re-existing", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 5: Zero-error build → clean report ──────────────────────────

    [Scenario("A successful build with zero errors produces a clean zero-errors report")]
    [Fact]
    public async Task Verify_BuildSucceeds_ReportsZeroErrors()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState());
            var stub = new StubBuildRunner(BuildResult.Completed("Build succeeded.", 0));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("0 error", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 6: dotnet build not on PATH → graceful skip ────────────────

    [Scenario("When dotnet build cannot be launched the command exits 0 with a skip note")]
    [Fact]
    public async Task Verify_BuildNotOnPath_SkipsGracefully()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState());
            var stub = new StubBuildRunner(BuildResult.LaunchFailed("dotnet not found on PATH"));

            var (exitCode, _, stdErr) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("dotnet build not found", stdErr, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 7: No state file → graceful skip ────────────────────────────

    [Scenario("When no migration state file exists the command exits 0 with an explanatory message")]
    [Fact]
    public async Task Verify_NoStateFile_SkipsGracefully()
    {
        var dir = CreateTempDir();
        try
        {
            // Write schema but NO state file
            var schemaPath = await WriteSchemaAsync(dir);
            var stub = new StubBuildRunner(BuildResult.Completed(string.Empty, 0));

            var (exitCode, _, stdErr) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("no migration state", stdErr, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 8: Unparseable diagnostic line is skipped ──────────────────

    [Scenario("An unparseable line in dotnet build output does not crash the command")]
    [Fact]
    public async Task Verify_UnparseableDiagnostic_Skipped()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState());
            const string gibberish =
                "   MSBuild: some internal message that is not a diagnostic\n" +
                "   Restore complete.\n";
            var stub = new StubBuildRunner(BuildResult.Completed(gibberish, 0));

            var (exitCode, _, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode); // no crash
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 9: Baseline file missing → exit 1 ───────────────────────────

    [Scenario("When --baseline points to a missing file the command exits 1 with an error")]
    [Fact]
    public async Task Verify_BaselineMissing_Fails()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath    = await WriteStateAsync(dir, BuildState());
            var missingBaseline = Path.Combine(dir, "does-not-exist.json");

            var (exitCode, _, stdErr) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\" --baseline \"{missingBaseline}\"");

            Assert.Equal(1, exitCode);
            Assert.Contains("baseline file not found", stdErr, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 10: Tiebreak — two rewrites equidistant, latest index wins ──

    [Scenario("When two rewrites are equidistant the one with the latest AppliedAt timestamp wins")]
    [Fact]
    public async Task Verify_TwoRulesEquidistant_PicksLatestAppliedAt()
    {
        var dir = CreateTempDir();
        try
        {
            const string file = "Foo.cs";
            // Two rewrites: R-001 at line 10, R-002 at line 14. Diagnostic at line 12.
            // |12 - 10| = 2 == |12 - 14| = 2. Tie → latest AppliedAt wins.
            // We arrange R-001 with the LATER timestamp despite being earlier in the
            // list — this proves we tiebreak by AppliedAt and not by index alone.
            var laterTimestamp   = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
            var earlierTimestamp = new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);

            var r1 = new AppliedRewrite("R-001", file, 10, "A", "B", laterTimestamp);
            var r2 = new AppliedRewrite("R-002", file, 14, "C", "D", earlierTimestamp);
            var schemaPath = await WriteStateAsync(dir, BuildState([r1, r2]));

            var diagLine = DiagLine(file, 12, 1, "error", "CS0001", "msg");
            var stub = new StubBuildRunner(BuildResult.Completed(diagLine, 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("R-001", stdout); // tiebreak: later AppliedAt wins
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 10b: AppliedAt equal → falls back to index order ────────────

    [Scenario("Equal AppliedAt timestamps fall back to index-order tiebreak (legacy state files)")]
    [Fact]
    public async Task Verify_EqualAppliedAt_FallsBackToIndexOrder()
    {
        var dir = CreateTempDir();
        try
        {
            const string file = "Foo.cs";
            // Both rewrites carry the default DateTimeOffset (MinValue), simulating
            // a state file written by a build that predates the AppliedAt field.
            var r1 = new AppliedRewrite("R-001", file, 10, "A", "B"); // default AppliedAt
            var r2 = new AppliedRewrite("R-002", file, 14, "C", "D"); // default AppliedAt
            var schemaPath = await WriteStateAsync(dir, BuildState([r1, r2]));

            var diagLine = DiagLine(file, 12, 1, "error", "CS0001", "msg");
            var stub = new StubBuildRunner(BuildResult.Completed(diagLine, 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            // Equal AppliedAt → secondary tiebreak by index → later index wins → R-002
            Assert.Contains("R-002", stdout);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 11: Case-insensitive path matching ──────────────────────────

    [Scenario("Path matching is case-insensitive on Windows")]
    [Fact]
    public async Task Verify_PathCaseInsensitiveMatch()
    {
        var dir = CreateTempDir();
        try
        {
            const string upperFile = "Foo.cs";
            var lowerFile = upperFile.ToLowerInvariant(); // "foo.cs"

            // Rewrite recorded with the uppercase variant
            var applied    = new AppliedRewrite("R-001", upperFile, 5, "A", "B");
            var schemaPath = await WriteStateAsync(dir, BuildState([applied]));

            // Diagnostic comes in with the lowercase variant
            var diagLine = DiagLine(lowerFile, 6, 1, "error", "CS0001", "msg");
            var stub = new StubBuildRunner(BuildResult.Completed(diagLine, 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("R-001", stdout); // matched despite case difference
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 12: JSON output has required keys ───────────────────────────

    [Scenario("--json output is parseable JSON containing build, attribution, and unattributed keys")]
    [Fact]
    public async Task Verify_Json_ParsesAsJson()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState());
            var stub = new StubBuildRunner(BuildResult.Completed(string.Empty, 0));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\" --json", stub);

            Assert.Equal(0, exitCode);
            var doc = JsonDocument.Parse(stdout); // throws if not valid JSON
            Assert.True(doc.RootElement.TryGetProperty("build",         out _), "must have 'build'");
            Assert.True(doc.RootElement.TryGetProperty("attribution",   out _), "must have 'attribution'");
            Assert.True(doc.RootElement.TryGetProperty("unattributed",  out _), "must have 'unattributed'");
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 13: --no-build skips build, reports state summary only ──────

    [Scenario("--no-build skips the dotnet build invocation and reports state summary")]
    [Fact]
    public async Task Verify_NoBuild_ReportsStateSummaryOnly()
    {
        var dir = CreateTempDir();
        try
        {
            var applied    = new AppliedRewrite("R-001", "Foo.cs", 10, "A", "B");
            var schemaPath = await WriteStateAsync(dir, BuildState([applied]));

            // Stub would return errors; --no-build should prevent it being called
            var neverCalledStub = new StubBuildRunner(BuildResult.Completed(
                DiagLine("Foo.cs", 10, 1, "error", "CS0001", "should not appear"), 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\" --no-build", neverCalledStub);

            Assert.Equal(0, exitCode);
            Assert.Contains("SKIPPED", stdout, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CS0001", stdout); // build was not run
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 14: All errors unattributed when state has 0 applied ────────

    [Scenario("When the state file has no applied rewrites all build errors are unattributed")]
    [Fact]
    public async Task Verify_ZeroApplied_AllErrorsUnattributed()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState()); // no applied rewrites
            var diagLine   = DiagLine("Foo.cs", 10, 1, "error", "CS0001", "msg");
            var stub       = new StubBuildRunner(BuildResult.Completed(diagLine, 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("Unattributed", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 15: Diagnostic with no file path (MSBuild-level) ────────────

    [Scenario("An MSBuild-level error without a file reference does not crash the command")]
    [Fact]
    public async Task Verify_DiagnosticWithNoFilePath_DoesNotCrash()
    {
        var dir = CreateTempDir();
        try
        {
            var applied    = new AppliedRewrite("R-001", "Foo.cs", 10, "A", "B");
            var schemaPath = await WriteStateAsync(dir, BuildState([applied]));

            // MSBuild-level error — no (line,col), so the diagnostic parser won't parse it
            const string msBuildError = "error MSB3001: some project-level error without file reference\n";
            var stub = new StubBuildRunner(BuildResult.Completed(msBuildError, 1));

            var (exitCode, _, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode); // non-fatal
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 16: Warnings only → exit 0 with 0 errors ───────────────────

    [Scenario("A build with only warnings exits 0 and reports 0 errors")]
    [Fact]
    public async Task Verify_BuildWithOnlyWarnings_ExitsZeroNoErrors()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState());
            var warnLine   = DiagLine("Foo.cs", 5, 1, "warning", "CS0618", "deprecated usage");
            var stub       = new StubBuildRunner(BuildResult.Completed(warnLine, 0));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("0 error", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 17: Schema hash mismatch → warning but proceed ─────────────

    [Scenario("A schema hash mismatch produces a warning but the command still exits 0")]
    [Fact]
    public async Task Verify_SchemaHashMismatch_WarnAndProceed()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState());

            // Mutate the schema AFTER saving state → current hash will differ from stored hash
            await File.AppendAllTextAsync(schemaPath, "\n// mutated");

            var stub = new StubBuildRunner(BuildResult.Completed(string.Empty, 0));
            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("schema has changed", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 18: Three errors, two attributed, one not ──────────────────

    [Scenario("Three errors: two near applied rules (attributed), one far away (unattributed)")]
    [Fact]
    public async Task Verify_ThreeErrors_TwoAttributedOneUnattributed()
    {
        var dir = CreateTempDir();
        try
        {
            const string file = "Foo.cs";
            var r1 = new AppliedRewrite("R-001", file, 10, "A", "B");
            var r2 = new AppliedRewrite("R-002", file, 30, "C", "D");
            var schemaPath = await WriteStateAsync(dir, BuildState([r1, r2]));

            // Error at 11 → near R-001 (delta 1)
            // Error at 31 → near R-002 (delta 1)
            // Error at 50 → no rewrite within ±3 → unattributed
            var buildOutput =
                DiagLine(file, 11, 1, "error", "CS0001", "near R-001") + "\n" +
                DiagLine(file, 31, 1, "error", "CS0002", "near R-002") + "\n" +
                DiagLine(file, 50, 1, "error", "CS0003", "far away")   + "\n";

            var stub = new StubBuildRunner(BuildResult.Completed(buildOutput, 1));
            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("R-001", stdout);
            Assert.Contains("R-002", stdout);
            Assert.Contains("Unattributed", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 19: JSON output with attributed errors ──────────────────────

    [Scenario("--json output with an attributed error contains a non-empty attribution array with the ruleId")]
    [Fact]
    public async Task Verify_Json_WithAttributedError_HasAttributionEntries()
    {
        var dir = CreateTempDir();
        try
        {
            const string file = "Foo.cs";
            var applied    = new AppliedRewrite("R-005", file, 20, "A", "B");
            var schemaPath = await WriteStateAsync(dir, BuildState([applied]));

            var diagLine = DiagLine(file, 21, 1, "error", "CS1501", "no overload");
            var stub     = new StubBuildRunner(BuildResult.Completed(diagLine, 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\" --json", stub);

            Assert.Equal(0, exitCode);
            var doc        = JsonDocument.Parse(stdout);
            var attrArray  = doc.RootElement.GetProperty("attribution");
            Assert.NotEqual(0, attrArray.GetArrayLength());
            var firstEntry = attrArray.EnumerateArray().First();
            Assert.Equal("R-005", firstEntry.GetProperty("ruleId").GetString());
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 20: --verbose lists per-diagnostic details ─────────────────

    [Scenario("--verbose lists per-diagnostic file/line/code details for attributed errors")]
    [Fact]
    public async Task Verify_Verbose_ListsPerDiagnosticDetails()
    {
        var dir = CreateTempDir();
        try
        {
            const string file = "Bar.cs";
            var applied    = new AppliedRewrite("R-010", file, 7, "X", "Y");
            var schemaPath = await WriteStateAsync(dir, BuildState([applied]));

            var diagLine = DiagLine(file, 8, 3, "error", "CS0200", "read-only property");
            var stub     = new StubBuildRunner(BuildResult.Completed(diagLine, 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\" --verbose", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("CS0200", stdout);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 21: --no-build --json emits build.skipped sentinel ─────────

    [Scenario("--no-build --json emits a structured build.skipped=true sentinel instead of null")]
    [Fact]
    public async Task Verify_NoBuild_Json_EmitsSkippedSentinel()
    {
        var dir = CreateTempDir();
        try
        {
            var schemaPath = await WriteStateAsync(dir, BuildState());

            // Stub would return errors; --no-build must short-circuit so this is never called.
            var stub = new StubBuildRunner(BuildResult.Completed("noise", 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\" --no-build --json", stub);

            Assert.Equal(0, exitCode);

            var doc = JsonDocument.Parse(stdout);
            var buildEl = doc.RootElement.GetProperty("build");

            // Must be a structured object, not null
            Assert.Equal(JsonValueKind.Object, buildEl.ValueKind);

            // skipped must be true and a reason must be present
            Assert.True(buildEl.GetProperty("skipped").GetBoolean());
            Assert.Equal("--no-build flag set", buildEl.GetProperty("reason").GetString());

            // exitCode is null (not present as a non-null int) so consumers know not to read it
            Assert.Equal(JsonValueKind.Null, buildEl.GetProperty("exitCode").ValueKind);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 22: Auto-detect schema when --schema omitted ───────────────

    [Scenario("When --schema is omitted the command auto-detects the most recent state file")]
    [Fact]
    public async Task Verify_AutoDetectsSchemaFromStateFile()
    {
        var dir = CreateTempDir();
        try
        {
            // Write schema + state file with a known applied rewrite
            const string file = "Foo.cs";
            var applied = new AppliedRewrite("R-AUTO", file, 10, "A", "B",
                new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero));
            await WriteStateAsync(dir, BuildState([applied]));

            // No --schema given; should auto-detect from --project-dir
            var diagLine = DiagLine(file, 11, 1, "error", "CS0001", "near R-AUTO");
            var stub = new StubBuildRunner(BuildResult.Completed(diagLine, 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            Assert.Contains("R-AUTO", stdout);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 23: Auto-detect when schema file missing but state file exists ──

    [Scenario("Auto-detect works when only the state file exists (schema deleted) — runs in state-only mode")]
    [Fact]
    public async Task Verify_AutoDetect_StateExists_SchemaMissing_ProceedsGracefully()
    {
        var dir = CreateTempDir();
        try
        {
            // Write schema + state, then delete the schema file to simulate
            // schema-deleted-but-state-retained scenarios (e.g. cleanup script).
            var applied = new AppliedRewrite("R-001", "Foo.cs", 10, "A", "B");
            var schemaPath = await WriteStateAsync(dir, BuildState([applied]));
            File.Delete(schemaPath);
            Assert.False(File.Exists(schemaPath), "schema file should be deleted for this scenario");

            // State sibling still exists
            Assert.True(File.Exists(schemaPath + ".state.json"), "state file must remain");

            var stub = new StubBuildRunner(BuildResult.Completed(string.Empty, 0));

            // Auto-detect via --project-dir (no --schema)
            var (exitCode, stdout, _) = await InvokeAsync($"--project-dir \"{dir}\"", stub);

            Assert.Equal(0, exitCode);
            // The state-file's recorded rule appears (via state.Applied being read)
            // and the command runs through to attribution without crashing.
            Assert.Contains("verify", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally { SafeDelete(dir); }
    }

    // ── Scenario 24: Regex accepts wider NETSDK-style codes ──────────────────

    [Scenario("DiagnosticParser regex accepts 6-letter NETSDK-style diagnostic codes")]
    [Fact]
    public async Task Verify_Parses_NetSdkLongCode()
    {
        var dir = CreateTempDir();
        try
        {
            const string file = "Foo.cs";
            var applied    = new AppliedRewrite("R-001", file, 10, "A", "B");
            var schemaPath = await WriteStateAsync(dir, BuildState([applied]));

            // NETSDK1138 is a 6-letter-prefix code; the old [A-Za-z]{1,3} regex would reject it.
            var diagLine = DiagLine(file, 11, 1, "error", "NETSDK1138", "long-prefix code");
            var stub = new StubBuildRunner(BuildResult.Completed(diagLine, 1));

            var (exitCode, stdout, _) = await InvokeAsync(
                $"--schema \"{schemaPath}\" --project-dir \"{dir}\" --verbose", stub);

            Assert.Equal(0, exitCode);
            // The diagnostic must have been parsed AND attributed (delta = 1)
            Assert.Contains("R-001", stdout);
            Assert.Contains("NETSDK1138", stdout);
        }
        finally { SafeDelete(dir); }
    }
}
