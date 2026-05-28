using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("ChangeTypeReferenceRewriter: replace old type references with new type in declarations and expressions")]
public sealed class ChangeTypeReferenceRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ChangeTypeReferenceRewriter Make() => new();

    private static ChangeTypeReferenceRule Rule(string old = "IList", string @new = "IReadOnlyList") =>
        new() { Id = "CTR-001", OldType = old, NewType = @new };

    private static (SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return (tree.GetRoot(), new RewriteContext("test.cs"));
    }

    // ── happy ────────────────────────────────────────────────────────────────

    [Scenario("Field declaration type is replaced")]
    [Fact]
    public Task ChangeTypeReference_FieldDeclaration_Replaced() =>
        Given("source with 'IList<string> items' and rule IList->IReadOnlyList", () =>
        {
            var (root, ctx) = Parse("class C { IList<string> items; }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains IReadOnlyList", t => t.Result!.ToString().Contains("IReadOnlyList"))
        .And("result does not contain IList<", t => !t.Result!.ToString().Contains("IList<"))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("typeof expression type is replaced")]
    [Fact]
    public Task ChangeTypeReference_TypeofExpression_Replaced() =>
        Given("source with typeof(IList<int>) and rule IList->IReadOnlyList", () =>
        {
            var (root, ctx) = Parse("class C { void M() { var t = typeof(IList<int>); } }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result contains IReadOnlyList", t => t.Result!.ToString().Contains("IReadOnlyList"))
        .And("Applied count >= 1", t => t.Ctx.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("Multiple type references in file are all replaced")]
    [Fact]
    public Task ChangeTypeReference_MultipleOccurrences_AllReplaced() =>
        Given("source with three IList<T> references", () =>
        {
            var (root, ctx) = Parse(
                "class C { IList<int> a; IList<string> b; void M(IList<bool> p) {} }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count is 3", t => t.Ctx.Applied.Count == 3)
        .And("no IList< remains", t => !t.Result!.ToString().Contains("IList<"))
        .AssertPassed();

    [Scenario("Wrong rule kind returns null")]
    [Fact]
    public Task ChangeTypeReference_WrongRuleKind_ReturnsNull() =>
        Given("a RenameTypeRule passed to ChangeTypeReferenceRewriter", () =>
        {
            var (root, ctx) = Parse("class C { IList<int> a; }");
            var wrongRule = new RenameTypeRule { Id = "X", OldName = "A", NewName = "B" };
            var result = Make().TryRewrite(root, wrongRule, ctx);
            return result;
        })
        .Then("result is null", r => r is null)
        .AssertPassed();

    [Scenario("No matching type reference returns null")]
    [Fact]
    public Task ChangeTypeReference_NoMatch_ReturnsNull() =>
        Given("source with no IList references", () =>
        {
            var (root, ctx) = Parse("class C { int x; }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return result;
        })
        .Then("result is null", r => r is null)
        .AssertPassed();

    [Scenario("Trivia is preserved on replaced type node")]
    [Fact]
    public Task ChangeTypeReference_Trivia_IsPreserved() =>
        Given("source with comments near the type reference", () =>
        {
            var src = "class C {\n    // collection field\n    IList<string>  items;\n}";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, Rule(), ctx);
            // Use ToFullString() to capture trivia (comments, whitespace) around nodes
            return result!.ToFullString();
        })
        .Then("comment is preserved", s => s.Contains("// collection field"))
        .And("IReadOnlyList is in result", s => s.Contains("IReadOnlyList"))
        .AssertPassed();

    [Scenario("Partial name collision: IListExtensions is not replaced")]
    [Fact]
    public Task ChangeTypeReference_PartialNameCollision_NotReplaced() =>
        Given("source with IListExtensions type and rule targeting IList", () =>
        {
            var (root, ctx) = Parse("class C { IListExtensions ext; }");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null (no match)", t => t.Result is null)
        .And("Applied count is 0", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("Kind property returns changeTypeReference")]
    [Fact]
    public Task ChangeTypeReferenceRewriter_Kind_IsChangeTypeReference() =>
        Given("a ChangeTypeReferenceRewriter instance", () => Make())
        .Then("Kind is changeTypeReference", r => r.Kind == "changeTypeReference")
        .AssertPassed();
}
