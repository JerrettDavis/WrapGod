using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("RenameTypeRewriter: rename a type's short name across declarations and usages")]
public sealed class RenameTypeRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static RenameTypeRewriter Make() => new();

    private static RenameTypeRule SameNsRule(string old = "Foo.Bar", string @new = "Foo.Baz") =>
        new() { Id = "RT-001", OldName = old, NewName = @new };

    private static RenameTypeRule CrossNsRule() =>
        new() { Id = "RT-002", OldName = "OldNs.OldType", NewName = "NewNs.NewType" };

    private static (SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var ctx = new RewriteContext("test.cs");
        return (tree.GetRoot(), ctx);
    }

    // ── happy ────────────────────────────────────────────────────────────────

    [Scenario("Short name is replaced in a simple variable declaration")]
    [Fact]
    public Task RenameType_VariableDeclaration_ReplacesShortName() =>
        Given("source with 'Bar x = new Bar();' and rule OldName=Foo.Bar NewName=Foo.Baz", () =>
        {
            var (root, ctx) = Parse("class C { Foo.Bar x = new Foo.Bar(); }");
            var rule = SameNsRule();
            var rewriter = Make();
            var result = rewriter.TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains Baz", t => t.Result!.ToString().Contains("Baz"))
        .And("result does not contain Bar as type", t => !t.Result!.ToString().Contains("Foo.Bar"))
        .And("at least one Applied entry recorded", t => t.Ctx.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("Multiple occurrences of the type name are all replaced")]
    [Fact]
    public Task RenameType_MultipleOccurrences_AllReplaced() =>
        Given("source with three usages of OldType and rule OldName=Foo.OldType NewName=Foo.NewType", () =>
        {
            var (root, ctx) = Parse(
                "class C { Foo.OldType a; Foo.OldType b; void M(Foo.OldType p) {} }");
            var rule = SameNsRule("Foo.OldType", "Foo.NewType");
            var rewriter = Make();
            var result = rewriter.TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result has no remaining OldType references", t =>
            !t.Result!.ToString().Contains("OldType"))
        .And("Applied count is 3", t => t.Ctx.Applied.Count == 3)
        .AssertPassed();

    [Scenario("Wrong rule kind returns null without modifying anything")]
    [Fact]
    public Task RenameType_WrongRuleKind_ReturnsNull() =>
        Given("a RenameNamespaceRule passed to RenameTypeRewriter", () =>
        {
            var (root, ctx) = Parse("class C { Bar x; }");
            var wrongRule = new RenameNamespaceRule { Id = "X-001", OldNamespace = "Old", NewNamespace = "New" };
            var rewriter = Make();
            var result = rewriter.TryRewrite(root, wrongRule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .And("no Skipped entries", t => t.Ctx.Skipped.Count == 0)
        .AssertPassed();

    [Scenario("No matching type name in source returns null")]
    [Fact]
    public Task RenameType_NoMatch_ReturnsNull() =>
        Given("source with only 'Qux' type and rule targeting 'Bar'", () =>
        {
            var (root, ctx) = Parse("class C { Qux x; }");
            var rule = SameNsRule("Foo.Bar", "Foo.Baz");
            var rewriter = Make();
            var result = rewriter.TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("Trivia is preserved around replaced type identifier")]
    [Fact]
    public Task RenameType_Trivia_IsPreserved() =>
        Given("source with leading comment and whitespace around the type reference", () =>
        {
            var src = "class C {\n    // comment\n    Foo.Bar  x;\n}";
            var (root, ctx) = Parse(src);
            var rule = SameNsRule();
            var rewriter = Make();
            var result = rewriter.TryRewrite(root, rule, ctx);
            return result!.ToString();
        })
        .Then("leading comment is still present", s => s.Contains("// comment"))
        .And("replaced name is Baz", s => s.Contains("Baz"))
        .AssertPassed();

    [Scenario("Partial-name collision: FooBarType is not renamed when rule targets Foo")]
    [Fact]
    public Task RenameType_PartialNameCollision_NotRenamed() =>
        Given("source with 'FooBarType' and rule targeting short name 'Foo'", () =>
        {
            var (root, ctx) = Parse("class C { FooBarType x; }");
            var rule = SameNsRule("Ns.Foo", "Ns.Baz");
            var rewriter = Make();
            var result = rewriter.TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null (no match)", t => t.Result is null)
        .And("FooBarType is not in Applied", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("Kind property returns renameType")]
    [Fact]
    public Task RenameTypeRewriter_Kind_IsRenameType() =>
        Given("a RenameTypeRewriter instance", () => Make())
        .Then("Kind is renameType", r => r.Kind == "renameType")
        .AssertPassed();
}
