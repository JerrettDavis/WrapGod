using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("ChangeParameterRewriter: rename named arguments; skip positional when type changes")]
public sealed class ChangeParameterRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ChangeParameterRewriter Make() => new();

    private static ChangeParameterRule NameRenameRule() =>
        new()
        {
            Id = "CP-001",
            TypeName = "Builder",
            MethodName = "Build",
            OldParameterName = "size",
            NewParameterName = "buttonSize",
            OldParameterType = null,
            NewParameterType = null,
        };

    private static ChangeParameterRule TypeChangeRule() =>
        new()
        {
            Id = "CP-002",
            TypeName = "Builder",
            MethodName = "Build",
            OldParameterName = "size",
            NewParameterName = null,
            OldParameterType = "int",
            NewParameterType = "MudSize",
        };

    private static (SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return (tree.GetRoot(), new RewriteContext("test.cs"));
    }

    // ── happy ────────────────────────────────────────────────────────────────

    [Scenario("Named argument label is renamed when parameter name changed")]
    [Fact]
    public Task ChangeParameter_NamedArg_Renamed() =>
        Given("source with Build(size: 12) and rule renaming size->buttonSize", () =>
        {
            var (root, ctx) = Parse("class C { void M(Builder b) { b.Build(size: 12); } }");
            var result = Make().TryRewrite(root, NameRenameRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains buttonSize:", t => t.Result!.ToString().Contains("buttonSize:"))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("Positional argument with type change records SkippedRewrite")]
    [Fact]
    public Task ChangeParameter_PositionalWithTypeChange_RecordsSkipped() =>
        Given("source with Build(12) and rule changing type int->MudSize", () =>
        {
            var (root, ctx) = Parse("class C { void M(Builder b) { b.Build(12); } }");
            var result = Make().TryRewrite(root, TypeChangeRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Skipped count >= 1", t => t.Ctx.Skipped.Count >= 1)
        .And("Skipped reason mentions type change", t =>
            t.Ctx.Skipped[0].Reason.Contains("type", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Wrong rule kind returns null")]
    [Fact]
    public Task ChangeParameter_WrongRuleKind_ReturnsNull() =>
        Given("a RemoveMemberRule passed to ChangeParameterRewriter", () =>
        {
            var (root, ctx) = Parse("class C { void M(Builder b) { b.Build(size: 12); } }");
            var wrongRule = new RemoveMemberRule { Id = "X", TypeName = "T", MemberName = "M" };
            var result = Make().TryRewrite(root, wrongRule, ctx);
            return result;
        })
        .Then("result is null", r => r is null)
        .AssertPassed();

    [Scenario("No matching invocation returns null")]
    [Fact]
    public Task ChangeParameter_NoMatch_ReturnsNull() =>
        Given("source with no Build invocation", () =>
        {
            var (root, ctx) = Parse("class C { void M() { } }");
            var result = Make().TryRewrite(root, NameRenameRule(), ctx);
            return result;
        })
        .Then("result is null", r => r is null)
        .AssertPassed();

    [Scenario("Trivia is preserved on renamed named argument label")]
    [Fact]
    public Task ChangeParameter_Trivia_IsPreserved() =>
        Given("source with leading whitespace around named argument", () =>
        {
            var src = "class C { void M(Builder b) { b.Build(\n    size:  12\n); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, NameRenameRule(), ctx);
            return result!.ToString();
        })
        .Then("buttonSize: is present", s => s.Contains("buttonSize:"))
        .And("size: is gone", s => !s.Contains("size:"))
        .AssertPassed();

    [Scenario("Multiple named argument labels of same name are all renamed")]
    [Fact]
    public Task ChangeParameter_MultipleNamedArgs_AllRenamed() =>
        Given("source with two calls both using size: named argument", () =>
        {
            var (root, ctx) = Parse(
                "class C { void M(Builder b) { b.Build(size: 1); b.Build(size: 2); } }");
            var result = Make().TryRewrite(root, NameRenameRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count is 2", t => t.Ctx.Applied.Count == 2)
        .And("no remaining size: labels", t => !t.Result!.ToString().Contains("size:"))
        .AssertPassed();

    [Scenario("Kind property returns changeParameter")]
    [Fact]
    public Task ChangeParameterRewriter_Kind_IsChangeParameter() =>
        Given("a ChangeParameterRewriter instance", () => Make())
        .Then("Kind is changeParameter", r => r.Kind == "changeParameter")
        .AssertPassed();
}
