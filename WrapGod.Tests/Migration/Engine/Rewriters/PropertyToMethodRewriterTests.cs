using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters.Structural;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

[Feature("PropertyToMethodRewriter: property read/write access becomes getter/setter method calls")]
public sealed class PropertyToMethodRewriterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PropertyToMethodRewriter Make() => new();

    private static PropertyToMethodRule DisabledRule(string newMethodName = "SetDisabled") =>
        new()
        {
            Id = "PTM-001",
            TypeName = "Button",
            OldPropertyName = "Disabled",
            NewMethodName = newMethodName,
        };

    private static (Microsoft.CodeAnalysis.SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var ctx = new RewriteContext("test.cs");
        return (tree.GetRoot(), ctx);
    }

    // ── happy ─────────────────────────────────────────────────────────────────

    [Scenario("Write context: btn.Disabled = true becomes btn.SetDisabled(true)")]
    [Fact]
    public Task PropertyToMethod_WriteContext_BecomesSetCall() =>
        Given("source 'btn.Disabled = true;' and rule NewMethodName=SetDisabled", () =>
        {
            var src = "class C { void M() { Button btn = new Button(); btn.Disabled = true; } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DisabledRule("SetDisabled"), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains SetDisabled(", t => t.Result!.ToString().Contains("SetDisabled("))
        .And("original 'Disabled =' is not in result", t => !t.Result!.ToString().Contains("Disabled ="))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("Read context: var b = btn.Disabled becomes var b = btn.GetDisabled() when rule specifies GetDisabled")]
    [Fact]
    public Task PropertyToMethod_ReadContext_BecomesGetCall() =>
        Given("source 'var b = btn.Disabled;' and rule NewMethodName=GetDisabled", () =>
        {
            var src = "class C { void M() { Button btn = new Button(); var b = btn.Disabled; } }";
            var (root, ctx) = Parse(src);
            // NewMethodName = "GetDisabled" (verbatim) → reads become GetDisabled()
            var result = Make().TryRewrite(root, DisabledRule("GetDisabled"), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("result contains GetDisabled()", t => t.Result!.ToString().Contains("GetDisabled()"))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("Mixed read and write in same method with SetDisabled rule — both become SetDisabled calls")]
    [Fact]
    public Task PropertyToMethod_ReadAndWrite_BothRewritten() =>
        Given("source with both read and write of Disabled and rule NewMethodName=SetDisabled", () =>
        {
            var src = "class C { void M() { Button btn = new Button(); var b = btn.Disabled; btn.Disabled = false; } }";
            var (root, ctx) = Parse(src);
            // NewMethodName = "SetDisabled" (verbatim): reads become SetDisabled(), writes become SetDisabled(false)
            var result = Make().TryRewrite(root, DisabledRule("SetDisabled"), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("Applied count is 2", t => t.Ctx.Applied.Count == 2)
        .And("result contains two SetDisabled usages", t =>
        {
            var text = t.Result!.ToString();
            int count = 0, idx = 0;
            while ((idx = text.IndexOf("SetDisabled", idx, StringComparison.Ordinal)) >= 0) { count++; idx++; }
            return count >= 2;
        })
        .And("result no longer contains btn.Disabled as a property", t =>
            !t.Result!.ToString().Contains("btn.Disabled ="))
        .AssertPassed();

    // ── sad ──────────────────────────────────────────────────────────────────

    [Scenario("Compound-assignment btn.Disabled++ produces SkippedRewrite")]
    [Fact]
    public Task PropertyToMethod_CompoundAssignment_ProducesSkipped() =>
        Given("source with 'btn.Disabled++;' compound assignment", () =>
        {
            var src = "class C { void M() { Button btn = new Button(); btn.Disabled++; } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DisabledRule("SetDisabled"), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null (no applied rewrite)", t => t.Result is null)
        .And("Skipped contains compound-assignment reason", t =>
            t.Ctx.Skipped.Any(s => s.Reason.Contains("compound-assignment")))
        .AssertPassed();

    [Scenario("Wrong rule kind returns null")]
    [Fact]
    public Task PropertyToMethod_WrongRuleKind_ReturnsNull() =>
        Given("a RenameMemberRule passed to PropertyToMethodRewriter", () =>
        {
            var (root, ctx) = Parse("class C { void M() { btn.Disabled = true; } }");
            var wrongRule = new RenameMemberRule { Id = "X-001", TypeName = "Button", OldMemberName = "Disabled", NewMemberName = "Enabled" };
            var result = Make().TryRewrite(root, wrongRule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("No matching property name returns null")]
    [Fact]
    public Task PropertyToMethod_NoMatch_ReturnsNull() =>
        Given("source with no Disabled property access", () =>
        {
            var (root, ctx) = Parse("class C { void M() { var x = btn.OtherProp; } }");
            var result = Make().TryRewrite(root, DisabledRule(), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null", t => t.Result is null)
        .And("no Applied entries", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    // ── edge ─────────────────────────────────────────────────────────────────

    [Scenario("NewMethodName=ChangeDisabled — reads and writes both call ChangeDisabled verbatim")]
    [Fact]
    public Task PropertyToMethod_NoGetSetPrefix_UsedVerbatim() =>
        Given("rule with NewMethodName=ChangeDisabled applied to both read and write contexts", () =>
        {
            var src = "class C { void M() { Button btn = new Button(); var b = btn.Disabled; btn.Disabled = true; } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DisabledRule("ChangeDisabled"), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("read context has ChangeDisabled() call (verbatim, no Get prefix)", t =>
            t.Result!.ToString().Contains("ChangeDisabled()"))
        .And("write context has ChangeDisabled(true) call (verbatim, no Set prefix)", t =>
            t.Result!.ToString().Contains("ChangeDisabled(true)"))
        .AssertPassed();

    [Scenario("NewMethodName=GetDisabled: used verbatim on reads (no GetGet prefix)")]
    [Fact]
    public Task PropertyToMethod_NewMethodNameStartsWithGet_UsedVerbatim() =>
        Given("rule with NewMethodName=GetDisabled applied to read context", () =>
        {
            var src = "class C { void M() { Button btn = new Button(); var b = btn.Disabled; } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DisabledRule("GetDisabled"), ctx);
            return result!.ToString();
        })
        .Then("result contains GetDisabled() (verbatim, not GetGetDisabled())", s =>
            s.Contains("GetDisabled()") && !s.Contains("GetGetDisabled()"))
        .AssertPassed();

    [Scenario("Kind property returns propertyToMethod")]
    [Fact]
    public Task PropertyToMethodRewriter_Kind_IsPropertyToMethod() =>
        Given("a PropertyToMethodRewriter instance", () => Make())
        .Then("Kind is propertyToMethod", r => r.Kind == "propertyToMethod")
        .AssertPassed();

    // ── regression: review feedback ──────────────────────────────────────────

    [Scenario("nameof(btn.Disabled) is NOT rewritten — reflection-style semantics preserved")]
    [Fact]
    public Task PropertyToMethod_InsideNameOf_NotRewritten() =>
        Given("source with 'var s = nameof(btn.Disabled);' and SetDisabled rule", () =>
        {
            var src = "class C { void M() { Button btn = new Button(); var s = nameof(btn.Disabled); } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DisabledRule("SetDisabled"), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null (no rewrite applied inside nameof)", t => t.Result is null)
        .And("no Applied entries — Disabled was NOT rewritten", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    [Scenario("Mixed: nameof(btn.Disabled) preserved AND btn.Disabled = true is rewritten")]
    [Fact]
    public Task PropertyToMethod_MixedNameOfAndAssignment_OnlyAssignmentRewritten() =>
        Given("source with both nameof use and a real assignment", () =>
        {
            var src = "class C { void M() { Button btn = new Button(); var s = nameof(btn.Disabled); btn.Disabled = true; } }";
            var (root, ctx) = Parse(src);
            var result = Make().TryRewrite(root, DisabledRule("SetDisabled"), ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("Applied count is 1 (only the assignment, not the nameof)", t => t.Ctx.Applied.Count == 1)
        .And("nameof(btn.Disabled) is preserved", t => t.Result!.ToString().Contains("nameof(btn.Disabled)"))
        .And("btn.SetDisabled(true) appears (assignment was rewritten)", t =>
            t.Result!.ToString().Contains("SetDisabled(true)"))
        .AssertPassed();
}
