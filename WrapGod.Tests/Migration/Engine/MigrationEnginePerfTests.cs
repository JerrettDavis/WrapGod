using System.Diagnostics;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using WrapGod.Tests.Migration.Engine.Fixtures;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine;

[Feature("MigrationEngine perf: 1000-file project completes within time budget")]
public sealed class MigrationEnginePerfTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Budget ────────────────────────────────────────────────────────────────

    // Allow up to 10 s on a slow CI host; target is 5 s on a dev box.
    private const int BudgetMs = 10_000;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MigrationEngine BuildEngine(IMigrationFileSystem fs) =>
        new(
        [
            new RenameTypeRewriter(),
            new RenameNamespaceRewriter(),
            new RenameMemberRewriter(),
            new ChangeParameterRewriter(),
            new RemoveMemberRewriter(),
            new AddRequiredParameterRewriter(),
            new ChangeTypeReferenceRewriter(),
        ], fs);

    private static MigrationSchema FiveRuleSchema() =>
        new()
        {
            Library = "Perf", From = "1.0", To = "2.0",
            Rules =
            [
                new RenameTypeRule    { Id = "P1", OldName = "Widget",    NewName = "MudWidget" },
                new RenameTypeRule    { Id = "P2", OldName = "Button",    NewName = "MudButton" },
                new RenameMemberRule  { Id = "P3", TypeName = "Widget",   OldMemberName = "Render", NewMemberName = "Draw" },
                new ChangeTypeReferenceRule { Id = "P4", OldType = "IList",   NewType = "IReadOnlyList" },
                new RenameNamespaceRule { Id = "P5", OldNamespace = "Acme.Old", NewNamespace = "Acme.New" },
            ]
        };

    /// <summary>
    /// Generates a small but realistic synthetic C# source that exercises most rules.
    /// Average ~300 bytes, well under the 250 KB average.
    /// </summary>
    private static string GenerateSyntheticSource(int index) =>
        $"using Acme.Old.Core;\n" +
        $"using System.Collections.Generic;\n" +
        $"\n" +
        $"namespace TestApp.Unit{index};\n" +
        $"\n" +
        $"public class Service{index}\n" +
        "{\n" +
        "    private readonly Widget _widget;\n" +
        "    private readonly Button _button;\n" +
        "\n" +
        $"    public Service{index}(Widget widget, Button button)\n" +
        "    {\n" +
        "        _widget = widget;\n" +
        "        _button = button;\n" +
        "    }\n" +
        "\n" +
        "    public IList<string> GetItems()\n" +
        "    {\n" +
        "        _widget.Render();\n" +
        "        _button.Render();\n" +
        "        IList<string> items = new List<string>();\n" +
        "        return items;\n" +
        "    }\n" +
        "}\n";

    // ══════════════════════════════════════════════════════════════════════════
    // Perf test
    // ══════════════════════════════════════════════════════════════════════════

    [Trait("Category", "Perf")]
    [Scenario("Perf-01: 1000-file project with 5 rules completes under budget")]
    [Fact]
    public Task Perf01_1000Files_FiveRules_UnderBudget() =>
        Given("1000 synthetic .cs files loaded into InMemoryFileSystem with 5 rules schema", () =>
        {
            var fs = new InMemoryFileSystem();
            var paths = new string[1000];
            for (int i = 0; i < 1000; i++)
            {
                var path = $"file{i:D4}.cs";
                fs.WithFile(path, GenerateSyntheticSource(i));
                paths[i] = path;
            }

            var engine = BuildEngine(fs);
            var schema = FiveRuleSchema();

            var sw = Stopwatch.StartNew();
            var result = engine.Apply(schema, paths);
            sw.Stop();

            return (result, elapsedMs: sw.ElapsedMilliseconds);
        })
        .Then($"elapsed time is under {BudgetMs} ms", t => t.elapsedMs < BudgetMs)
        .And("at least 3000 Applied entries (multiple rewrites per file)", t => t.result.Applied.Count >= 3000)
        .And("all 1000 files were rewritten", t => t.result.RewrittenFiles.Count == 1000)
        .AssertPassed();
}
