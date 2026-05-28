using WrapGod.Migration;
using WrapGod.Migration.Engine;
using Xunit;

namespace SerilogV2ToV3.MigrationTests;

/// <summary>
/// Parity test: applies the Serilog v2-to-v3 schema to a temp copy of
/// <c>before/</c> and diffs the result against <c>after/</c>.
///
/// This is the CI guard that prevents the example from silently rotting.
/// If the engine changes how it handles a rule, the diff will fail and
/// the <c>after/</c> fixture must be updated to match the new output.
/// </summary>
public sealed class MigrationParityTests
{
    // ── Paths resolved relative to this assembly's location ──────────────────
    //
    // Walk up from the output directory until we find WrapGod.slnx (repo root),
    // then navigate to the example.

    private static readonly string RepoRoot  = FindRepoRoot(AppContext.BaseDirectory);

    private static readonly string ExampleRoot =
        Path.Combine(RepoRoot, "examples", "migrations", "serilog-v2-to-v3");

    private static readonly string BeforeDir  = Path.Combine(ExampleRoot, "before");
    private static readonly string AfterDir   = Path.Combine(ExampleRoot, "after");
    private static readonly string SchemaFile =
        Path.Combine(ExampleRoot, "schema", "serilog.2.x-to-3.x.wrapgod-migration.json");

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
        var path = Path.Combine(Path.GetTempPath(), $"wrapgod-serilog-parity-{Guid.NewGuid():N}");
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

    /// <summary>Normalises line endings to LF so diffs are OS-agnostic.</summary>
    private static string NormaliseLf(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies the committed schema to a temp copy of <c>before/</c> and asserts
    /// the resulting C# files are byte-equal (after LF normalisation) to <c>after/</c>.
    ///
    /// This is the primary acceptance test for issue #203.
    /// </summary>
    [Fact]
    public async Task Apply_BeforeMatchesAfter_AfterMigration()
    {
        Assert.True(Directory.Exists(BeforeDir),
            $"before/ fixture missing — expected: {BeforeDir}");
        Assert.True(Directory.Exists(AfterDir),
            $"after/ fixture missing — expected: {AfterDir}");
        Assert.True(File.Exists(SchemaFile),
            $"Schema file missing — expected: {SchemaFile}");

        var tempDir = CreateTempDir();
        try
        {
            // Copy before/ to a temp dir so the engine can mutate it
            CopyDirectory(BeforeDir, tempDir);

            // Load schema and collect .cs files
            var schemaJson = await File.ReadAllTextAsync(SchemaFile);
            var schema     = MigrationSchemaSerializer.Deserialize(schemaJson)
                             ?? throw new InvalidOperationException("Schema deserialization returned null");

            var csFiles = Directory
                .GetFiles(tempDir, "*.cs", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Run the engine using the default factory (all A-level + B-level rewriters)
            var engine = MigrationEngine.CreateDefault();
            engine.Apply(schema, csFiles);

            // Diff every committed .cs fixture in after/ against the migrated temp dir.
            // Exclude obj/ and bin/ which may exist locally when after/ has been built.
            var afterFiles = Directory
                .GetFiles(AfterDir, "*.cs", SearchOption.AllDirectories)
                .Where(f =>
                    !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Select(f => Path.GetRelativePath(AfterDir, f))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Assert.NotEmpty(afterFiles);

            var diffs = new List<string>();
            foreach (var relPath in afterFiles)
            {
                var migratedPath = Path.Combine(tempDir, relPath);
                var expectedPath = Path.Combine(AfterDir, relPath);

                if (!File.Exists(migratedPath))
                {
                    diffs.Add($"MISSING: {relPath} was not produced by engine.Apply");
                    continue;
                }

                var migrated = NormaliseLf(await File.ReadAllTextAsync(migratedPath));
                var expected = NormaliseLf(await File.ReadAllTextAsync(expectedPath));

                if (!string.Equals(migrated, expected, StringComparison.Ordinal))
                {
                    diffs.Add(
                        $"DIFF in {relPath}:\n" +
                        $"  expected:\n{Indent(expected)}\n" +
                        $"  actual:\n{Indent(migrated)}");
                }
            }

            Assert.True(diffs.Count == 0,
                $"Parity check failed — {diffs.Count} file(s) differ:\n" +
                string.Join("\n\n", diffs));
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    /// <summary>
    /// Verifies the schema currently exercises three distinct rule kinds and that the
    /// engine reports the expected applied + manual counts.
    /// Auto:   SERILOG-NS-001 (renameNamespace) → applies in both .cs files.
    /// Manual: SERILOG-RM-001 (renameMember) + SERILOG-RX-001 (removeMember).
    /// </summary>
    [Fact]
    public async Task Apply_ReturnsExpected_AppliedAndManualCounts()
    {
        Assert.True(File.Exists(SchemaFile), $"Schema missing: {SchemaFile}");

        var tempDir = CreateTempDir();
        try
        {
            CopyDirectory(BeforeDir, tempDir);

            var schemaJson = await File.ReadAllTextAsync(SchemaFile);
            var schema     = MigrationSchemaSerializer.Deserialize(schemaJson)!;

            var csFiles = Directory
                .GetFiles(tempDir, "*.cs", SearchOption.AllDirectories)
                .ToArray();

            var engine = MigrationEngine.CreateDefault();
            var result = engine.Apply(schema, csFiles);

            // SERILOG-NS-001 (renameNamespace) is auto-applicable → 2 Applied (one per file)
            Assert.True(result.Applied.Count >= 2,
                $"Expected at least 2 applied rewrites; got {result.Applied.Count}");

            // SERILOG-RM-001 + SERILOG-RX-001 are manual-confidence → 2 entries
            Assert.True(result.Manual.Count >= 2,
                $"Expected at least 2 manual rules; got {result.Manual.Count}");

            // DryRun flag should be false (this is a real apply)
            Assert.False(result.DryRun);
        }
        finally
        {
            SafeDelete(tempDir);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string Indent(string text) =>
        string.Join('\n', text.Split('\n').Select(l => "    " + l));
}
