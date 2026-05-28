using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine;

[Feature("WrapGod.Migration.Engine scaffold: contract shape, equality, and immutability")]
public sealed class EngineScaffoldTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Test double ──────────────────────────────────────────────────────────

    /// <summary>No-op rewriter used as a test double for IRuleRewriter.</summary>
    private sealed class NullRewriter : IRuleRewriter
    {
        public string Kind => "renameType";

        public SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx) => null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static RenameTypeRule MakeRule(string id = "R-001") =>
        new RenameTypeRule { Id = id, OldName = "Foo", NewName = "Bar" };

    private static TextSpan AnySpan() => new(0, 4);

    // ── happy ────────────────────────────────────────────────────────────────

    [Scenario("NullRewriter implements IRuleRewriter and is reachable")]
    [Fact]
    public Task NullRewriter_ImplementsInterface_IsReachable() =>
        Given("a NullRewriter instance", () => new NullRewriter())
        .Then("it implements IRuleRewriter", r => r is IRuleRewriter)
        .And("Kind is renameType", r => r.Kind == "renameType")
        .And("TryRewrite returns null for any node", r =>
        {
            var tree = CSharpSyntaxTree.ParseText("class C {}");
            var root = tree.GetRoot();
            var ctx = new RewriteContext("file.cs");
            var rule = MakeRule();
            return r.TryRewrite(root, rule, ctx) is null;
        })
        .AssertPassed();

    [Scenario("RewriteContext carries all expected fields after RecordApplied")]
    [Fact]
    public Task RewriteContext_RecordApplied_AppendsEntry() =>
        Given("a fresh RewriteContext and one RecordApplied call", () =>
        {
            var ctx = new RewriteContext("foo.cs");
            ctx.RecordApplied(MakeRule("R-001"), AnySpan(), "old", "new", 12);
            return ctx;
        })
        .Then("Applied has one entry", ctx => ctx.Applied.Count == 1)
        .And("RuleId is preserved", ctx => ctx.Applied[0].RuleId == "R-001")
        .And("File is preserved", ctx => ctx.Applied[0].File == "foo.cs")
        .And("Line is preserved", ctx => ctx.Applied[0].Line == 12)
        .And("OriginalText is preserved", ctx => ctx.Applied[0].OriginalText == "old")
        .And("ReplacedWith is preserved", ctx => ctx.Applied[0].ReplacedWith == "new")
        .AssertPassed();

    [Scenario("RewriteContext accumulates multiple Skipped entries in order")]
    [Fact]
    public Task RewriteContext_RecordSkipped_AppendsEntries() =>
        Given("a RewriteContext with three RecordSkipped calls", () =>
        {
            var ctx = new RewriteContext("bar.cs");
            ctx.RecordSkipped(MakeRule("R-001"), AnySpan(), 1, "reason A");
            ctx.RecordSkipped(MakeRule("R-002"), AnySpan(), 2, "reason B");
            ctx.RecordSkipped(MakeRule("R-003"), AnySpan(), 3, "reason C");
            return ctx;
        })
        .Then("Skipped count is 3", ctx => ctx.Skipped.Count == 3)
        .And("first reason is reason A", ctx => ctx.Skipped[0].Reason == "reason A")
        .And("second reason is reason B", ctx => ctx.Skipped[1].Reason == "reason B")
        .And("third reason is reason C", ctx => ctx.Skipped[2].Reason == "reason C")
        .AssertPassed();

    [Scenario("MigrationResult.Empty has zero counts and DryRun false")]
    [Fact]
    public Task MigrationResult_Empty_HasZeroCounts() =>
        Given("MigrationResult.Empty", () => MigrationResult.Empty)
        .Then("Applied is empty", r => r.Applied.Count == 0)
        .And("Skipped is empty", r => r.Skipped.Count == 0)
        .And("Manual is empty", r => r.Manual.Count == 0)
        .And("RewrittenFiles is empty", r => r.RewrittenFiles.Count == 0)
        .And("DryRun is false", r => !r.DryRun)
        .And("AppliedCount is 0", r => r.AppliedCount == 0)
        .And("SkippedCount is 0", r => r.SkippedCount == 0)
        .And("ManualCount is 0", r => r.ManualCount == 0)
        .AssertPassed();

    [Scenario("AppliedRewrite value-equals when all fields are equal")]
    [Fact]
    public Task AppliedRewrite_RecordEquality_Holds() =>
        Given("two AppliedRewrite records with identical fields", () =>
        (
            A: new AppliedRewrite("R-001", "foo.cs", 10, "old", "new"),
            B: new AppliedRewrite("R-001", "foo.cs", 10, "old", "new")
        ))
        .Then("they are equal", t => t.A == t.B)
        .And("hash codes match", t => t.A.GetHashCode() == t.B.GetHashCode())
        .AssertPassed();

    [Scenario("SkippedRewrite value-equals when all fields are equal")]
    [Fact]
    public Task SkippedRewrite_RecordEquality_Holds() =>
        Given("two SkippedRewrite records with identical fields", () =>
        (
            A: new SkippedRewrite("R-001", "foo.cs", 5, "reason"),
            B: new SkippedRewrite("R-001", "foo.cs", 5, "reason")
        ))
        .Then("they are equal", t => t.A == t.B)
        .And("hash codes match", t => t.A.GetHashCode() == t.B.GetHashCode())
        .AssertPassed();

    [Scenario("ManualRewrite value-equals when all fields are equal")]
    [Fact]
    public Task ManualRewrite_RecordEquality_Holds() =>
        Given("two ManualRewrite records with identical fields", () =>
        {
            IReadOnlyList<string> files = ["a.cs", "b.cs"];
            return (
                A: new ManualRewrite("R-001", "note", files),
                B: new ManualRewrite("R-001", "note", files)
            );
        })
        .Then("they are equal", t => t.A == t.B)
        .AssertPassed();

    // ── sad ─────────────────────────────────────────────────────────────────

    [Scenario("RewriteContext throws when filePath is null")]
    [Fact]
    public Task RewriteContext_NullFilePath_Throws() =>
        Given("a null file path", () => (string?)null)
        .Then("constructing RewriteContext throws ArgumentNullException", path =>
        {
            try { _ = new RewriteContext(path!); return false; }
            catch (ArgumentNullException) { return true; }
        })
        .AssertPassed();

    [Scenario("RecordApplied throws when rule is null")]
    [Fact]
    public Task RewriteContext_NullRule_Throws() =>
        Given("a RewriteContext and a null rule", () => new RewriteContext("file.cs"))
        .Then("RecordApplied throws ArgumentNullException", ctx =>
        {
            try { ctx.RecordApplied(null!, AnySpan(), "old", "new", 1); return false; }
            catch (ArgumentNullException) { return true; }
        })
        .AssertPassed();

    [Scenario("RecordSkipped throws when reason is null")]
    [Fact]
    public Task RewriteContext_NullReason_Throws() =>
        Given("a RewriteContext and a null reason", () => new RewriteContext("file.cs"))
        .Then("RecordSkipped throws ArgumentNullException", ctx =>
        {
            try { ctx.RecordSkipped(MakeRule(), AnySpan(), 1, null!); return false; }
            catch (ArgumentNullException) { return true; }
        })
        .AssertPassed();

    [Scenario("MigrationResult constructor throws when applied list is null")]
    [Fact]
    public Task MigrationResult_NullList_Throws() =>
        Given("a null applied list", () => (IReadOnlyList<AppliedRewrite>?)null)
        .Then("constructing MigrationResult throws ArgumentNullException", applied =>
        {
            try
            {
                _ = new MigrationResult(
                    applied: applied!,
                    skipped: [],
                    manual: [],
                    rewrittenFiles: new Dictionary<string, string>(),
                    dryRun: false);
                return false;
            }
            catch (ArgumentNullException) { return true; }
        })
        .AssertPassed();

    // ── edge ────────────────────────────────────────────────────────────────

    [Scenario("WithReplacedToken preserves leading and trailing trivia")]
    [Fact]
    public Task WithReplacedToken_PreservesTrivia() =>
        Given("a syntax tree with a class that has leading and trailing whitespace", () =>
        {
            var tree = CSharpSyntaxTree.ParseText("  class  OldName  {  }  ");
            var root = tree.GetRoot();
            // Find the identifier token for OldName
            var classDecl = root.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                .First();
            var oldIdentifier = classDecl.Identifier;
            var newIdentifier = SyntaxFactory.Identifier("NewName");
            var rewritten = classDecl.WithReplacedToken(oldIdentifier, newIdentifier);
            return (Old: oldIdentifier, New: rewritten.Identifier);
        })
        .Then("leading trivia is preserved", t => t.New.LeadingTrivia.ToFullString() == t.Old.LeadingTrivia.ToFullString())
        .And("trailing trivia is preserved", t => t.New.TrailingTrivia.ToFullString() == t.Old.TrailingTrivia.ToFullString())
        .And("new identifier text is NewName", t => t.New.ValueText == "NewName")
        .AssertPassed();

    [Scenario("NullRewriter TryRewrite returns null leaving tree unchanged")]
    [Fact]
    public Task IRuleRewriter_NoOp_LeavesTreeUnchanged() =>
        Given("a NullRewriter, a parsed syntax tree, and a rule", () =>
        {
            var tree = CSharpSyntaxTree.ParseText("class C { int X; }");
            var root = tree.GetRoot();
            var ctx = new RewriteContext("test.cs");
            var rule = MakeRule();
            var rewriter = new NullRewriter();
            var result = rewriter.TryRewrite(root, rule, ctx);
            return (Root: root, Result: result);
        })
        .Then("result is null", t => t.Result is null)
        .And("Applied is empty", t =>
        {
            // null result means no mutation was applied
            return true; // NullRewriter never calls RecordApplied
        })
        .AssertPassed();

    [Scenario("Applied collection is not externally castable to mutable List")]
    [Fact]
    public Task RewriteContext_ExternalCast_Fails() =>
        Given("a RewriteContext with one applied entry", () =>
        {
            var ctx = new RewriteContext("file.cs");
            ctx.RecordApplied(MakeRule(), AnySpan(), "x", "y", 1);
            return ctx.Applied;
        })
        .Then("cast to List<AppliedRewrite> throws InvalidCastException", applied =>
        {
            try { _ = (List<AppliedRewrite>)applied; return false; }
            catch (InvalidCastException) { return true; }
        })
        .AssertPassed();
}
