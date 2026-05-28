using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("RemoveMemberRewriter: comment-out call sites for removed members and record Applied")]
public sealed class RemoveMemberRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static RemoveMemberRewriter Make() => new();

    private static RemoveMemberRule Rule(string typeName = "OldApi", string member = "Deprecated") =>
        new() { Id = "DEL-001", TypeName = typeName, MemberName = member, Note = "Use NewApi instead." };

    private static (SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return (tree.GetRoot(), new RewriteContext("test.cs"));
    }

    // ── happy ────────────────────────────────────────────────────────────────

    [Scenario("Call site is commented out and Applied entry recorded")]
    [Fact]
    public Task RemoveMember_CallSite_CommentedOut_AndApplied() =>
        Given("source calling obj.Deprecated() and rule for OldApi.Deprecated", () =>
        {
            var (root, ctx) = Parse("class C { void M(OldApi obj) { obj.Deprecated(); } }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains MIGRATION comment", t => t.Result!.ToString().Contains("MIGRATION"))
        .And("Applied count >= 1", t => t.Ctx.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("MIGRATION comment contains rule id")]
    [Fact]
    public Task RemoveMember_Comment_ContainsRuleId() =>
        Given("source calling obj.Deprecated()", () =>
        {
            var (root, ctx) = Parse("class C { void M(OldApi obj) { obj.Deprecated(); } }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return result!.ToString();
        })
        .Then("comment contains rule id DEL-001", s => s.Contains("DEL-001"))
        .AssertPassed();

    [Scenario("Multiple call sites are all commented out")]
    [Fact]
    public Task RemoveMember_MultipleCallSites_AllCommentedOut() =>
        Given("source with two calls to Deprecated()", () =>
        {
            var (root, ctx) = Parse(
                "class C { void M(OldApi obj) { obj.Deprecated(); obj.Deprecated(); } }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count is 2", t => t.Ctx.Applied.Count == 2)
        .AssertPassed();

    [Scenario("Wrong rule kind returns null")]
    [Fact]
    public Task RemoveMember_WrongRuleKind_ReturnsNull() =>
        Given("a RenameTypeRule passed to RemoveMemberRewriter", () =>
        {
            var (root, ctx) = Parse("class C { void M(OldApi obj) { obj.Deprecated(); } }");
            var wrongRule = new RenameTypeRule { Id = "X", OldName = "A", NewName = "B" };
            var result = Make().TryRewrite(root, wrongRule, ctx);
            return result;
        })
        .Then("result is null", r => r is null)
        .AssertPassed();

    [Scenario("No matching call site returns null")]
    [Fact]
    public Task RemoveMember_NoMatch_ReturnsNull() =>
        Given("source with no calls to Deprecated", () =>
        {
            var (root, ctx) = Parse("class C { void M() { } }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return result;
        })
        .Then("result is null", r => r is null)
        .AssertPassed();

    [Scenario("Trivia around commented-out expression is preserved")]
    [Fact]
    public Task RemoveMember_Trivia_IsPreserved() =>
        Given("source with leading whitespace on the call statement", () =>
        {
            var src = "class C { void M(OldApi obj) {\n    obj.Deprecated();\n} }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, Rule(), ctx);
            return result!.ToString();
        })
        .Then("MIGRATION comment is in the output", s => s.Contains("MIGRATION"))
        .AssertPassed();

    [Scenario("Kind property returns removeMember")]
    [Fact]
    public Task RemoveMemberRewriter_Kind_IsRemoveMember() =>
        Given("a RemoveMemberRewriter instance", () => Make())
        .Then("Kind is removeMember", r => r.Kind == "removeMember")
        .AssertPassed();
}
