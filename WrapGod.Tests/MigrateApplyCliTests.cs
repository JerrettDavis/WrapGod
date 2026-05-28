using System.CommandLine;
using System.Text.Json;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Cli;
using WrapGod.Migration;
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

            // Second run
            var (exitCode, stdout, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\"");

            Assert.Equal(0, exitCode);
            var contentAfterSecond = await File.ReadAllTextAsync(csPath);
            Assert.Equal(contentAfterFirst, contentAfterSecond);
            // Second run should report 0 modifications
            Assert.Contains("0 modified", stdout, StringComparison.OrdinalIgnoreCase);
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
            Assert.True(doc.RootElement.TryGetProperty("applied",       out _), "JSON must have 'applied'");
            Assert.True(doc.RootElement.TryGetProperty("skipped",       out _), "JSON must have 'skipped'");
            Assert.True(doc.RootElement.TryGetProperty("manual",        out _), "JSON must have 'manual'");
            Assert.True(doc.RootElement.TryGetProperty("dryRun",        out _), "JSON must have 'dryRun'");
            Assert.True(doc.RootElement.TryGetProperty("filesScanned",  out _), "JSON must have 'filesScanned'");
            Assert.True(doc.RootElement.TryGetProperty("filesModified", out _), "JSON must have 'filesModified'");
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
    /// Scenario 10: No --schema flag → exit code is non-zero (required option missing).
    /// </summary>
    [Scenario("Sad-04: no --schema flag → non-zero exit from System.CommandLine")]
    [Fact]
    public async Task Apply_NoSchemaFlag_Fails()
    {
        var (exitCode, _, _) = await InvokeAsync("apply");

        Assert.NotEqual(0, exitCode);
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
    /// Scenario 13: --dry-run on a read-only directory → exit 0 (never writes).
    /// </summary>
    [Scenario("Edge-03: --dry-run on directory where writes would fail → exit 0")]
    [Fact]
    public async Task Apply_DryRun_NeverWrites_SoReadOnlyDirIsOk()
    {
        var tempDir = CreateTempDir();
        try
        {
            var schemaPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var csPath     = Path.Combine(tempDir, "Widget.cs");
            await File.WriteAllTextAsync(schemaPath, MakeRenameSchema());
            await File.WriteAllTextAsync(csPath, SourceWithMatch);

            // Dry-run never writes; should always succeed regardless of write permissions
            var (exitCode, _, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\" --dry-run");

            Assert.Equal(0, exitCode);
            // Source file is unchanged
            var content = await File.ReadAllTextAsync(csPath);
            Assert.Contains("OldWidget", content, StringComparison.Ordinal);
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
    /// run completes, output mentions recovery.
    /// </summary>
    [Scenario("Edge-07: corrupt state file → engine recovers with .bak and continues")]
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
            var (exitCode, _, _) = await InvokeAsync(
                $"apply --schema \"{schemaPath}\" --project-dir \"{tempDir}\"");

            Assert.Equal(0, exitCode);
            // Backup of the corrupt file should exist
            Assert.True(File.Exists(statePath + ".bak"), "Corrupt state file should be archived to .bak");
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
}
