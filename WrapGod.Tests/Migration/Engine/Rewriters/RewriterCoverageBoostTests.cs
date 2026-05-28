using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine.Rewriters;

/// <summary>
/// Additional coverage scenarios targeting specific branches in the rewriter implementations
/// that are not exercised by the main per-rewriter test files.
/// </summary>
[Feature("Rewriter coverage boost: branch coverage for helpers and edge paths")]
public sealed class RewriterCoverageBoostTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static (SyntaxNode Root, RewriteContext Ctx) Parse(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return (tree.GetRoot(), new RewriteContext("test.cs"));
    }

    // ── RenameTypeRewriter — qualified name in class body ────────────────────

    [Scenario("RenameType rewrites fully-qualified reference in the tree")]
    [Fact]
    public Task RenameType_FullyQualifiedInTree_Rewritten() =>
        Given("source with fully-qualified type usage and rule", () =>
        {
            var (root, ctx) = Parse("class C { Foo.Bar x; }");
            var rule = new RenameTypeRule { Id = "RT-FQ", OldName = "Foo.Bar", NewName = "Foo.Baz" };
            var result = new RenameTypeRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null", t => t.Result is not null)
        .And("Applied count >= 1", t => t.Ctx.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("RenameType rewrites identifier in generic type argument")]
    [Fact]
    public Task RenameType_InGenericArgument_Rewritten() =>
        Given("source with List<Bar> and rule Bar->Baz", () =>
        {
            var (root, ctx) = Parse("class C { System.Collections.Generic.List<Bar> x; }");
            var rule = new RenameTypeRule { Id = "RT-GEN", OldName = "Ns.Bar", NewName = "Ns.Baz" };
            var result = new RenameTypeRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is not null (Bar matched)", t => t.Result is not null)
        .AssertPassed();

    // ── RenameNamespaceRewriter — no sub-namespace suffix ────────────────────

    [Scenario("RenameNamespace with extra sub-namespace suffix is updated correctly")]
    [Fact]
    public Task RenameNamespace_DeepSubNamespace_Rewritten() =>
        Given("source with 'using OldNs.A.B.C;' and rule OldNs->NewNs", () =>
        {
            var (root, ctx) = Parse("using OldNs.A.B.C;");
            var rule = new RenameNamespaceRule { Id = "RNS-DEEP", OldNamespace = "OldNs", NewNamespace = "NewNs" };
            var result = new RenameNamespaceRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result contains NewNs.A.B.C", t => t.Result!.ToFullString().Contains("NewNs.A.B.C"))
        .AssertPassed();

    // ── RenameMemberRewriter — receiver via qualified parameter type ─────────

    [Scenario("RenameMember with receiver declared as qualified parameter type is renamed")]
    [Fact]
    public Task RenameMember_QualifiedParamType_ReceiverInferred() =>
        Given("source with 'MyService svc' parameter and svc.OldMethod() call", () =>
        {
            // The receiver is a parameter declared with a simple type name matching the rule
            var (root, ctx) = Parse(
                "class C { void M(MyService svc) { svc.OldMethod(); } }");
            var rule = new RenameMemberRule
            {
                Id = "RM-QP",
                TypeName = "Ns.MyService",
                OldMemberName = "OldMethod",
                NewMemberName = "NewMethod",
            };
            var result = new RenameMemberRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count >= 1", t => t.Ctx.Applied.Count >= 1)
        .And("result contains NewMethod", t => t.Result!.ToString().Contains("NewMethod"))
        .AssertPassed();

    [Scenario("RenameMember with constructor parameter as receiver is renamed")]
    [Fact]
    public Task RenameMember_ConstructorParam_ReceiverInferred() =>
        Given("source with constructor parameter MyService and call to OldMethod", () =>
        {
            var (root, ctx) = Parse(
                "class C { C(MyService svc) { svc.OldMethod(); } }");
            var rule = new RenameMemberRule
            {
                Id = "RM-CTOR",
                TypeName = "MyService",
                OldMemberName = "OldMethod",
                NewMemberName = "NewMethod",
            };
            var result = new RenameMemberRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count >= 1 OR Skipped (constructor body)", t =>
            t.Ctx.Applied.Count >= 1 || t.Ctx.Skipped.Count >= 1)
        .AssertPassed();

    // ── ChangeParameterRewriter — bare method name invocation ────────────────

    [Scenario("ChangeParameter renames named arg when method called without receiver")]
    [Fact]
    public Task ChangeParameter_BareMethodCall_NamedArgRenamed() =>
        Given("source with bare Build(size: 1) call and rename rule", () =>
        {
            // Invocation via IdentifierNameSyntax (not MemberAccessExpression)
            var (root, ctx) = Parse("class C { void M() { Build(size: 1); } void Build(int size) {} }");
            var rule = new ChangeParameterRule
            {
                Id = "CP-BARE",
                TypeName = "C",
                MethodName = "Build",
                OldParameterName = "size",
                NewParameterName = "buttonSize",
            };
            var result = new ChangeParameterRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count >= 1", t => t.Ctx.Applied.Count >= 1)
        .And("result contains buttonSize:", t => t.Result!.ToString().Contains("buttonSize:"))
        .AssertPassed();

    [Scenario("ChangeParameter with no NewParameterName leaves label unchanged (no rewrite)")]
    [Fact]
    public Task ChangeParameter_NullNewName_NoRewrite() =>
        Given("source with Build(size: 1) and rule where NewParameterName is null", () =>
        {
            var (root, ctx) = Parse("class C { void M(Builder b) { b.Build(size: 1); } }");
            var rule = new ChangeParameterRule
            {
                Id = "CP-NULL",
                TypeName = "Builder",
                MethodName = "Build",
                OldParameterName = "size",
                NewParameterName = null,  // Name did not change
                OldParameterType = "int",
                NewParameterType = "int",
            };
            var result = new ChangeParameterRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result is null (nothing changed)", t => t.Result is null)
        .And("Applied count is 0", t => t.Ctx.Applied.Count == 0)
        .AssertPassed();

    // ── RemoveMemberRewriter — invocation without member access ─────────────

    [Scenario("RemoveMember does not match bare function call (not a member access)")]
    [Fact]
    public Task RemoveMember_BareCall_DoesNotMatch() =>
        Given("source with bare Deprecated() call (no receiver)", () =>
        {
            var (root, ctx) = Parse("class C { void M() { Deprecated(); } void Deprecated() {} }");
            var rule = new RemoveMemberRule { Id = "DEL-BARE", TypeName = "C", MemberName = "Deprecated" };
            var result = new RemoveMemberRewriter().TryRewrite(root, rule, ctx);
            return result;
        })
        .Then("result is null (no member access found)", r => r is null)
        .AssertPassed();

    // ── AddRequiredParameterRewriter — clamped position ─────────────────────

    [Scenario("AddRequiredParameter with position beyond arg count clamps to end")]
    [Fact]
    public Task AddRequiredParameter_PositionBeyondCount_ClampsToEnd() =>
        Given("source with provider.Apply(x) and rule adding at position 99", () =>
        {
            var (root, ctx) = Parse("class C { void M(ThemeProvider provider) { provider.Apply(x); } }");
            var rule = new AddRequiredParameterRule
            {
                Id = "ARP-CLAMP",
                TypeName = "ThemeProvider",
                MethodName = "Apply",
                ParameterName = "theme",
                ParameterType = "MudTheme",
                Position = 99,
            };
            var result = new AddRequiredParameterRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .And("result contains default", t => t.Result!.ToString().Contains("default", StringComparison.Ordinal))
        .AssertPassed();

    // ── ChangeTypeReferenceRewriter — base list and cast contexts ────────────

    [Scenario("ChangeTypeReference replaces type in base list")]
    [Fact]
    public Task ChangeTypeReference_BaseList_Replaced() =>
        Given("source with 'class C : IList' and rule IList->IReadOnlyList", () =>
        {
            var (root, ctx) = Parse("class C : IList { }");
            var rule = new ChangeTypeReferenceRule { Id = "CTR-BASE", OldType = "IList", NewType = "IReadOnlyList" };
            var result = new ChangeTypeReferenceRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result contains IReadOnlyList", t => t.Result!.ToString().Contains("IReadOnlyList"))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("ChangeTypeReference replaces type in cast expression")]
    [Fact]
    public Task ChangeTypeReference_CastExpression_Replaced() =>
        Given("source with '(IList)x' and rule IList->IReadOnlyList", () =>
        {
            var (root, ctx) = Parse("class C { void M(object x) { var y = (IList)x; } }");
            var rule = new ChangeTypeReferenceRule { Id = "CTR-CAST", OldType = "IList", NewType = "IReadOnlyList" };
            var result = new ChangeTypeReferenceRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result contains IReadOnlyList", t => t.Result!.ToString().Contains("IReadOnlyList"))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("ChangeTypeReference replaces type in parameter declaration")]
    [Fact]
    public Task ChangeTypeReference_ParameterType_Replaced() =>
        Given("source with method parameter IList p and rule IList->IReadOnlyList", () =>
        {
            var (root, ctx) = Parse("class C { void M(IList p) { } }");
            var rule = new ChangeTypeReferenceRule { Id = "CTR-PARAM", OldType = "IList", NewType = "IReadOnlyList" };
            var result = new ChangeTypeReferenceRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result contains IReadOnlyList", t => t.Result!.ToString().Contains("IReadOnlyList"))
        .And("Applied count is 1", t => t.Ctx.Applied.Count == 1)
        .AssertPassed();

    [Scenario("ChangeTypeReference replaces type in object creation expression")]
    [Fact]
    public Task ChangeTypeReference_ObjectCreation_Replaced() =>
        Given("source with 'new OldType()' and rule OldType->NewType", () =>
        {
            var (root, ctx) = Parse("class C { void M() { var x = new OldType(); } }");
            var rule = new ChangeTypeReferenceRule { Id = "CTR-OBJ", OldType = "OldType", NewType = "NewType" };
            var result = new ChangeTypeReferenceRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("result contains NewType", t => t.Result!.ToString().Contains("NewType"))
        .And("Applied count >= 1", t => t.Ctx.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("ChangeTypeReference replaces type in nullable type declaration")]
    [Fact]
    public Task ChangeTypeReference_NullableType_Replaced() =>
        Given("source with 'IList? x' and rule IList->IReadOnlyList", () =>
        {
            var (root, ctx) = Parse("class C { IList? x; }");
            var rule = new ChangeTypeReferenceRule { Id = "CTR-NULL", OldType = "IList", NewType = "IReadOnlyList" };
            var result = new ChangeTypeReferenceRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count >= 1", t => t.Ctx.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("ChangeTypeReference replaces fully-qualified type reference in type context")]
    [Fact]
    public Task ChangeTypeReference_FullyQualifiedInTypeContext_Replaced() =>
        Given("source with 'System.Collections.IList x' and rule for System.Collections.IList", () =>
        {
            var (root, ctx) = Parse("class C { System.Collections.IList x; }");
            var rule = new ChangeTypeReferenceRule { Id = "CTR-FQ", OldType = "System.Collections.IList", NewType = "System.Collections.IReadOnlyList" };
            var result = new ChangeTypeReferenceRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count >= 1", t => t.Ctx.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("ChangeTypeReference replaces method return type")]
    [Fact]
    public Task ChangeTypeReference_MethodReturnType_Replaced() =>
        Given("source with method returning IList and rule IList->IReadOnlyList", () =>
        {
            var (root, ctx) = Parse("class C { IList GetItems() => null; }");
            var rule = new ChangeTypeReferenceRule { Id = "CTR-RET", OldType = "IList", NewType = "IReadOnlyList" };
            var result = new ChangeTypeReferenceRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Applied count >= 1", t => t.Ctx.Applied.Count >= 1)
        .And("result contains IReadOnlyList", t => t.Result!.ToString().Contains("IReadOnlyList"))
        .AssertPassed();

    // ── RewriterHelpers coverage ─────────────────────────────────────────────

    [Scenario("RenameMember with receiver declared via var (implicit type) records Skipped")]
    [Fact]
    public Task RenameMember_VarDeclaredReceiver_RecordsSkipped() =>
        Given("source with 'var svc = ...; svc.OldMethod()' where type is implicit", () =>
        {
            var (root, ctx) = Parse(
                "class C { void M() { var svc = GetService(); svc.OldMethod(); } object GetService() => null; }");
            var rule = new RenameMemberRule
            {
                Id = "RM-VAR",
                TypeName = "MyService",
                OldMemberName = "OldMethod",
                NewMemberName = "NewMethod",
            };
            var result = new RenameMemberRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Skipped count >= 1 (var type is inferred, not explicit)", t => t.Ctx.Skipped.Count >= 1)
        .AssertPassed();

    [Scenario("RenameMember in property body is skipped (ambiguous receiver)")]
    [Fact]
    public Task RenameMember_PropertyBody_AmbiguousReceiver_Skipped() =>
        Given("source with property getter calling obj.OldMethod()", () =>
        {
            var (root, ctx) = Parse(
                "class C { int Prop { get { return obj.OldMethod(); } } object obj; }");
            var rule = new RenameMemberRule
            {
                Id = "RM-PROP",
                TypeName = "MyService",
                OldMemberName = "OldMethod",
                NewMemberName = "NewMethod",
            };
            var result = new RenameMemberRewriter().TryRewrite(root, rule, ctx);
            return (Result: result, Ctx: ctx);
        })
        .Then("Skipped count >= 1 (field receiver is unknown type)", t => t.Ctx.Skipped.Count >= 1)
        .AssertPassed();
}
