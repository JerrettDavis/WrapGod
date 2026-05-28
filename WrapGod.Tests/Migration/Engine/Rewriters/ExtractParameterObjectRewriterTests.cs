using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters.Structural;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("ExtractParameterObjectRewriter: collapse extracted named parameters into a new options object")]
public sealed class ExtractParameterObjectRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ExtractParameterObjectRewriter Make() => new();

    private static ExtractParameterObjectRule DialogRule() =>
        new()
        {
            Id = "EPO-001",
            TypeName = "Dialog",
            MethodName = "ShowAsync",
            ParameterObjectType = "DialogParameters",
            ExtractedParameters = ["title", "content"],
        };

    private static (Microsoft.CodeAnalysis.SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var ctx = new RewriteContext("test.cs");
        return (tree.GetRoot(), ctx);
    }

    // ── happy ─────────────────────────────────────────────────────────────────

    [Scenario("Named args title/content are folded into new DialogParameters object")]
    [Fact]
    public Task ExtractParamObj_NamedArgs_FoldedIntoObject() =>
        Given("source with 'dlg.ShowAsync(title: \"T\", content: c)' and DialogRule", () =>
        {
            var src = @"class C { void M() { Dialog dlg = new Dialog(); dlg.ShowAsync(title: ""T"", content: c); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DialogRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains new DialogParameters", t => t.Result!.ToString().Contains("new DialogParameters"))
        .And("result contains Title =", t => t.Result!.ToString().Contains("Title ="))
        .And("result contains Content =", t => t.Result!.ToString().Contains("Content ="))
        .And("at least one Applied entry", t => t.Ctx.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("Multiple call sites in one file are all rewritten")]
    [Fact]
    public Task ExtractParamObj_MultipleCallSites_AllRewritten() =>
        Given("source with two ShowAsync calls with named args", () =>
        {
            var src = @"class C { void M() { Dialog dlg = new Dialog(); dlg.ShowAsync(title: ""A"", content: c1); dlg.ShowAsync(title: ""B"", content: c2); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DialogRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count is 2", t => t.Ctx.Applied.Count == 2)
        .And("result contains two DialogParameters usages", t =>
        {
            var text = t.Result!.ToString();
            int count = 0, idx = 0;
            while ((idx = text.IndexOf("DialogParameters", idx, StringComparison.Ordinal)) >= 0) { count++; idx++; }
            return count >= 2;
        })
        .AssertPassed();

    [Scenario("Extra (non-extracted) parameters are preserved alongside the new object")]
    [Fact]
    public Task ExtractParamObj_ExtraParams_PreservedAlongside() =>
        Given("source with 'dlg.ShowAsync(title: T, content: c, callback: cb)' where callback is not extracted", () =>
        {
            var src = @"class C { void M() { Dialog dlg = new Dialog(); dlg.ShowAsync(title: ""T"", content: c, callback: cb); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DialogRule(), ctx);
            return result!.ToString();
        })
        .Then("result contains callback: cb (preserved)", s => s.Contains("callback: cb") || s.Contains("callback"))
        .And("result contains new DialogParameters", s => s.Contains("new DialogParameters"))
        .AssertPassed();

    // ── sad ──────────────────────────────────────────────────────────────────

    [Scenario("All-positional args with count mismatch produces SkippedRewrite")]
    [Fact]
    public Task ExtractParamObj_PositionalArgsMismatch_ProducesSkipped() =>
        Given("source with positional 'dlg.ShowAsync(\"T\", c, extra)' where count != extracted params count", () =>
        {
            var src = @"class C { void M() { Dialog dlg = new Dialog(); dlg.ShowAsync(""T"", c, extra); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DialogRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null (no successful rewrite)", t => t.Result is null)
        .And("Skipped contains positional argument reason", t =>
            t.Ctx.Skipped.Any(s => s.Reason.Contains("positional")))
        .AssertPassed();

    [Scenario("Wrong rule kind returns null")]
    [Fact]
    public Task ExtractParamObj_WrongRuleKind_ReturnsNull() =>
        Given("a RenameMemberRule passed to ExtractParameterObjectRewriter", () =>
        {
            var (root, ctx) = Parse("class C { void M() { dlg.ShowAsync(title: \"T\"); } }");
            var wrongRule = new RenameMemberRule { Id = "X-001", TypeName = "Dialog", OldMemberName = "ShowAsync", NewMemberName = "ShowNewAsync" };
            var result = Make().TryRewrite(root, wrongRule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("No matching call site returns null")]
    [Fact]
    public Task ExtractParamObj_NoMatch_ReturnsNull() =>
        Given("source with no ShowAsync call", () =>
        {
            var (root, ctx) = Parse("class C { void M() { dlg.OtherMethod(); } }");
            var result = Make().TryRewrite(root, DialogRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    // ── edge ─────────────────────────────────────────────────────────────────

    [Scenario("All-positional args with exact matching count are mapped positionally")]
    [Fact]
    public Task ExtractParamObj_PositionalArgsExactMatch_MappedPositionally() =>
        Given("source with positional 'dlg.ShowAsync(\"T\", c)' matching exactly extracted params count", () =>
        {
            var src = @"class C { void M() { Dialog dlg = new Dialog(); dlg.ShowAsync(""T"", c); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DialogRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null (successfully mapped positionally)", t => t.Result is not null)
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .And("result contains DialogParameters", t => t.Result!.ToString().Contains("DialogParameters"))
        .AssertPassed();

    [Scenario("Kind property returns extractParameterObject")]
    [Fact]
    public Task ExtractParameterObjectRewriter_Kind_IsExtractParameterObject() =>
        Given("an ExtractParameterObjectRewriter instance", () => Make())
        .Then("Kind is extractParameterObject", r => r.Kind == "extractParameterObject")
        .AssertPassed();

    [Scenario("Receiver inferred as different type skips rewrite silently")]
    [Fact]
    public Task ExtractParamObj_DifferentReceiverType_NoRewrite() =>
        Given("source where receiver is 'OtherDialog' not 'Dialog'", () =>
        {
            var src = @"class C { void M() { OtherDialog dlg = new OtherDialog(); dlg.ShowAsync(title: ""T"", content: c); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DialogRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null (no match — different type)", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();
}
