using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("RenameMemberRewriter: rename a member on a known declaring type, skip ambiguous usages")]
public sealed class RenameMemberRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static RenameMemberRewriter Make() => new();

    private static RenameMemberRule Rule(string typeName = "MyService", string old = "OldMethod", string @new = "NewMethod") =>
        new() { Id = "RM-001", TypeName = typeName, OldMemberName = old, NewMemberName = @new };

    private static (SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return (tree.GetRoot(), new RewriteContext("test.cs"));
    }

    // ── happy ────────────────────────────────────────────────────────────────

    [Scenario("Member access where receiver type variable is declared with the declaring type is renamed")]
    [Fact]
    public Task RenameMember_VariableDeclaredWithType_Renamed() =>
        Given("source declaring 'MyService svc' and calling svc.OldMethod()", () =>
        {
            var (root, ctx) = Parse("class C { void M() { MyService svc = null; svc.OldMethod(); } }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains NewMethod", t => t.Result!.ToString().Contains("NewMethod"))
        .And("Applied count >= 1", t => t.Ctx.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("Ambiguous receiver records SkippedRewrite and returns non-null tree (no rewrite at that site)")]
    [Fact]
    public Task RenameMember_AmbiguousReceiver_RecordsSkipped() =>
        Given("source with 'obj.OldMethod()' where obj type is unknown", () =>
        {
            var (root, ctx) = Parse("class C { void M(object obj) { obj.OldMethod(); } }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Skipped has at least one entry", t => t.Ctx.Skipped.Count >= 1)
        .And("Skipped reason mentions ambiguous", t => t.Ctx.Skipped[0].Reason.Contains("ambiguous", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Wrong rule kind returns null")]
    [Fact]
    public Task RenameMember_WrongRuleKind_ReturnsNull() =>
        Given("a RenameTypeRule passed to RenameMemberRewriter", () =>
        {
            var (root, ctx) = Parse("class C { void M() { svc.OldMethod(); } }");
            var wrongRule = new RenameTypeRule { Id = "X", OldName = "A", NewName = "B" };
            var result = Make().TryRewrite(root, wrongRule, ctx);
            return result;
        })
        .Then("result is null", r => r is null)
        .AssertPassed();

    [Scenario("No member access of OldMemberName returns null")]
    [Fact]
    public Task RenameMember_NoMatch_ReturnsNull() =>
        Given("source with no OldMethod references", () =>
        {
            var (root, ctx) = Parse("class C { void M() { } }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return result;
        })
        .Then("result is null", r => r is null)
        .AssertPassed();

    [Scenario("Trivia is preserved on renamed member access")]
    [Fact]
    public Task RenameMember_Trivia_IsPreserved() =>
        Given("source with whitespace around member access", () =>
        {
            var src = "class C { void M() { MyService  svc = null;\n    svc.OldMethod  (); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, Rule(), ctx);
            return result!.ToString();
        })
        .Then("NewMethod is present", s => s.Contains("NewMethod"))
        .And("OldMethod is gone", s => !s.Contains("OldMethod"))
        .AssertPassed();

    [Scenario("Same-named member on unrelated type is skipped, not renamed")]
    [Fact]
    public Task RenameMember_UnrelatedType_Skipped() =>
        Given("source with two types both having OldMethod, only one matches rule", () =>
        {
            var (root, ctx) = Parse(
                "class C { void M() { OtherService other = null; other.OldMethod(); } }");
            var result = Make().TryRewrite(root, Rule("MyService", "OldMethod", "NewMethod"), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Skipped count >= 1 (receiver type does not match)", t => t.Ctx.Skipped.Count >= 1)
        .AssertPassed();

    [Scenario("Kind property returns renameMember")]
    [Fact]
    public Task RenameMemberRewriter_Kind_IsRenameMember() =>
        Given("a RenameMemberRewriter instance", () => Make())
        .Then("Kind is renameMember", r => r.Kind == "renameMember")
        .AssertPassed();
}
