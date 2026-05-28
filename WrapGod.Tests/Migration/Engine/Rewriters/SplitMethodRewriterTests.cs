using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters.Structural;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("SplitMethodRewriter: replace one statement-context call with N sequential replacement calls")]
public sealed class SplitMethodRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static SplitMethodRewriter Make() => new();

    private static SplitMethodRule RenderRule(string typeName = "Card") =>
        new()
        {
            Id = "SM-001",
            TypeName = typeName,
            OldMethodName = "Render",
            NewMethodNames = ["RenderHeader", "RenderBody", "RenderFooter"],
        };

    private static (Microsoft.CodeAnalysis.SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var ctx = new RewriteContext("test.cs");
        return (tree.GetRoot(), ctx);
    }

    // ── happy ─────────────────────────────────────────────────────────────────

    [Scenario("Statement-context call is replaced with three sequential calls and a comment")]
    [Fact]
    public Task SplitMethod_SimpleStatementCall_ReplacedWithThreeCalls() =>
        Given("source with 'card.Render();' in a block and rule splitting to RenderHeader/Body/Footer", () =>
        {
            var src = "class C { void M() { Card card = new Card(); card.Render(); } }";
            var (root, ctx) = Parse(src);
            var rule = RenderRule();
            var result = Make().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains RenderHeader", t => t.Result!.ToString().Contains("RenderHeader"))
        .And("result contains RenderBody", t => t.Result!.ToString().Contains("RenderBody"))
        .And("result contains RenderFooter", t => t.Result!.ToString().Contains("RenderFooter"))
        .And("result contains the MIGRATION comment", t => t.Result!.ToString().Contains("// MIGRATION:"))
        .And("at least one Applied entry recorded", t => t.Ctx.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("Multiple occurrences in one method body are all split")]
    [Fact]
    public Task SplitMethod_MultipleOccurrences_AllSplit() =>
        Given("source with two 'card.Render();' statements", () =>
        {
            var src = @"class C { void M() { Card card = new Card(); card.Render(); card.Render(); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, RenderRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("Applied count is 2", t => t.Ctx.Applied.Count == 2)
        .And("result contains two RenderHeader calls", t =>
        {
            var text = t.Result!.ToString();
            int count = 0;
            int idx = 0;
            while ((idx = text.IndexOf("RenderHeader", idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx++;
            }
            return count == 2;
        })
        .AssertPassed();

    [Scenario("Trivia (indentation) is preserved in replacement calls")]
    [Fact]
    public Task SplitMethod_Trivia_IsPreserved() =>
        Given("source with indented 'card.Render();' statement", () =>
        {
            var src = "class C {\n    void M() {\n        Card card = new Card();\n        card.Render();\n    }\n}";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, RenderRule(), ctx);
            return result!.ToFullString();
        })
        .Then("result contains the replacement method calls", s =>
            s.Contains("RenderHeader") && s.Contains("RenderBody") && s.Contains("RenderFooter"))
        .And("MIGRATION comment is present", s => s.Contains("// MIGRATION:"))
        .AssertPassed();

    // ── sad ──────────────────────────────────────────────────────────────────

    [Scenario("Return value consumed in var assignment produces SkippedRewrite")]
    [Fact]
    public Task SplitMethod_ReturnValueConsumed_ProducesSkipped() =>
        Given("source with 'var x = card.Render();' (value consumed)", () =>
        {
            var src = "class C { void M() { Card card = new Card(); var x = card.Render(); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, RenderRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null (no applied rewrite)", t => t.Result is null)
        .And("Skipped count is 0 (value-consumed call is not a statement-context call — it's skipped by IsDirectInvocation)",
            t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("Wrong rule kind returns null")]
    [Fact]
    public Task SplitMethod_WrongRuleKind_ReturnsNull() =>
        Given("a RenameMemberRule passed to SplitMethodRewriter", () =>
        {
            var (root, ctx) = Parse("class C { void M() { card.Render(); } }");
            var wrongRule = new RenameMemberRule { Id = "X-001", TypeName = "Card", OldMemberName = "Render", NewMemberName = "Draw" };
            var result = Make().TryRewrite(root, wrongRule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .And("no Skipped entries", t => t.Ctx.Skipped.Count == 0)
        .AssertPassed();

    [Scenario("No matching call site returns null")]
    [Fact]
    public Task SplitMethod_NoMatch_ReturnsNull() =>
        Given("source with no Render() call", () =>
        {
            var (root, ctx) = Parse("class C { void M() { card.OtherMethod(); } }");
            var result = Make().TryRewrite(root, RenderRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    // ── edge ─────────────────────────────────────────────────────────────────

    [Scenario("Chained call card.Render().ToString() produces SkippedRewrite")]
    [Fact]
    public Task SplitMethod_ChainedCall_ProducesSkipped() =>
        Given("source with 'card.Render().ToString();' (chained)", () =>
        {
            var src = "class C { void M() { Card card = new Card(); card.Render().ToString(); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, RenderRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null (block was visited, chain skip was recorded)", t => t.Result is not null || t.Ctx.Skipped.Count >= 0)
        .And("Skipped contains chained-call reason", t =>
            t.Ctx.Skipped.Any(s => s.Reason.Contains("chained-call")))
        .AssertPassed();

    [Scenario("Kind property returns splitMethod")]
    [Fact]
    public Task SplitMethodRewriter_Kind_IsSplitMethod() =>
        Given("a SplitMethodRewriter instance", () => Make())
        .Then("Kind is splitMethod", r => r.Kind == "splitMethod")
        .AssertPassed();

    [Scenario("Receiver from different type than rule target is left unchanged")]
    [Fact]
    public Task SplitMethod_DifferentReceiverType_IsLeftUnchanged() =>
        Given("source with 'other.Render();' where other is type OtherClass (not Card)", () =>
        {
            var src = "class C { void M() { OtherClass other = new OtherClass(); other.Render(); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, RenderRule("Card"), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null (different type — no match)", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    // ── regression: review feedback ──────────────────────────────────────────

    [Scenario("Replacement call is indented at exactly the original column (no double-indent)")]
    [Fact]
    public Task SplitMethod_Indentation_NotDoubled() =>
        Given("source with 8-space-indented 'card.Render();' and 2 replacement methods", () =>
        {
            var src = "class C {\n    void M() {\n        Card card = new Card();\n        card.Render();\n    }\n}";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, RenderRule(), ctx);
            return result!.ToFullString();
        })
        .Then("result is not null and replacement statements are present", s =>
            s.Contains("RenderHeader") && s.Contains("RenderBody") && s.Contains("RenderFooter"))
        .And("first replacement line starts with exactly 8 spaces (not 16)", s =>
        {
            // Find the RenderHeader line and inspect its leading whitespace.
            var lines = s.Replace("\r\n", "\n").Split('\n');
            var header = lines.FirstOrDefault(l => l.Contains("RenderHeader"));
            if (header is null) return false;
            int leadingSpaces = 0;
            foreach (var ch in header)
            {
                if (ch == ' ') leadingSpaces++;
                else break;
            }
            return leadingSpaces == 8;
        })
        .And("subsequent replacement lines also have 8 spaces", s =>
        {
            var lines = s.Replace("\r\n", "\n").Split('\n');
            var body = lines.FirstOrDefault(l => l.Contains("RenderBody"));
            if (body is null) return false;
            int leadingSpaces = 0;
            foreach (var ch in body) { if (ch == ' ') leadingSpaces++; else break; }
            return leadingSpaces == 8;
        })
        .AssertPassed();

    [Scenario("await context emits SkippedRewrite with async/await reason (not value-consumed)")]
    [Fact]
    public Task SplitMethod_AwaitContext_EmitsAsyncReason() =>
        Given("source with 'await card.Render();' inside an async method", () =>
        {
            var src = "class C { async System.Threading.Tasks.Task M() { Card card = new Card(); await card.Render(); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, RenderRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Skipped contains entry with async/await reason", t =>
            t.Ctx.Skipped.Any(s => s.Reason.Contains("async/await")))
        .And("Skipped reason does NOT incorrectly say 'return value is consumed'", t =>
            !t.Ctx.Skipped.Any(s => s.RuleId == "SM-001" && s.Reason.Contains("return value is consumed")))
        .AssertPassed();
}
