using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

/// <summary>
/// Integration tests verifying that multiple A-level rewriters applied in schema order to
/// a single synthetic source file reproduce a hand-authored expected output byte-for-byte
/// (line endings normalized to <c>\n</c>). This validates rule composition and
/// trivia preservation across rewriter boundaries — the contract the #196 orchestrator
/// will rely on.
/// </summary>
[Feature("Rewriter integration: multiple rewriters compose on a synthetic source")]
public sealed class RewriterIntegrationTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Fixture loading ──────────────────────────────────────────────────────

    private static string FixtureDir =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Migration");

    private static string LoadFixture(string name) =>
        File.ReadAllText(Path.Combine(FixtureDir, name));

    /// <summary>Normalize line endings to <c>\n</c> so byte-for-byte comparison is
    /// platform-independent (Windows checkout uses CRLF, Linux uses LF).</summary>
    private static string NormalizeNewlines(string s) =>
        s.Replace("\r\n", "\n").Replace("\r", "\n");

    /// <summary>Build the dispatch map from rewriter Kind to rewriter instance.</summary>
    private static Dictionary<string, IRuleRewriter> BuildRewriterMap() =>
        new(StringComparer.Ordinal)
        {
            { "renameType", new RenameTypeRewriter() },
            { "renameNamespace", new RenameNamespaceRewriter() },
            { "renameMember", new RenameMemberRewriter() },
            { "changeParameter", new ChangeParameterRewriter() },
            { "removeMember", new RemoveMemberRewriter() },
            { "addRequiredParameter", new AddRequiredParameterRewriter() },
            { "changeTypeReference", new ChangeTypeReferenceRewriter() },
        };

    /// <summary>
    /// Applies the given schema's rules to the source text in schema order, returning the
    /// final source text after all rewrites. Each rule walks the tree once; rewriters that
    /// return null leave the tree unchanged.
    /// </summary>
    private static (string FinalText, RewriteContext Ctx) ApplyRulesInOrder(
        string sourceText,
        MigrationSchema schema)
    {
        var ctx = new RewriteContext("synthetic.cs");
        var rewriters = BuildRewriterMap();

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

    // ── happy ────────────────────────────────────────────────────────────────

    [Scenario("MultipleRulesOnOneFile_AllRewritersApply matches hand-authored fixture byte-for-byte")]
    [Fact]
    public Task MultipleRulesOnOneFile_AllRewritersApply() =>
        Given("the synthetic before/after fixtures and the rules schema", () =>
        {
            var before = LoadFixture("SyntheticBefore.cs.txt");
            var expectedAfter = LoadFixture("SyntheticAfter.cs.txt");
            var rulesJson = LoadFixture("SyntheticRules.json");
            var schema = MigrationSchemaSerializer.Deserialize(rulesJson)
                ?? throw new InvalidOperationException("Failed to load SyntheticRules.json");

            var (actual, ctx) = ApplyRulesInOrder(before, schema);
            return (Actual: actual, Expected: expectedAfter, Ctx: ctx);
        })
        .Then("the normalized rewritten text matches the expected fixture", t =>
            NormalizeNewlines(t.Actual) == NormalizeNewlines(t.Expected))
        .And("at least 3 rewrites were applied (one per rule)", t => t.Ctx.Applied.Count >= 3)
        .AssertPassed();

    [Scenario("Integration: namespace rewrite is recorded in the audit trail")]
    [Fact]
    public Task Integration_NamespaceRewrite_Applied() =>
        Given("the synthetic schema applied to the before fixture", () =>
        {
            var before = LoadFixture("SyntheticBefore.cs.txt");
            var rulesJson = LoadFixture("SyntheticRules.json");
            var schema = MigrationSchemaSerializer.Deserialize(rulesJson)
                ?? throw new InvalidOperationException("Failed to load SyntheticRules.json");

            var (_, ctx) = ApplyRulesInOrder(before, schema);
            return ctx;
        })
        .Then("Applied contains a RNS-001 entry", ctx =>
            ctx.Applied.Any(a => a.RuleId == "RNS-001"))
        .And("Applied contains a RM-001 entry", ctx =>
            ctx.Applied.Any(a => a.RuleId == "RM-001"))
        .And("Applied contains a CTR-001 entry", ctx =>
            ctx.Applied.Any(a => a.RuleId == "CTR-001"))
        .AssertPassed();
}
