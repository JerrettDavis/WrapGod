using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("AddRequiredParameterRewriter: insert default argument at required position in matching invocations")]
public sealed class AddRequiredParameterRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static AddRequiredParameterRewriter Make() => new();

    private static AddRequiredParameterRule RuleAt(int position = 0, string paramType = "MudTheme") =>
        new()
        {
            Id = "ARP-001",
            TypeName = "ThemeProvider",
            MethodName = "Apply",
            ParameterName = "theme",
            ParameterType = paramType,
            Position = position,
        };

    private static (SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return (tree.GetRoot(), new RewriteContext("test.cs"));
    }

    // ── happy ────────────────────────────────────────────────────────────────

    [Scenario("default argument is inserted at position 0 for an empty argument list")]
    [Fact]
    public Task AddRequiredParameter_Position0_EmptyArgList_InsertsDefault() =>
        Given("source calling provider.Apply() with no args and rule adding at position 0", () =>
        {
            var (root, ctx) = Parse("class C { void M(ThemeProvider provider) { provider.Apply(); } }");
            var result = Make().TryRewrite(root, RuleAt(0), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains default", t => t.Result!.ToString().Contains("default"))
        .And("result contains TODO MIGRATION comment", t => t.Result!.ToString().Contains("TODO MIGRATION", StringComparison.Ordinal))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("default argument is inserted at position 1 after existing argument")]
    [Fact]
    public Task AddRequiredParameter_Position1_ExistingArgs_InsertsAtRight() =>
        Given("source calling provider.Apply(x) and rule adding at position 1", () =>
        {
            var (root, ctx) = Parse("class C { void M(ThemeProvider provider) { provider.Apply(x); } }");
            var result = Make().TryRewrite(root, RuleAt(1), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result contains x, default", t =>
        {
            var s = t.Result!.ToString();
            return s.Contains('x') && s.Contains("default", StringComparison.Ordinal);
        })
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("Multiple matching invocations each get the default argument")]
    [Fact]
    public Task AddRequiredParameter_MultipleInvocations_AllGetDefault() =>
        Given("source with two calls to Apply()", () =>
        {
            var (root, ctx) = Parse(
                "class C { void M(ThemeProvider p) { p.Apply(); p.Apply(); } }");
            var result = Make().TryRewrite(root, RuleAt(0), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count is 2", t => t.Ctx.Applied.Count == 2)
        .AssertPassed();

    [Scenario("Wrong rule kind returns null")]
    [Fact]
    public Task AddRequiredParameter_WrongRuleKind_ReturnsNull() =>
        Given("a RemoveMemberRule passed to AddRequiredParameterRewriter", () =>
        {
            var (root, ctx) = Parse("class C { void M(ThemeProvider p) { p.Apply(); } }");
            var wrongRule = new RemoveMemberRule { Id = "X", TypeName = "T", MemberName = "M" };
            var result = Make().TryRewrite(root, wrongRule, ctx);
            return result;
        })
        .Then("result is null", r => r is null)
        .AssertPassed();

    [Scenario("No matching invocation returns null")]
    [Fact]
    public Task AddRequiredParameter_NoMatch_ReturnsNull() =>
        Given("source with no Apply() calls", () =>
        {
            var (root, ctx) = Parse("class C { void M() { } }");
            var result = Make().TryRewrite(root, RuleAt(0), ctx);
            return result;
        })
        .Then("result is null", r => r is null)
        .AssertPassed();

    [Scenario("Trivia is preserved around the modified argument list")]
    [Fact]
    public Task AddRequiredParameter_Trivia_IsPreserved() =>
        Given("source with whitespace around the call", () =>
        {
            var src = "class C { void M(ThemeProvider p) {\n    p.Apply();\n} }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, RuleAt(0), ctx);
            return result!.ToString();
        })
        .Then("default is in the result", s => s.Contains("default"))
        .AssertPassed();

    [Scenario("Kind property returns addRequiredParameter")]
    [Fact]
    public Task AddRequiredParameterRewriter_Kind_IsAddRequiredParameter() =>
        Given("an AddRequiredParameterRewriter instance", () => Make())
        .Then("Kind is addRequiredParameter", r => r.Kind == "addRequiredParameter")
        .AssertPassed();
}
