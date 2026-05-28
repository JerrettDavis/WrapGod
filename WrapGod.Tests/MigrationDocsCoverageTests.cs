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

    // ── JSON shape drift test ──────────────────────────────────────────────

    /// <summary>
    /// For every <see cref="MigrationRuleKind"/>, reflects on the corresponding
    /// <c>{Kind}Rule</c> record/class in <c>WrapGod.Migration</c> and verifies
    /// that each public property name appears (in camelCase) somewhere in
    /// <c>authoring.md</c>. Catches schema-model drift where a property is
    /// renamed in the C# model but the documentation still references the old
    /// name.
    /// </summary>
    [Fact]
    public void AuthoringDoc_HasMatchingJsonFieldsForEveryRuleKind()
    {
        var doc = File.ReadAllText(MigrationDocPath("authoring.md"));
        var migrationAsm = typeof(MigrationRule).Assembly;

        // Properties present on the base MigrationRule type (Id, Kind, Confidence, Note)
        // are documented in the "Rule object skeleton" section and need not appear in
        // every per-kind section.
        var baseProps = typeof(MigrationRule)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = new List<string>();

        foreach (var kindName in Enum.GetNames<MigrationRuleKind>())
        {
            var ruleType = migrationAsm.GetType($"WrapGod.Migration.{kindName}Rule");
            if (ruleType is null)
                continue; // Skip if the per-kind class isn't named {Kind}Rule.

            foreach (var prop in ruleType.GetProperties())
            {
                if (baseProps.Contains(prop.Name))
                    continue;

                var camelCase = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
                if (!doc.Contains(camelCase, StringComparison.Ordinal))
                {
                    missing.Add($"{kindName}Rule.{prop.Name} → '{camelCase}'");
                }
            }
        }

        Assert.True(
            missing.Count == 0,
            "authoring.md is missing documentation for the following rule property fields " +
            "(model has them but docs do not mention them in camelCase):\n  " +
            string.Join("\n  ", missing));
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
