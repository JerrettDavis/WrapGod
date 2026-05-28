using System.Text.RegularExpressions;
using WrapGod.Migration;

namespace WrapGod.Tests;

/// <summary>
/// Static-text coverage tests for the migration documentation suite.
/// Verifies that every <see cref="MigrationRuleKind"/> value is mentioned in
/// the relevant documentation pages and that the CLI reference lists all four
/// migrate subcommands.
///
/// These tests are intentionally mechanical — they catch documentation drift
/// when new rule kinds or commands are added to the engine.
/// </summary>
public sealed class MigrationDocsCoverageTests
{
    // Locate the repo root relative to the test DLL output directory.
    // The DLL lives at WrapGod.Tests/bin/{config}/{tfm}/
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WrapGod.slnx")))
            dir = dir.Parent;

        if (dir == null)
            throw new InvalidOperationException(
                "Could not locate WrapGod.slnx from test output directory. " +
                "Ensure the test is run from within the WrapGod repository.");

        return dir.FullName;
    }

    private static string MigrationDocPath(string fileName) =>
        Path.Combine(RepoRoot, "docs", "migration", fileName);

    private static string GuideCLIDocPath() =>
        Path.Combine(RepoRoot, "docs", "guide", "cli.md");

    private static string ReadmeDocPath() =>
        Path.Combine(RepoRoot, "README.md");

    // ── authoring.md ───────────────────────────────────────────────────────

    [Fact]
    public void AuthoringDoc_MentionsEveryRuleKind()
    {
        var doc = File.ReadAllText(MigrationDocPath("authoring.md"));

        foreach (var kind in Enum.GetValues<MigrationRuleKind>())
        {
            // Each kind should appear as its camelCase JSON discriminator value
            // e.g., RenameType → "renameType"
            var camelCase = ToCamelCase(kind.ToString());
            Assert.Contains(camelCase, doc, StringComparison.Ordinal);
        }
    }

    // ── schema.md ──────────────────────────────────────────────────────────

    [Fact]
    public void SchemaDoc_HasJsonExamplePerRuleKind()
    {
        var doc = File.ReadAllText(MigrationDocPath("schema.md"));

        foreach (var kind in Enum.GetValues<MigrationRuleKind>())
        {
            var camelCase = ToCamelCase(kind.ToString());
            // Each kind's JSON example should contain: "kind": "camelCaseValue"
            var pattern = $@"""kind""\s*:\s*""{Regex.Escape(camelCase)}""";
            Assert.Matches(pattern, doc);
        }
    }

    // ── cli.md ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("migrate generate")]
    [InlineData("migrate apply")]
    [InlineData("migrate status")]
    [InlineData("migrate verify")]
    public void CliDoc_ListsAllMigrateSubcommands(string subcommand)
    {
        var doc = File.ReadAllText(GuideCLIDocPath());
        Assert.Contains(subcommand, doc, StringComparison.OrdinalIgnoreCase);
    }

    // ── README.md ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("migrate generate")]
    [InlineData("migrate apply")]
    [InlineData("migrate status")]
    [InlineData("migrate verify")]
    public void ReadmeCliTable_ListsMigrateCommands(string command)
    {
        var doc = File.ReadAllText(ReadmeDocPath());
        Assert.Contains(command, doc, StringComparison.OrdinalIgnoreCase);
    }

    // ── index.md ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("authoring.md")]
    [InlineData("applying.md")]
    [InlineData("schema.md")]
    [InlineData("engine.md")]
    [InlineData("rewriters.md")]
    [InlineData("state.md")]
    [InlineData("verifying.md")]
    public void IndexDoc_LinksToSubpages(string page)
    {
        var doc = File.ReadAllText(MigrationDocPath("index.md"));
        Assert.Contains(page, doc, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Converts a PascalCase enum name to camelCase, matching the JSON
    /// discriminator convention used in migration schemas.
    /// E.g., "RenameType" → "renameType".
    /// </summary>
    private static string ToCamelCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        return char.ToLowerInvariant(pascalCase[0]) + pascalCase[1..];
    }
}
