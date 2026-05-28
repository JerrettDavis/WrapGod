using System.CommandLine;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Cli;
using Xunit.Abstractions;

namespace WrapGod.Tests;

/// <summary>
/// End-to-end parity tests for the Serilog v2-to-v3 migration example (issue #203).
///
/// These tests are the in-suite guard for the example at
/// <c>examples/migrations/serilog-v2-to-v3/</c>.  They complement the standalone
/// <c>MigrationTests</c> project inside the example directory; having both keeps the
/// main test suite aware of example health without requiring a separate test run.
///
/// Tests match the BDD scenarios specified in MIGRATION-ENGINE-PLAN.md §203.b:
///   Happy-01: apply + diff == after/  (byte-equal, LF-normalised)
///   Happy-02: summary shows 100% (1 applied + 1 manual)
/// </summary>
[Feature("E2E: Serilog v2-to-v3 migration example (#203) parity")]
[Collection("CLI")]
public sealed class SerilogMigrationE2ETests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Paths ─────────────────────────────────────────────────────────────────
    //
    // The test assembly lives at WrapGod.Tests/bin/…  We walk up to the repo
    // root by looking for the WrapGod.slnx marker file, then navigate from there.

    private static readonly string RepoRoot = FindRepoRoot(AppContext.BaseDirectory);

    private static readonly string ExampleRoot =
        Path.Combine(RepoRoot, "examples", "migrations", "serilog-v2-to-v3");

    private static readonly string BeforeDir  = Path.Combine(ExampleRoot, "before");
    private static readonly string AfterDir   = Path.Combine(ExampleRoot, "after");

    private static readonly string SchemaFile =
        Path.Combine(ExampleRoot, "schema",
            "serilog.2.x-to-3.x.wrapgod-migration.json");

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FindRepoRoot(string start)
    {
        var dir = start;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "WrapGod.slnx")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException(
            $"Cannot locate WrapGod.slnx starting from {start}");
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wrapgod-serilog-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDelete(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { /* best-effort */ }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        }
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeMigrateApplyAsync(
        string schema, string projectDir)
    {
        var command = MigrateCommandBuilder.Build();
        var previousOut  = Console.Out;
        var previousErr  = Console.Error;
        var previousCode = Environment.ExitCode;
        await using var stdOut = new StringWriter();
        await using var stdErr = new StringWriter();

        try
        {
            Console.SetOut(stdOut);
            Console.SetError(stdErr);
            Environment.ExitCode = 0;

            var invokeCode = await command.InvokeAsync(
                $"apply --schema \"{schema}\" --project-dir \"{projectDir}\"");

            var effective = Environment.ExitCode == 0 ? invokeCode : Environment.ExitCode;
            return (effective, stdOut.ToString(), stdErr.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousErr);
            Environment.ExitCode = previousCode;
        }
    }

    private static string NormaliseLf(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Happy-01: apply + diff == after/ (byte-equal, LF-normalised).
    /// Satisfies BDD scenario 1 from MIGRATION-ENGINE-PLAN.md §203.b.
    /// </summary>
    [Scenario("Happy-01: Apply_BeforeMatchesAfter_AfterMigration")]
    [Fact]
    public async Task Apply_BeforeMatchesAfter_AfterMigration()
    {
        Assert.True(Directory.Exists(BeforeDir),  $"before/ fixture missing: {BeforeDir}");
        Assert.True(Directory.Exists(AfterDir),   $"after/ fixture missing: {AfterDir}");
        Assert.True(File.Exists(SchemaFile),       $"Schema missing: {SchemaFile}");

        var tempDir = CreateTempDir();
        try
        {
            CopyDirectory(BeforeDir, tempDir);

            // Copy the schema to a temp location too — otherwise the engine writes a
            // state file next to the real committed schema, mutating the fixture.
            var tempSchema = Path.Combine(tempDir, Path.GetFileName(SchemaFile));
            File.Copy(SchemaFile, tempSchema, overwrite: true);

            var (exitCode, stdOut, stdErr) = await InvokeMigrateApplyAsync(tempSchema, tempDir);

            Assert.True(exitCode == 0,
                $"migrate apply failed (exit {exitCode}).\nstdout:\n{stdOut}\nstderr:\n{stdErr}");

            var afterFiles = EnumerateFixtureFiles(AfterDir);

            var diffs = new List<string>();
            foreach (var rel in afterFiles)
            {
                var migratedPath = Path.Combine(tempDir, rel);
                var expectedPath = Path.Combine(AfterDir, rel);

                if (!File.Exists(migratedPath))
                {
                    diffs.Add($"MISSING: {rel}");
                    continue;
                }

                var migrated = NormaliseLf(await File.ReadAllTextAsync(migratedPath));
                var expected = NormaliseLf(await File.ReadAllTextAsync(expectedPath));

                if (!string.Equals(migrated, expected, StringComparison.Ordinal))
                    diffs.Add($"DIFF in {rel}");
            }

            Assert.True(diffs.Count == 0,
                $"Parity failure — {diffs.Count} file(s) differ: {string.Join(", ", diffs)}\n" +
                $"Engine stdout:\n{stdOut}");
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Enumerates committed fixture .cs files under <paramref name="root"/>, excluding
    /// build outputs (obj/, bin/) that may exist locally when the example projects have
    /// been built. Returns paths relative to <paramref name="root"/>.
    /// </summary>
    private static List<string> EnumerateFixtureFiles(string root) =>
        Directory
            .GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
                !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(root, f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Happy-02: summary reflects 2 applied (renameNamespace hits both .cs files) +
    /// 2 manual (renameMember + removeMember).
    /// Satisfies BDD scenario 2 from MIGRATION-ENGINE-PLAN.md §203.b.
    /// </summary>
    [Scenario("Happy-02: Status_ShowsExpectedSummary_AfterApply")]
    [Fact]
    public async Task Status_ShowsExpectedSummary_AfterApply()
    {
        Assert.True(File.Exists(SchemaFile), $"Schema missing: {SchemaFile}");

        var tempDir = CreateTempDir();
        try
        {
            CopyDirectory(BeforeDir, tempDir);

            // Copy the schema to temp too so the state file write doesn't touch the
            // committed fixture next to the real schema.
            var tempSchema = Path.Combine(tempDir, Path.GetFileName(SchemaFile));
            File.Copy(SchemaFile, tempSchema, overwrite: true);

            var (exitCode, stdOut, _) = await InvokeMigrateApplyAsync(tempSchema, tempDir);

            Assert.Equal(0, exitCode);
            // renameNamespace fires on both .cs files → 2 rewrites
            Assert.Contains("2 rewrites", stdOut, StringComparison.OrdinalIgnoreCase);
            // 2 manual-confidence rules: renameMember (SERILOG-RM-001) + removeMember (SERILOG-RX-001)
            Assert.Contains("2 rules require human intervention", stdOut,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }
}
