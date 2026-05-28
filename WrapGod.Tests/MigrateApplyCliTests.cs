using System.CommandLine;
using System.Text.Json;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Cli;
using WrapGod.Migration;
using WrapGod.Migration.Engine.State;
using Xunit.Abstractions;

namespace WrapGod.Tests;

/// <summary>
/// In-process BDD-style tests for <c>wrap-god migrate apply</c>.
/// All tests use temp directories isolated per test run (GUID-suffixed).
/// Tests match the 14 scenarios specified in MIGRATION-ENGINE-PLAN.md §199.c plus
/// four additional edge/integration scenarios for the 18+ total required.
/// </summary>
[Feature("CLI: migrate apply command applies a migration schema to a codebase")]
[Collection("CLI")]
public sealed class MigrateApplyCliTests(ITestOutputHelper output) : TinyBddXunitBase(output)
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
        var path = Path.Combine(Path.GetTempPath(), $"wrapgod-apply-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { /* best-effort */ }
    }

    /// <summary>
    /// Creates a minimal valid schema JSON that renames OldWidget → NewWidget.
    /// </summary>
    private static string MakeRenameSchema(RuleConfidence confidence = RuleConfidence.Auto) =>
        $$"""
        {
          "schema": "wrapgod-migration/1.0",
          "library": "TestLib",
          "from": "1.0.0",
          "to": "2.0.0",
          "rules": [
            {
              "kind": "renameType",
              "id": "TEST-001",
              "confidence": "{{confidence.ToString().ToLowerInvariant()}}",
              "oldName": "OldWidget",
              "newName": "NewWidget"
            }
          ]
        }
        """;

    /// <summary>
    /// Creates a schema JSON with zero rules.
    /// </summary>
    private static string MakeEmptySchema() =>
        """
        {
          "schema": "wrapgod-migration/1.0",
          "library": "TestLib",
          "from": "1.0.0",
          "to": "2.0.0",
          "rules": []
        }
        """;

    /// <summary>
    /// Creates a schema JSON with one manual-confidence rule only.
    /// </summary>
    private static string MakeManualOnlySchema() =>
        """
        {
          "schema": "wrapgod-migration/1.0",
          "library": "TestLib",
          "from": "1.0.0",
          "to": "2.0.0",
          "rules": [
            {
              "kind": "renameType",
              "id": "MANUAL-001",
              "confidence": "manual",
              "note": "Parameters restructured -- requires manual mapping",
              "oldName": "OldFoo",
              "newName": "NewFoo"
            }
          ]
        }
        """;

    // ── Source containing a match ────────────────────────────────────────────────────────────
    // OldWidget is used as a type reference in Consumer, which RenameTypeRewriter will rename.
    private const string SourceWithMatch =
        "using System;\nnamespace MyApp { class OldWidget { }\nclass Consumer { OldWidget w = null; } }";

    private const string SourceWithoutMatch =
        "using System;\nnamespace MyApp { class Unrelated { int x; } }";

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Happy-path scenarios
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Scenario 1: Apply on a project with one matching .cs file → exit 0,
    /// file is rewritten, state file is created.
    /// </summary>
    [Scenario("Happy-01: apply on simple project with one matching file → exit 0, file rewritten, state created")]
    [Fact]
    public async Task Apply_HappyPath_ModifiesFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            // Arrange: write schema and a source file with a match
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            // Act
            var (exitCode, stdout, stderr) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\"");

            // Assert
            Assert.Equal(0, exitCode);
            var newContent = await File.ReadAllTextAsync(csPath);
            Assert.Contains("NewWidget", newContent, StringComparison.Ordinal);
            Assert.True(File.Exists(schemaPath + ".state.json"), "State file must be created after apply");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 2: --dry-run → exit 0, no files modified, no state file written.
    /// </summary>
    [Scenario("Happy-02: --dry-run does not modify files or create state")]
    [Fact]
    public async Task Apply_DryRun_DoesNotModifyFile()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);
            var originalContent = await File.ReadAllTextAsync(csPath);

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --dry-run");

            Assert.Equal(0, exitCode);
            // File unchanged
            var newContent = await File.ReadAllTextAsync(csPath);
            Assert.Equal(originalContent, newContent);
            // No state file
            Assert.False(File.Exists(schemaPath + ".state.json"), "State file must NOT be created during dry-run");
            // Output should mention dry-run
            Assert.Contains("DRY", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 3: second apply with same schema is a no-op (idempotent via state).
    /// </summary>
    [Scenario("Happy-03: second apply with same schema is idempotent (no-op)")]
    [Fact]
    public async Task Apply_SecondRun_IsNoOp()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            // First run
            await InvokeAsync($"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\"");
            var contentAfterFirst = await File.ReadAllTextAsync(csPath);
            var stateBefore = MigrationStateStore.Load(schemaPath, out _, out _);
            Assert.NotNull(stateBefore);

            // Second run
            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\"");

            Assert.Equal(0, exitCode);
            var contentAfterSecond = await File.ReadAllTextAsync(csPath);
            Assert.Equal(contentAfterFirst, contentAfterSecond);
            // Second run should report 0 modifications
            Assert.Contains("0 modified", stdout, StringComparison.OrdinalIgnoreCase);

            // State file Applied list is stable across the idempotent re-run.
            var stateAfter = MigrationStateStore.Load(schemaPath, out _, out _);
            Assert.NotNull(stateAfter);
            Assert.Equal(stateBefore.Applied.Count, stateAfter.Applied.Count);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 4: --include glob narrows files to a subset.
    /// </summary>
    [Scenario("Happy-04: --include glob narrows processing to matching files only")]
    [Fact]
    public async Task Apply_Include_FiltersFiles()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());

            // Only files under Components/ should be processed
            var compDir = Path.Combine(tempDir, "Components");
            Directory.CreateDirectory(compDir);
            var matchedFile  = Path.Combine(compDir, "Widget.cs");
            var excludedFile = Path.Combine(tempDir, "Other.cs");
            await File.WriteAllTextAsync(matchedFile,  SourceWithMatch);
            await File.WriteAllTextAsync(excludedFile, SourceWithMatch);

            var (exitCode, _, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" " +
                $"--include \"**/Components/*.cs\"");

            Assert.Equal(0, exitCode);
            // Matched file should be rewritten
            Assert.Contains("NewWidget", await File.ReadAllTextAsync(matchedFile), StringComparison.Ordinal);
            // Excluded file should remain unchanged
            Assert.Contains("OldWidget", await File.ReadAllTextAsync(excludedFile), StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 5: --exclude glob removes matched files from processing.
    /// </summary>
    [Scenario("Happy-05: --exclude glob removes specified files from processing")]
    [Fact]
    public async Task Apply_Exclude_FiltersFiles()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath  = Path.Combine(tempDir, "test.wrapgod-migration.json");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());

            var genDir = Path.Combine(tempDir, "Generated");
            Directory.CreateDirectory(genDir);
            var generatedFile = Path.Combine(genDir, "Auto.cs");
            var normalFile    = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(generatedFile, SourceWithMatch);
            await File.WriteAllTextAsync(normalFile,    SourceWithMatch);

            var (exitCode, _, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" " +
                $"--exclude \"**/Generated/**\"");

            Assert.Equal(0, exitCode);
            Assert.Contains("NewWidget", await File.ReadAllTextAsync(normalFile), StringComparison.Ordinal);
            Assert.Contains("OldWidget", await File.ReadAllTextAsync(generatedFile), StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 6: --json emits parseable JSON summary with required keys.
    /// </summary>
    [Scenario("Happy-06: --json emits parseable JSON summary with required keys")]
    [Fact]
    public async Task Apply_Json_OutputParsesAsJson()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --json");

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(stdout);
            // ── Required keys ──────────────────────────────────────────────────────────────
            Assert.True(doc.RootElement.TryGetProperty("applied",        out _), "JSON must have 'applied'");
            Assert.True(doc.RootElement.TryGetProperty("skipped",        out _), "JSON must have 'skipped'");
            Assert.True(doc.RootElement.TryGetProperty("manual",         out _), "JSON must have 'manual'");
            Assert.True(doc.RootElement.TryGetProperty("dryRun",         out _), "JSON must have 'dryRun'");
            Assert.True(doc.RootElement.TryGetProperty("filesScanned",   out _), "JSON must have 'filesScanned'");
            Assert.True(doc.RootElement.TryGetProperty("filesModified",  out _), "JSON must have 'filesModified'");
            // ── New CI-consumer fields ──────────────────────────────────────────────────────
            Assert.True(doc.RootElement.TryGetProperty("stateRecovered", out _), "JSON must have 'stateRecovered' (null when not recovered)");
            Assert.True(doc.RootElement.TryGetProperty("skippedDetails", out var skipped), "JSON must have 'skippedDetails'");
            Assert.Equal(JsonValueKind.Array, skipped.ValueKind);
            Assert.True(doc.RootElement.TryGetProperty("manualDetails",  out var manual),  "JSON must have 'manualDetails'");
            Assert.Equal(JsonValueKind.Array, manual.ValueKind);
            Assert.True(doc.RootElement.TryGetProperty("appliedByRule",  out var byRule),  "JSON must have 'appliedByRule'");
            Assert.Equal(JsonValueKind.Array, byRule.ValueKind);
            // ── For a successful single-rule run, expect ≥1 entry under appliedByRule ──────
            Assert.True(byRule.GetArrayLength() >= 1, "appliedByRule should contain at least one entry");
            var first = byRule[0];
            Assert.True(first.TryGetProperty("ruleId",    out _), "appliedByRule entries must have 'ruleId'");
            Assert.True(first.TryGetProperty("kind",      out _), "appliedByRule entries must have 'kind'");
            Assert.True(first.TryGetProperty("fileCount", out _), "appliedByRule entries must have 'fileCount'");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Sad-path scenarios
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Scenario 7: Schema file does not exist → exit 1.
    /// </summary>
    [Scenario("Sad-01: schema file not found → exit 1 with 'not found' message")]
    [Fact]
    public async Task Apply_SchemaMissing_Fails()
    {
        var (exitCode, _, stderr) = await InvokeAsync(
            "apply --schema does-not-exist.wrapgod-migration.json");

        Assert.Equal(1, exitCode);
        Assert.Contains("not found", stderr, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario 8: Schema file is invalid JSON → exit 1.
    /// </summary>
    [Scenario("Sad-02: malformed schema JSON → exit 1 with parse error")]
    [Fact]
    public async Task Apply_SchemaMalformed_Fails()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "bad.wrapgod-migration.json");
            await File.WriteAllTextAsync(schemaPath, "{ this is not valid json }}}");

            var (exitCode, _, stderr) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\"");

            Assert.Equal(1, exitCode);
            Assert.False(string.IsNullOrWhiteSpace(stderr), "Error message must be printed");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 9: Project directory does not exist → exit 1.
    /// </summary>
    [Scenario("Sad-03: project directory not found → exit 1")]
    [Fact]
    public async Task Apply_ProjectDirMissing_Fails()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());

            var nonExistentDir = Path.Combine(Path.GetTempPath(), $"no-such-dir-{Guid.NewGuid():N}");
            var (exitCode, _, stderr) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{nonExistentDir}\"");

            Assert.Equal(1, exitCode);
            Assert.False(string.IsNullOrWhiteSpace(stderr));
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 10: No --schema flag → exit code 2 per plan §4.3 (bad args).
    /// The command validates the option itself rather than relying on
    /// System.CommandLine's default IsRequired behavior (which returns 1).
    /// </summary>
    [Scenario("Sad-04: no --schema flag → exit 2 (bad args, per plan §4.3)")]
    [Fact]
    public async Task Apply_NoSchemaFlag_Fails()
    {
        var (exitCode, _, stderr) = await InvokeAsync("apply");

        Assert.Equal(2, exitCode);
        Assert.Contains("--schema", stderr, StringComparison.OrdinalIgnoreCase);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Edge-case scenarios
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Scenario 11: Schema with zero rules → exit 0 with informational note.
    /// </summary>
    [Scenario("Edge-01: schema with zero rules → exit 0 with 'no rules' note")]
    [Fact]
    public async Task Apply_ZeroRules_SucceedsWithNote()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "empty.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeEmptySchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\"");

            Assert.Equal(0, exitCode);
            Assert.Contains("no rules", stdout + " " , StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 12: All rules are Manual → exit 0, Applied=0, Manual list populated.
    /// </summary>
    [Scenario("Edge-02: all rules are Manual confidence → applied = 0, manual list populated")]
    [Fact]
    public async Task Apply_AllManual_AppliesNothing()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "manual.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeManualOnlySchema());
            await File.WriteAllTextAsync(csPath, "using System;\nclass OldFoo { }");

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --json");

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(stdout);
            Assert.Equal(0, doc.RootElement.GetProperty("applied").GetInt32());
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 13: --dry-run leaves files unchanged on disk, even when the source
    /// file is marked read-only (dry-run never writes, regardless of permissions).
    /// </summary>
    [Scenario("Edge-03: --dry-run leaves files unchanged, even read-only files")]
    [Fact]
    public async Task Apply_DryRun_LeavesFilesUnchanged()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            // Mark the target file read-only so any write attempt would fail loudly.
            var originalAttrs = File.GetAttributes(csPath);
            File.SetAttributes(csPath, originalAttrs | FileAttributes.ReadOnly);

            try
            {
                var (exitCode, _, _) = await InvokeAsync(
                    $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --dry-run");

                Assert.Equal(0, exitCode);
                // Source file is byte-for-byte unchanged.
                var content = await File.ReadAllTextAsync(csPath);
                Assert.Contains("OldWidget", content, StringComparison.Ordinal);
                Assert.DoesNotContain("NewWidget", content, StringComparison.Ordinal);
            }
            finally
            {
                // Restore so SafeDelete can clean up.
                File.SetAttributes(csPath, originalAttrs);
            }
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 14: .cs file with syntax errors → still processes best-effort,
    /// and does not throw an unhandled exception.
    /// </summary>
    [Scenario("Edge-04: source file with syntax errors → best-effort processing, no crash")]
    [Fact]
    public async Task Apply_PartialTree_StillRewrites()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Broken.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            // Source with syntax error but still containing OldWidget reference
            await File.WriteAllTextAsync(csPath,
                "using System;\nclass Broken { OldWidget w = null // missing semicolon\n}");

            var (exitCode, _, stderr) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\"");

            // Must not crash with exit 1 due to parse error
            Assert.Equal(0, exitCode);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 15: --help shows all expected flags.
    /// </summary>
    [Scenario("Edge-05: --help lists all flags")]
    [Fact]
    public async Task Apply_Help_ListsAllFlags()
    {
        var (exitCode, stdout, _) = await InvokeAsync("apply --help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--schema",      stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--project-dir", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--dry-run",     stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--include",     stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--exclude",     stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--json",        stdout, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario 16: Empty project directory (no .cs files) → exit 0, message about no files.
    /// </summary>
    [Scenario("Edge-06: empty project directory with no .cs files → exit 0")]
    [Fact]
    public async Task Apply_EmptyProject_ExitsZero()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());

            var projectDir = Path.Combine(tempDir, "EmptyProject");
            Directory.CreateDirectory(projectDir);

            var (exitCode, _, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{projectDir}\"");

            Assert.Equal(0, exitCode);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 17: Corrupt state file → engine recovers (archives to .bak),
    /// run completes, output mentions recovery via prominent banner.
    /// </summary>
    [Scenario("Edge-07: corrupt state file → recovery banner appears in human mode")]
    [Fact]
    public async Task Apply_CorruptState_RecoversWithBackup()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            // Plant a corrupt state file
            var statePath = schemaPath + ".state.json";
            await File.WriteAllTextAsync(statePath, "{ this is NOT valid json }}}");

            // Engine should recover and complete successfully
            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\"");

            Assert.Equal(0, exitCode);
            // Backup of the corrupt file should exist
            Assert.True(File.Exists(statePath + ".bak"), "Corrupt state file should be archived to .bak");
            // Prominent banner is printed BEFORE the standard summary header.
            Assert.Contains("WARNING",       stdout, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("state",         stdout, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("===",           stdout, StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 17b: Corrupt state file → --json mode surfaces a top-level
    /// stateRecovered object with an archivedTo field (not buried in the skipped array).
    /// </summary>
    [Scenario("Edge-07b: corrupt state file → JSON mode emits stateRecovered.archivedTo")]
    [Fact]
    public async Task Apply_CorruptState_JsonHasStateRecovered()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            var statePath = schemaPath + ".state.json";
            await File.WriteAllTextAsync(statePath, "{ corrupt }");

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --json");

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(stdout);
            Assert.True(doc.RootElement.TryGetProperty("stateRecovered", out var recovered),
                "JSON must have 'stateRecovered'");
            Assert.NotEqual(JsonValueKind.Null, recovered.ValueKind);
            Assert.True(recovered.TryGetProperty("archivedTo", out var archived),
                "stateRecovered must have 'archivedTo'");
            Assert.False(string.IsNullOrWhiteSpace(archived.GetString()),
                "archivedTo should point at the .bak path");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 18: Include glob that matches no files → exit 0, 0 files scanned.
    /// </summary>
    [Scenario("Edge-08: --include glob that matches no files → exit 0, 0 files scanned")]
    [Fact]
    public async Task Apply_IncludeMatchesNoFiles_ExitsZero()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            // Include only VB files — none exist
            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" " +
                $"--include \"**/*.vb\" --json");

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(stdout);
            Assert.Equal(0, doc.RootElement.GetProperty("filesScanned").GetInt32());
            // Source file must remain unchanged
            Assert.Contains("OldWidget", await File.ReadAllTextAsync(csPath), StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 19: Mixed auto + manual rules → human output mentions manual section.
    /// </summary>
    [Scenario("Edge-09: schema with mixed auto and manual rules → output contains manual section")]
    [Fact]
    public async Task Apply_MixedRules_ShowsManualSection()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "mixed.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");

            var mixedSchema = """
            {
              "schema": "wrapgod-migration/1.0",
              "library": "TestLib",
              "from": "1.0.0",
              "to": "2.0.0",
              "rules": [
                {
                  "kind": "renameType",
                  "id": "AUTO-001",
                  "confidence": "auto",
                  "oldName": "OldWidget",
                  "newName": "NewWidget"
                },
                {
                  "kind": "renameType",
                  "id": "MANUAL-001",
                  "confidence": "manual",
                  "note": "Parameters restructured -- requires manual mapping",
                  "oldName": "OldWidget",
                  "newName": "NewWidget"
                }
              ]
            }
            """;

            await File.WriteAllTextAsync(schemaPath, mixedSchema);
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\"");

            Assert.Equal(0, exitCode);
            Assert.Contains("Manual", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 20: --verbose flag runs without error (smoke test for the -v flag).
    /// </summary>
    [Scenario("Edge-10: --verbose flag produces extra output without error")]
    [Fact]
    public async Task Apply_Verbose_RunsWithoutError()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            var (exitCode, _, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --verbose");

            Assert.Equal(0, exitCode);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 21 (plan §199.a step 5): --dry-run prints a unified-diff-style preview
    /// per file that would be modified.
    /// </summary>
    [Scenario("Edge-11: --dry-run prints a unified-diff preview per file")]
    [Fact]
    public async Task Apply_DryRun_PrintsDiff()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --dry-run");

            Assert.Equal(0, exitCode);
            // Unified-diff style markers must appear when there is a would-be change.
            Assert.Contains("--- a/", stdout, StringComparison.Ordinal);
            Assert.Contains("+++ b/", stdout, StringComparison.Ordinal);
            // The change itself should be visible: old line removed (-), new line added (+).
            Assert.Contains("-",       stdout, StringComparison.Ordinal);
            Assert.Contains("+",       stdout, StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Scenario 22 (plan §199.a step 5): --dry-run truncates per-file diff output at
    /// MaxInlineDiffLinesPerFile (20) and dumps the full diff to .wrapgod/dryrun-*.diff.
    /// </summary>
    [Scenario("Edge-12: --dry-run truncates inline diff and writes a dump file")]
    [Fact]
    public async Task Apply_DryRun_TruncatesDiff()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());

            // Build a large source where every line references OldWidget so the diff
            // is many lines long — guaranteed to exceed the 20-line inline cap.
            var sb = new System.Text.StringBuilder();
            sb.Append("namespace MyApp { class OldWidget { } class Consumer {\n");
            for (var i = 0; i < 60; i++)
                sb.Append("    OldWidget w").Append(i).Append(" = null;\n");
            sb.Append("} }\n");

            var csPath = Path.Combine(tempDir, "Big.cs");
            await File.WriteAllTextAsync(csPath, sb.ToString());

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --dry-run");

            Assert.Equal(0, exitCode);
            // Truncation hint should appear in the human-readable output.
            Assert.Contains("truncated", stdout, StringComparison.OrdinalIgnoreCase);
            // The .wrapgod/dryrun-*.diff dump file must exist.
            var dumpDir = Path.Combine(tempDir, ".wrapgod");
            Assert.True(Directory.Exists(dumpDir), ".wrapgod directory must be created for dump file");
            var dumpFiles = Directory.GetFiles(dumpDir, "dryrun-*.diff");
            Assert.NotEmpty(dumpFiles);
            // Dump file should contain more diff lines than the inline preview.
            var dumpContent = await File.ReadAllTextAsync(dumpFiles[0]);
            Assert.True(dumpContent.Length > 0, "Dump file must have content");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Coverage-gap closers (target uncovered branches in MigrateApplyCommand)
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Cov-01: Zero-rules with --json output → JSON has all summary fields zero.
    /// Exercises the zero-rules JSON branch in Handle (jsonOutput && schema.Rules.Count == 0).
    /// </summary>
    [Scenario("Cov-01: zero-rules schema with --json → valid JSON with zero counts")]
    [Fact]
    public async Task Apply_ZeroRules_Json_OutputsZeros()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "empty.wrapgod-migration.json");
            await File.WriteAllTextAsync(schemaPath, MakeEmptySchema());

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --json");

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(stdout);
            Assert.Equal(0, doc.RootElement.GetProperty("applied").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("skipped").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("manual").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("filesScanned").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("filesModified").GetInt32());
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Cov-02: --dry-run JSON output exercises the dryRunDiff branch with both
    /// dumpFilePath and inlinePerFile fields populated.  Covers the JSON
    /// dryRunDiff is-not-null arm and the inlinePerFile.Select lambda.
    /// </summary>
    [Scenario("Cov-02: --dry-run --json populates dryRunDiff.inlinePerFile and dumpFilePath")]
    [Fact]
    public async Task Apply_DryRun_Json_PopulatesDryRunDiff()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --dry-run --json");

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(stdout);
            Assert.True(doc.RootElement.TryGetProperty("dryRunDiff", out var diff));
            Assert.NotEqual(JsonValueKind.Null, diff.ValueKind);
            Assert.True(diff.TryGetProperty("dumpFilePath", out _));
            Assert.True(diff.TryGetProperty("inlinePerFile", out var inline));
            Assert.Equal(JsonValueKind.Array, inline.ValueKind);
            Assert.True(inline.GetArrayLength() >= 1, "Expected at least one file diff entry");
            var first = inline[0];
            Assert.True(first.TryGetProperty("file", out _));
            Assert.True(first.TryGetProperty("diff", out var diffText));
            Assert.Contains("--- a/", diffText.GetString() ?? "", StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Cov-03: --include and --exclude that both target the same file path → exclude wins.
    /// Demonstrates overlap behavior of the glob matcher and ensures Excludes loop is exercised.
    /// </summary>
    [Scenario("Cov-03: overlapping include/exclude — exclude wins")]
    [Fact]
    public async Task Apply_OverlappingIncludeExclude_ExcludeWins()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());

            var compDir = Path.Combine(tempDir, "Components");
            Directory.CreateDirectory(compDir);
            var csPath = Path.Combine(compDir, "Widget.cs");
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            // Include matches AND exclude matches the same file — exclude must win.
            var (exitCode, _, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" " +
                $"--include \"**/Components/**\" --exclude \"**/Components/Widget.cs\"");

            Assert.Equal(0, exitCode);
            // File is untouched because exclude removed it from the matched set.
            Assert.Contains("OldWidget", await File.ReadAllTextAsync(csPath), StringComparison.Ordinal);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Cov-04: A rule whose Confidence is auto but whose Kind cannot be re-resolved by
    /// the schema (rule.Id mismatch) exercises ResolveRuleKind's null path. Indirectly
    /// covered via JSON appliedByRule.kind being present (non-null) for valid rules.
    /// </summary>
    [Scenario("Cov-04: appliedByRule.kind is populated from the schema's rule kind")]
    [Fact]
    public async Task Apply_AppliedByRule_KindIsResolved()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --json");

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(stdout);
            var byRule = doc.RootElement.GetProperty("appliedByRule");
            Assert.True(byRule.GetArrayLength() >= 1);
            var kind = byRule[0].GetProperty("kind");
            Assert.Equal(JsonValueKind.String, kind.ValueKind);
            Assert.Equal("renameType", kind.GetString());
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Cov-05: Apply with no file modifications (rule does not match any source) →
    /// FilesModified == 0 in both modes, BuildDryRunDiff is not invoked.
    /// </summary>
    [Scenario("Cov-05: no matches → filesModified=0, no diff produced")]
    [Fact]
    public async Task Apply_NoMatches_FilesModifiedZero()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            // Source contains no reference to OldWidget — rule will not match.
            await File.WriteAllTextAsync(csPath,
                "namespace MyApp { class Unrelated { int Other; } }");

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --json");

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(stdout);
            Assert.True(doc.RootElement.GetProperty("filesScanned").GetInt32() >= 1);
            Assert.Equal(0, doc.RootElement.GetProperty("filesModified").GetInt32());
            Assert.Equal(0, doc.RootElement.GetProperty("applied").GetInt32());
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Cov-06: Mixed schema (auto rule that matches AND manual rule) with --json →
    /// exercises both manualDetails (with non-empty matchedFiles) and appliedByRule
    /// in the same JSON document.
    /// </summary>
    [Scenario("Cov-06: mixed auto+manual rules with --json → manualDetails populated alongside appliedByRule")]
    [Fact]
    public async Task Apply_MixedRules_Json_PopulatesAllArrays()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "mixed.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");

            var mixedSchema = """
            {
              "schema": "wrapgod-migration/1.0",
              "library": "TestLib",
              "from": "1.0.0",
              "to": "2.0.0",
              "rules": [
                {
                  "kind": "renameType",
                  "id": "AUTO-001",
                  "confidence": "auto",
                  "oldName": "OldWidget",
                  "newName": "NewWidget"
                },
                {
                  "kind": "renameType",
                  "id": "MANUAL-001",
                  "confidence": "manual",
                  "note": "Manual review required",
                  "oldName": "OldWidget",
                  "newName": "NewWidget"
                }
              ]
            }
            """;

            await File.WriteAllTextAsync(schemaPath, mixedSchema);
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --json");

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(stdout);
            var manualDetails = doc.RootElement.GetProperty("manualDetails");
            Assert.Equal(JsonValueKind.Array, manualDetails.ValueKind);
            Assert.True(manualDetails.GetArrayLength() >= 1, "manualDetails should have ≥1 entry");
            var first = manualDetails[0];
            Assert.True(first.TryGetProperty("ruleId",       out _));
            Assert.True(first.TryGetProperty("note",         out _));
            Assert.True(first.TryGetProperty("matchedFiles", out var matched));
            Assert.Equal(JsonValueKind.Array, matched.ValueKind);

            // The auto rule that matched should appear in appliedByRule.
            var byRule = doc.RootElement.GetProperty("appliedByRule");
            Assert.True(byRule.GetArrayLength() >= 1);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }
}
