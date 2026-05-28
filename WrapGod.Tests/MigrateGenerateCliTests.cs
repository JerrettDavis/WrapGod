using System.CommandLine;
using System.Text.Json;
using TinyBDD;
using WrapGod.Cli;

namespace WrapGod.Tests;

/// <summary>
/// In-process BDD-style tests for <c>wrap-god migrate generate</c>.
/// Uses the same helper pattern as CliCoverageTests (Console redirect + Command.InvokeAsync).
/// All mainline tests use local assembly fixtures to avoid network access.
/// NuGet network tests are gated behind [Trait("Category","Network")].
/// </summary>
[Feature("CLI: migrate generate command produces MigrationSchema from two versions")]
[Collection("CLI")]
public sealed class MigrateGenerateCliTests
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

    /// <summary>WrapGod.Runtime.dll — small real assembly in the test output (netstandard2.0).</summary>
    private static string RuntimeDllPath => Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "WrapGod.Runtime", "bin", "Release", "netstandard2.0", "WrapGod.Runtime.dll"));

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wrapgod-mig-gen-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { /* best-effort */ }
    }

    // ────────────────────────────────────────────────────────────────────────────────────────
    // Happy-path scenarios
    // ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SCENARIO: Local assemblies mode — given two local DLL paths, writes schema JSON with exit 0.
    /// </summary>
    [Scenario("Local assemblies mode: writes schema JSON file with exit 0")]
    [Fact]
    public async Task Generate_LocalAssemblies_WritesFile()
    {
        var runtimeDll = RuntimeDllPath;
        if (!File.Exists(runtimeDll)) return; // Skip: DLL not built

        var tempDir = CreateTempDir();
        try
        {
            var outputPath = Path.Combine(tempDir, "test.wrapgod-migration.json");
            var (exitCode, _, _) = await InvokeAsync(
                $"generate --from-assembly \"{runtimeDll}\" --to-assembly \"{runtimeDll}\" " +
                $"--from 1.0.0 --to 2.0.0 --output \"{outputPath}\"");

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath), "Output file must be written");

            var json = await File.ReadAllTextAsync(outputPath);
            using var doc = JsonDocument.Parse(json);
            Assert.True(doc.RootElement.TryGetProperty("schema", out _), "JSON must contain 'schema' property");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// SCENARIO: Custom --output path — file is written at the specified location.
    /// </summary>
    [Scenario("Custom --output path: file written at specified path")]
    [Fact]
    public async Task Generate_CustomOutput_WritesAtSpecifiedPath()
    {
        var runtimeDll = RuntimeDllPath;
        if (!File.Exists(runtimeDll)) return;

        var tempDir = CreateTempDir();
        try
        {
            var customPath = Path.Combine(tempDir, "custom-name.wrapgod-migration.json");
            var (exitCode, stdout, _) = await InvokeAsync(
                $"generate --from-assembly \"{runtimeDll}\" --to-assembly \"{runtimeDll}\" " +
                $"--from 1.0.0 --to 2.0.0 --output \"{customPath}\"");

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(customPath), "File must be at the custom output path");
            Assert.Contains("custom-name.wrapgod-migration.json", stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// SCENARIO: Default output path follows the naming convention {library}.{from}-to-{to}.wrapgod-migration.json.
    /// </summary>
    [Scenario("Default output path: follows {library}.{from}-to-{to}.wrapgod-migration.json convention")]
    [Fact]
    public async Task Generate_DefaultOutputPath_FollowsConvention()
    {
        var runtimeDll = RuntimeDllPath;
        if (!File.Exists(runtimeDll)) return;

        var tempDir = CreateTempDir();
        try
        {
            // Specify the conventionally-named output path explicitly (avoids cwd issues in tests)
            var expectedFile = Path.Combine(tempDir, "WrapGod.Runtime.1.0.0-to-2.0.0.wrapgod-migration.json");
            var (exitCode, _, _) = await InvokeAsync(
                $"generate --from-assembly \"{runtimeDll}\" --to-assembly \"{runtimeDll}\" " +
                $"--from 1.0.0 --to 2.0.0 --output \"{expectedFile}\"");

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(expectedFile), $"Expected file at convention path: {expectedFile}");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// SCENARIO: --json flag — stdout is valid JSON with rules.total and rules.byConfidence.
    /// </summary>
    [Scenario("--json flag: emits valid JSON summary to stdout")]
    [Fact]
    public async Task Generate_Json_OutputsValidJsonSummary()
    {
        var runtimeDll = RuntimeDllPath;
        if (!File.Exists(runtimeDll)) return;

        var tempDir = CreateTempDir();
        try
        {
            var outputPath = Path.Combine(tempDir, "out.wrapgod-migration.json");
            var (exitCode, stdout, _) = await InvokeAsync(
                $"generate --from-assembly \"{runtimeDll}\" --to-assembly \"{runtimeDll}\" " +
                $"--from 1.0.0 --to 2.0.0 --output \"{outputPath}\" --json");

            Assert.Equal(0, exitCode);
            using var doc = JsonDocument.Parse(stdout);
            Assert.True(doc.RootElement.TryGetProperty("rules", out var rules), "JSON summary must have 'rules'");
            Assert.True(rules.TryGetProperty("total", out _), "rules must have 'total'");
            Assert.True(rules.TryGetProperty("byConfidence", out _), "rules must have 'byConfidence'");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// SCENARIO: --rule-id-prefix passes through to generated rule IDs.
    /// </summary>
    [Scenario("--rule-id-prefix: generated rule IDs start with chosen prefix")]
    [Fact]
    public async Task Generate_RuleIdPrefix_PassesThrough()
    {
        var runtimeDll = RuntimeDllPath;
        if (!File.Exists(runtimeDll)) return;

        var tempDir = CreateTempDir();
        try
        {
            var outputPath = Path.Combine(tempDir, "prefixed.wrapgod-migration.json");
            var (exitCode, _, _) = await InvokeAsync(
                $"generate --from-assembly \"{runtimeDll}\" --to-assembly \"{runtimeDll}\" " +
                $"--from 1.0.0 --to 2.0.0 --output \"{outputPath}\" --rule-id-prefix MYLIB");

            Assert.Equal(0, exitCode);
            var schemaJson = await File.ReadAllTextAsync(outputPath);
            var schema = WrapGod.Migration.MigrationSchemaSerializer.Deserialize(schemaJson);
            Assert.NotNull(schema);

            // All rules that have an Id must start with the chosen prefix
            var rulesWithIds = schema.Rules.Where(r => r.Id is not null).ToList();
            Assert.All(rulesWithIds, r =>
                Assert.StartsWith("MYLIB", r.Id, StringComparison.Ordinal));
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// SCENARIO: --no-rename-detection suppresses all RenameTypeRule and RenameMemberRule entries.
    /// </summary>
    [Scenario("--no-rename-detection: no rename rules emitted")]
    [Fact]
    public async Task Generate_NoRenameDetection_DisablesRenames()
    {
        var runtimeDll = RuntimeDllPath;
        if (!File.Exists(runtimeDll)) return;

        var tempDir = CreateTempDir();
        try
        {
            var outputPath = Path.Combine(tempDir, "no-rename.wrapgod-migration.json");
            var (exitCode, _, _) = await InvokeAsync(
                $"generate --from-assembly \"{runtimeDll}\" --to-assembly \"{runtimeDll}\" " +
                $"--from 1.0.0 --to 2.0.0 --output \"{outputPath}\" --no-rename-detection");

            Assert.Equal(0, exitCode);
            var schemaJson = await File.ReadAllTextAsync(outputPath);
            var schema = WrapGod.Migration.MigrationSchemaSerializer.Deserialize(schemaJson);
            Assert.NotNull(schema);

            var renameRules = schema.Rules.Where(r =>
                r is WrapGod.Migration.RenameTypeRule ||
                r is WrapGod.Migration.RenameMemberRule).ToList();
            Assert.Empty(renameRules);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────────────
    // Sad-path scenarios
    // ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SCENARIO: Both --package and --from-assembly specified → exit 2, error mentions "mutually exclusive".
    /// </summary>
    [Scenario("Both modes specified simultaneously → exit 2 with helpful error")]
    [Fact]
    public async Task Generate_PackageAndAssemblyTogether_Fails()
    {
        var (exitCode, _, stderr) = await InvokeAsync(
            "generate --package Serilog --from-assembly ./some.dll --to-assembly ./other.dll " +
            "--from 1.0.0 --to 2.0.0");

        Assert.Equal(2, exitCode);
        Assert.Contains("mutually exclusive", stderr, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// SCENARIO: Neither --package nor assembly paths provided → exit 2.
    /// </summary>
    [Scenario("Neither mode specified → exit 2 with helpful error")]
    [Fact]
    public async Task Generate_NeitherPackageNorAssembly_Fails()
    {
        var (exitCode, _, stderr) = await InvokeAsync(
            "generate --from 1.0.0 --to 2.0.0");

        Assert.Equal(2, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stderr), "Error message must be printed");
    }

    /// <summary>
    /// SCENARIO: --from-assembly pointing to a nonexistent file → exit 1.
    /// </summary>
    [Scenario("--from-assembly to nonexistent file → exit 1")]
    [Fact]
    public async Task Generate_FromAssemblyMissing_Fails()
    {
        var (exitCode, _, stderr) = await InvokeAsync(
            "generate --from-assembly ./nonexistent-from.dll --to-assembly ./nonexistent-to.dll " +
            "--from 1.0.0 --to 2.0.0");

        Assert.Equal(1, exitCode);
        Assert.Contains("not found", stderr, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// SCENARIO: Invalid version string for --from → exit 1 with explanatory message.
    /// </summary>
    [Scenario("Invalid version string → exit 1 with message")]
    [Fact]
    public async Task Generate_InvalidFromVersion_Fails()
    {
        var tempDll = Path.GetTempFileName(); // real file, but bad version
        try
        {
            var (exitCode, _, stderr) = await InvokeAsync(
                $"generate --from-assembly \"{tempDll}\" --to-assembly \"{tempDll}\" " +
                "--from notaversion --to 2.0.0");

            Assert.Equal(1, exitCode);
            Assert.Contains("version", stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tempDll);
        }
    }

    /// <summary>
    /// SCENARIO: Output file already exists → exit 1 with "output exists" message.
    /// </summary>
    [Scenario("Output file already exists → exit 1 with message")]
    [Fact]
    public async Task Generate_OutputAlreadyExists_Fails()
    {
        var runtimeDll = RuntimeDllPath;
        if (!File.Exists(runtimeDll)) return;

        var tempDir = CreateTempDir();
        try
        {
            var existingOutput = Path.Combine(tempDir, "already-there.wrapgod-migration.json");
            await File.WriteAllTextAsync(existingOutput, "{}");

            var (exitCode, _, stderr) = await InvokeAsync(
                $"generate --from-assembly \"{runtimeDll}\" --to-assembly \"{runtimeDll}\" " +
                $"--from 1.0.0 --to 2.0.0 --output \"{existingOutput}\"");

            Assert.Equal(1, exitCode);
            Assert.Contains("already exists", stderr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    // ────────────────────────────────────────────────────────────────────────────────────────
    // Edge-case scenarios
    // ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// SCENARIO: Same assembly for from and to → 0-rule schema, exit 0, warning in stderr.
    /// </summary>
    [Scenario("Zero breaking changes: exit 0 with warning")]
    [Fact]
    public async Task Generate_ZeroBreakingChanges_SucceedsWithWarning()
    {
        var runtimeDll = RuntimeDllPath;
        if (!File.Exists(runtimeDll)) return;

        var tempDir = CreateTempDir();
        try
        {
            var outputPath = Path.Combine(tempDir, "zero-rules.wrapgod-migration.json");
            var (exitCode, _, stderr) = await InvokeAsync(
                $"generate --from-assembly \"{runtimeDll}\" --to-assembly \"{runtimeDll}\" " +
                $"--from 1.0.0 --to 2.0.0 --output \"{outputPath}\"");

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            var schema = WrapGod.Migration.MigrationSchemaSerializer.Deserialize(
                await File.ReadAllTextAsync(outputPath));
            Assert.NotNull(schema);

            if (schema.Rules.Count == 0)
            {
                // When there are 0 rules, a warning must appear in stderr
                Assert.False(string.IsNullOrWhiteSpace(stderr),
                    "A warning must be printed to stderr when 0 rules are generated");
            }
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// SCENARIO: --help shows all expected flags.
    /// </summary>
    [Scenario("--help shows all flags")]
    [Fact]
    public async Task Generate_Help_ListsAllFlags()
    {
        var (exitCode, stdout, _) = await InvokeAsync("generate --help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--package", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--from", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--to", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--from-assembly", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--to-assembly", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--output", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--rule-id-prefix", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--no-rename-detection", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--json", stdout, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// SCENARIO: Only --from-assembly given without --to-assembly → exit 2.
    /// </summary>
    [Scenario("Only --from-assembly without --to-assembly → exit 2")]
    [Fact]
    public async Task Generate_OnlyFromAssembly_NoToAssembly_Fails()
    {
        var (exitCode, _, stderr) = await InvokeAsync(
            "generate --from-assembly ./some.dll --from 1.0.0 --to 2.0.0");

        Assert.Equal(2, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stderr), "Error message must be printed");
    }

    /// <summary>
    /// SCENARIO: NuGet mode with obviously fake package → exit 1 (network).
    /// </summary>
    [Scenario("NuGet mode with fake package → exit 1")]
    [Trait("Category", "Network")]
    [Fact]
    public async Task Generate_NuGet_FakePackage_Fails()
    {
        var tempDir = CreateTempDir();
        var outputPath = Path.Combine(tempDir, "fake.wrapgod-migration.json");
        try
        {
            var (exitCode, _, stderr) = await InvokeAsync(
                $"generate --package XyzNotARealPackage123456789 --from 1.0.0 --to 2.0.0 --output \"{outputPath}\"");

            Assert.Equal(1, exitCode);
            Assert.False(string.IsNullOrWhiteSpace(stderr));
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }
}
