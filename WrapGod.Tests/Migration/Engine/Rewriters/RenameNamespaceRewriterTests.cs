using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("RenameNamespaceRewriter: rewrite using directives and qualified names when a namespace changes")]
public sealed class RenameNamespaceRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static RenameNamespaceRewriter Make() => new();

    private static RenameNamespaceRule Rule(string old = "OldNs", string @new = "NewNs") =>
        new() { Id = "RNS-001", OldNamespace = old, NewNamespace = @new };

    private static (SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return (tree.GetRoot(), new RewriteContext("test.cs"));
    }

    // ── happy ────────────────────────────────────────────────────────────────

    [Scenario("using directive with exact match is rewritten")]
    [Fact]
    public Task RenameNamespace_UsingDirective_ExactMatch_Rewritten() =>
        Given("source with 'using OldNs;' and rule OldNs->NewNs", () =>
        {
            var (root, ctx) = Parse("using OldNs;");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains NewNs", t => t.Result!.ToString().Contains("NewNs"))
        .And("result does not contain OldNs", t => !t.Result!.ToString().Contains("OldNs"))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("using directive with sub-namespace is rewritten")]
    [Fact]
    public Task RenameNamespace_UsingDirective_SubNamespace_Rewritten() =>
        Given("source with 'using OldNs.Sub;' and rule OldNs->NewNs", () =>
        {
            var (root, ctx) = Parse("using OldNs.Sub;");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result contains NewNs.Sub", t => t.Result!.ToString().Contains("NewNs.Sub"))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("Multiple using directives in same file are all updated")]
    [Fact]
    public Task RenameNamespace_MultipleUsings_AllRewritten() =>
        Given("source with 'using OldNs; using OldNs.A; using OldNs.B;'", () =>
        {
            var (root, ctx) = Parse("using OldNs;\nusing OldNs.A;\nusing OldNs.B;");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result has 3 Applied entries", t => t.Ctx.Applied.Count == 3)
        .And("no OldNs references remain", t => !t.Result!.ToString().Contains("OldNs"))
        .AssertPassed();

    [Scenario("Wrong rule kind returns null")]
    [Fact]
    public Task RenameNamespace_WrongRuleKind_ReturnsNull() =>
        Given("a RenameTypeRule passed to RenameNamespaceRewriter", () =>
        {
            var (root, ctx) = Parse("using OldNs;");
            var wrongRule = new RenameTypeRule { Id = "X", OldName = "A", NewName = "B" };
            var result = Make().TryRewrite(root, wrongRule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("No matching namespace returns null")]
    [Fact]
    public Task RenameNamespace_NoMatch_ReturnsNull() =>
        Given("source with 'using SomeOtherNs;' and rule OldNs->NewNs", () =>
        {
            var (root, ctx) = Parse("using SomeOtherNs;");
            var result = Make().TryRewrite(root, Rule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .AssertPassed();

    [Scenario("Trivia is preserved on rewritten using directive")]
    [Fact]
    public Task RenameNamespace_Trivia_IsPreserved() =>
        Given("source with comment before the using", () =>
        {
            var src = "// namespace import\nusing OldNs.Tools;";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, Rule(), ctx);
            // Use ToFullString() to include leading/trailing trivia (comments, whitespace)
            return result!.ToFullString();
        })
        .Then("comment is preserved", s => s.Contains("// namespace import"))
        .And("NewNs.Tools is present", s => s.Contains("NewNs.Tools"))
        .AssertPassed();

    [Scenario("Kind property returns renameNamespace")]
    [Fact]
    public Task RenameNamespaceRewriter_Kind_IsRenameNamespace() =>
        Given("a RenameNamespaceRewriter instance", () => Make())
        .Then("Kind is renameNamespace", r => r.Kind == "renameNamespace")
        .AssertPassed();
}
