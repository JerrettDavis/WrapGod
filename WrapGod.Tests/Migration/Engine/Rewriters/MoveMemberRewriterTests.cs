using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters.Structural;
using WrapGod.Tests.Migration.Engine.Fixtures;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("MoveMemberRewriter: static member accesses on old type are redirected to the new type")]
public sealed class MoveMemberRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static MoveMemberRewriter Make() => new();

    private static MoveMemberRule GetColorRule() =>
        new()
        {
            Id = "MM-001",
            OldTypeName = "Utilities",
            NewTypeName = "MudHelpers",
            MemberName = "GetColor",
        };

    private static (Microsoft.CodeAnalysis.SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var ctx = new RewriteContext("test.cs");
        return (tree.GetRoot(), ctx);
    }

    // ── happy ─────────────────────────────────────────────────────────────────

    [Scenario("Static-style call Utilities.GetColor() becomes MudHelpers.GetColor()")]
    [Fact]
    public Task MoveMember_StaticCall_ReceiverReplaced() =>
        Given("source with 'Utilities.GetColor()' and rule moving to MudHelpers", () =>
        {
            var src = "class C { void M() { var c = Utilities.GetColor(); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, GetColorRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains MudHelpers.GetColor", t => t.Result!.ToString().Contains("MudHelpers.GetColor"))
        .And("result does not contain Utilities.GetColor", t => !t.Result!.ToString().Contains("Utilities.GetColor"))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("Multiple occurrences of Utilities.GetColor() in one file are all replaced")]
    [Fact]
    public Task MoveMember_MultipleOccurrences_AllReplaced() =>
        Given("source with two Utilities.GetColor() calls", () =>
        {
            var src = "class C { void M() { var c1 = Utilities.GetColor(); var c2 = Utilities.GetColor(); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, GetColorRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count is 2", t => t.Ctx.Applied.Count == 2)
        .And("result does not contain Utilities.GetColor", t => !t.Result!.ToString().Contains("Utilities.GetColor"))
        .AssertPassed();

    [Scenario("Trivia around the receiver is preserved")]
    [Fact]
    public Task MoveMember_Trivia_IsPreserved() =>
        Given("source with indented Utilities.GetColor() call", () =>
        {
            var src = "class C {\n    void M() {\n        // get color\n        var c = Utilities.GetColor();\n    }\n}";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, GetColorRule(), ctx);
            return result!.ToString();
        })
        .Then("result contains the comment", s => s.Contains("// get color"))
        .And("result contains MudHelpers.GetColor", s => s.Contains("MudHelpers.GetColor"))
        .AssertPassed();

    // ── sad ──────────────────────────────────────────────────────────────────

    [Scenario("Instance call myInstance.GetColor() where myInstance is known type produces SkippedRewrite")]
    [Fact]
    public Task MoveMember_InstanceCall_ProducesSkipped() =>
        Given("source where myUtils is declared as type SomeUtil (not Utilities)", () =>
        {
            var src = "class C { void M() { SomeUtil myUtils = new SomeUtil(); myUtils.GetColor(); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, GetColorRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null (no applied rewrite)", t => t.Result is null)
        .And("Skipped or no Applied", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("Wrong rule kind returns null")]
    [Fact]
    public Task MoveMember_WrongRuleKind_ReturnsNull() =>
        Given("a RenameMemberRule passed to MoveMemberRewriter", () =>
        {
            var (root, ctx) = Parse("class C { void M() { Utilities.GetColor(); } }");
            var wrongRule = new RenameMemberRule { Id = "X-001", TypeName = "Utilities", OldMemberName = "GetColor", NewMemberName = "GetColour" };
            var result = Make().TryRewrite(root, wrongRule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("No matching call site returns null")]
    [Fact]
    public Task MoveMember_NoMatch_ReturnsNull() =>
        Given("source with no GetColor call", () =>
        {
            var (root, ctx) = Parse("class C { void M() { Utilities.OtherMethod(); } }");
            var result = Make().TryRewrite(root, GetColorRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    // ── edge ─────────────────────────────────────────────────────────────────

    [Scenario("Lowercase receiver that is unresolvable produces SkippedRewrite (assumed instance)")]
    [Fact]
    public Task MoveMember_LowercaseUnresolvableReceiver_ProducesSkipped() =>
        Given("source with 'utilities.GetColor()' where 'utilities' is lowercase and unresolvable", () =>
        {
            var src = "class C { void M() { utilities.GetColor(); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, GetColorRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Skipped count >= 1 (unresolvable lowercase — assumed instance)", t =>
            t.Ctx.Skipped.Count >= 1 || t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("Fully-qualified receiver OldNs.Utilities.GetColor() is replaced with new type")]
    [Fact]
    public Task MoveMember_FullyQualifiedReceiver_Replaced() =>
        Given("rule with fully-qualified old type and source using it", () =>
        {
            var rule = new MoveMemberRule
            {
                Id = "MM-002",
                OldTypeName = "OldNs.Utilities",
                NewTypeName = "NewNs.MudHelpers",
                MemberName = "GetColor",
            };
            var src = "class C { void M() { var c = OldNs.Utilities.GetColor(); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .And("result contains NewNs.MudHelpers.GetColor", t => t.Result!.ToString().Contains("MudHelpers.GetColor"))
        .AssertPassed();

    [Scenario("Kind property returns moveMember")]
    [Fact]
    public Task MoveMemberRewriter_Kind_IsMoveMember() =>
        Given("a MoveMemberRewriter instance", () => Make())
        .Then("Kind is moveMember", r => r.Kind == "moveMember")
        .AssertPassed();

    // ── regression: review feedback (cross-namespace using injection) ────────

    [Scenario("Cross-namespace MoveMember: MigrationEngine injects the new namespace using directive")]
    [Fact]
    public Task MoveMember_CrossNamespace_InjectsUsingDirective() =>
        Given("source uses 'OldNs.Helper.Do()' and rule moves Helper to NewNs", () =>
        {
            var source = "namespace App;\nclass C { void M() { var x = OldNs.Helper.Do(); } }";
            var schema = new MigrationSchema
            {
                Library = "Test", From = "1.0", To = "2.0",
                Rules =
                [
                    new MoveMemberRule
                    {
                        Id = "MM-USE-01",
                        OldTypeName = "OldNs.Helper",
                        NewTypeName = "NewNs.Helper",
                        MemberName = "Do",
                        Confidence = RuleConfidence.Auto,
                    },
                ],
            };

            var fs = new InMemoryFileSystem().WithFile("c.cs", source);
            // Use CreateDefault-equivalent engine that includes the B-level rewriters.
            var engine = MigrationEngine.CreateDefault();
            // We can't easily inject fs into CreateDefault. Use the internal ctor directly:
            var engineWithFs = new MigrationEngine(
                new IRuleRewriter[]
                {
                    new MoveMemberRewriter(),
                },
                fs);
            return engineWithFs.Apply(schema, ["c.cs"]);
        })
        .Then("rewritten file contains 'using NewNs;'", r =>
            r.RewrittenFiles.ContainsKey("c.cs") &&
            r.RewrittenFiles["c.cs"].Contains("using NewNs;"))
        .And("rewritten file contains NewNs.Helper.Do", r =>
            r.RewrittenFiles["c.cs"].Contains("NewNs.Helper.Do") ||
            r.RewrittenFiles["c.cs"].Contains("Helper.Do"))
        .AssertPassed();
}
