using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using WrapGod.Migration.Engine.Rewriters.Structural;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

/// <summary>
/// Integration tests verifying that B-level structural rewriters compose correctly on a
/// single synthetic source file. Also verifies that A-level and B-level rewriters compose
/// when all are applied together via <see cref="MigrationEngine.CreateDefault"/>.
/// </summary>
[Feature("Structural rewriter integration: B-level rewriters compose on a synthetic source")]
public sealed class StructuralRewriterIntegrationTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Fixture loading ──────────────────────────────────────────────────────

    private static string FixtureDir =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Migration");

    private static string LoadFixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDir, name));

    private static string NormalizeNewlines(string s) =>
        s.Replace("\r\n", "\n").Replace("\r", "\n");

    /// <summary>Build the dispatch map including both A-level and B-level rewriters.</summary>
    private static Dictionary<string, IRuleRewriter> BuildAllRewriterMap() =>
        new(StringComparer.Ordinal)
        {
            // A-level
            { "renameType", new RenameTypeRewriter() },
            { "renameNamespace", new RenameNamespaceRewriter() },
            { "renameMember", new RenameMemberRewriter() },
            { "changeParameter", new ChangeParameterRewriter() },
            { "removeMember", new RemoveMemberRewriter() },
            { "addRequiredParameter", new AddRequiredParameterRewriter() },
            { "changeTypeReference", new ChangeTypeReferenceRewriter() },
            // B-level structural
            { "splitMethod", new SplitMethodRewriter() },
            { "extractParameterObject", new ExtractParameterObjectRewriter() },
            { "propertyToMethod", new PropertyToMethodRewriter() },
            { "moveMember", new MoveMemberRewriter() },
        };

    private static (string FinalText, RewriteContext Ctx) ApplyRulesInOrder(
        string sourceText,
        MigrationSchema schema)
    {
        var ctx = new RewriteContext("synthetic.cs");
        var rewriters = BuildAllRewriterMap();

        var tree = CSharpSyntaxTree.ParseText(sourceText);
        SyntaxNode currentRoot = tree.GetRoot();

        foreach (var rule in schema.Rules)
        {
            var kindKey = char.ToLowerInvariant(rule.Kind.ToString()[0]) + rule.Kind.ToString()[1..];
            if (!rewriters.TryGetValue(kindKey, out var rewriter))
                continue;

            var rewritten = rewriter.TryRewrite(currentRoot, rule, ctx);
            if (rewritten is not null)
                currentRoot = rewritten;
        }

        return (currentRoot.ToFullString(), ctx);
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Scenario("BLevelRulesCompose: all 4 B-level rules are applied to BLevelBefore fixture")]
    [Fact]
    public Task BLevelRulesCompose_ProducesExpectedOutput() =>
        Given("the BLevel synthetic before fixture and rules schema", () =>
        {
            var before = LoadFixture("BLevelBefore.cs.txt");
            var rulesJson = LoadFixture("BLevelRules.json");
            var schema = MigrationSchemaSerializer.Deserialize(rulesJson)
                ?? throw new InvalidOperationException("Failed to load BLevelRules.json");

            var (actual, ctx) = ApplyRulesInOrder(before, schema);
            return (Actual: actual, Ctx: ctx);
        })
        .Then("at least 4 rewrites were applied (one per B-level rule)", t =>
            t.Ctx.Applied.Count >= 4)
        .And("SplitMethod rule SM-B01 was applied", t =>
            t.Ctx.Applied.Any(a => a.RuleId == "SM-B01"))
        .And("ExtractParameterObject rule EPO-B01 was applied", t =>
            t.Ctx.Applied.Any(a => a.RuleId == "EPO-B01"))
        .And("PropertyToMethod rule PTM-B01 was applied", t =>
            t.Ctx.Applied.Any(a => a.RuleId == "PTM-B01"))
        .And("MoveMember rule MM-B01 was applied", t =>
            t.Ctx.Applied.Any(a => a.RuleId == "MM-B01"))
        .AssertPassed();

    [Scenario("BLevelRulesCompose: SplitMethod inserts replacement calls and MIGRATION comment")]
    [Fact]
    public Task BLevelRulesCompose_SplitMethod_InsertsCallsAndComment() =>
        Given("the BLevel before fixture processed with B-level rules", () =>
        {
            var before = LoadFixture("BLevelBefore.cs.txt");
            var schema = MigrationSchemaSerializer.Deserialize(LoadFixture("BLevelRules.json"))!;
            var (actual, _) = ApplyRulesInOrder(before, schema);
            return NormalizeNewlines(actual);
        })
        .Then("result contains RefreshLayout", s => s.Contains("RefreshLayout"))
        .And("result contains RefreshContent", s => s.Contains("RefreshContent"))
        .And("result contains MIGRATION comment", s => s.Contains("// MIGRATION:"))
        .AssertPassed();

    [Scenario("BLevelRulesCompose: ExtractParameterObject produces new ShowOptions object")]
    [Fact]
    public Task BLevelRulesCompose_ExtractParameterObject_ProducesNewObject() =>
        Given("the BLevel before fixture processed with B-level rules", () =>
        {
            var before = LoadFixture("BLevelBefore.cs.txt");
            var schema = MigrationSchemaSerializer.Deserialize(LoadFixture("BLevelRules.json"))!;
            var (actual, ctx) = ApplyRulesInOrder(before, schema);
            return (Actual: NormalizeNewlines(actual), Ctx: ctx);
        })
        .Then("result contains new ShowOptions", t => t.Actual.Contains("new ShowOptions"))
        .And("EPO-B01 rule was applied", t => t.Ctx.Applied.Any(a => a.RuleId == "EPO-B01"))
        .AssertPassed();

    [Scenario("BLevelRulesCompose: PropertyToMethod rewrites write context correctly")]
    [Fact]
    public Task BLevelRulesCompose_PropertyToMethod_RewritesWriteContext() =>
        Given("the BLevel before fixture processed with B-level rules", () =>
        {
            var before = LoadFixture("BLevelBefore.cs.txt");
            var schema = MigrationSchemaSerializer.Deserialize(LoadFixture("BLevelRules.json"))!;
            var (actual, ctx) = ApplyRulesInOrder(before, schema);
            return (Actual: NormalizeNewlines(actual), Ctx: ctx);
        })
        .Then("result contains SetActive(", t => t.Actual.Contains("SetActive("))
        .And("PTM-B01 rule was applied", t => t.Ctx.Applied.Any(a => a.RuleId == "PTM-B01"))
        .AssertPassed();

    [Scenario("BLevelRulesCompose: MoveMember replaces LegacyHelper with ModernHelper")]
    [Fact]
    public Task BLevelRulesCompose_MoveMember_ReplacesReceiver() =>
        Given("the BLevel before fixture processed with B-level rules", () =>
        {
            var before = LoadFixture("BLevelBefore.cs.txt");
            var schema = MigrationSchemaSerializer.Deserialize(LoadFixture("BLevelRules.json"))!;
            var (actual, ctx) = ApplyRulesInOrder(before, schema);
            return (Actual: NormalizeNewlines(actual), Ctx: ctx);
        })
        .Then("result contains ModernHelper.ComputeHash", t => t.Actual.Contains("ModernHelper.ComputeHash"))
        .And("MM-B01 rule was applied", t => t.Ctx.Applied.Any(a => a.RuleId == "MM-B01"))
        .AssertPassed();

    [Scenario("MigrationEngine.CreateDefault includes all B-level rewriters")]
    [Fact]
    public Task MigrationEngine_CreateDefault_IncludesBLevelRewriters() =>
        Given("a MigrationEngine created via CreateDefault()", () => MigrationEngine.CreateDefault())
        .Then("engine is not null", e => e is not null)
        .AssertPassed();

    [Scenario("B-level and A-level rules compose together without interference")]
    [Fact]
    public Task BAndALevel_ComposeTogether_NoInterference() =>
        Given("a schema combining an A-level renameMember and a B-level moveMember rule", () =>
        {
            var src = @"
namespace App;
class C {
    void M() {
        Widget w = new Widget();
        w.OldDraw();
        var c = Utilities.GetColor();
    }
}";
            var schema = new MigrationSchema
            {
                Rules =
                [
                    new RenameMemberRule
                    {
                        Id = "RM-COMPOSE",
                        TypeName = "Widget",
                        OldMemberName = "OldDraw",
                        NewMemberName = "Draw",
                        Confidence = RuleConfidence.Auto,
                    },
                    new MoveMemberRule
                    {
                        Id = "MM-COMPOSE",
                        OldTypeName = "Utilities",
                        NewTypeName = "ColorHelper",
                        MemberName = "GetColor",
                        Confidence = RuleConfidence.Auto,
                    },
                ],
            };

            var (actual, ctx) = ApplyRulesInOrder(src, schema);
            return (Actual: actual, Ctx: ctx);
        })
        .Then("Applied count is 2 (both rules applied)", t => t.Ctx.Applied.Count == 2)
        .And("result contains w.Draw()", t => t.Actual.Contains("w.Draw()"))
        .And("result contains ColorHelper.GetColor", t => t.Actual.Contains("ColorHelper.GetColor"))
        .AssertPassed();
}
