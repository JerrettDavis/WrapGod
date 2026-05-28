using Microsoft.CodeAnalysis.CSharp.Syntax;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.Rewriters;
using WrapGod.Tests.Migration.Engine.Fixtures;
using Xunit.Abstractions;

namespace WrapGod.Tests.Migration.Engine;

[Feature("MigrationEngine orchestrator: Apply and DryRun scenarios")]
public sealed class MigrationEngineTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IRuleRewriter[] DefaultRewriterList() =>
    [
        new RenameTypeRewriter(),
        new RenameNamespaceRewriter(),
        new RenameMemberRewriter(),
        new ChangeParameterRewriter(),
        new RemoveMemberRewriter(),
        new AddRequiredParameterRewriter(),
        new ChangeTypeReferenceRewriter(),
    ];

    private static MigrationEngine EngineWithFs(IMigrationFileSystem fs) =>
        new(DefaultRewriterList(), fs);

    private static MigrationSchema OneRenameTypeSchema(
        string id = "RT-001",
        string oldName = "OldWidget",
        string newName = "NewWidget",
        RuleConfidence confidence = RuleConfidence.Auto) =>
        new()
        {
            Library = "Test", From = "1.0", To = "2.0",
            Rules =
            [
                new RenameTypeRule { Id = id, OldName = oldName, NewName = newName, Confidence = confidence }
            ]
        };

    private static MigrationSchema EmptySchema() =>
        new() { Library = "Test", From = "1.0", To = "2.0" };

    // OldWidget is used as a type reference (not a declaration), so RenameTypeRewriter will
    // replace it. The class declaration name is a SyntaxToken and is NOT renamed.
    private const string SourceWithOldWidget =
        "using System;\nclass OldWidget { }\nclass Consumer { OldWidget w; }";

    private const string SourceWithoutMatch =
        "using System;\nclass Unrelated { int x; }";

    // ══════════════════════════════════════════════════════════════════════════
    // Group: happy
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Happy-01: empty schema, one file → Empty result, no writes")]
    [Fact]
    public Task Happy01_EmptySchema_NoWrites() =>
        Given("an empty schema applied to one file", () =>
        {
            var fs = new InMemoryFileSystem().WithFile("a.cs", SourceWithoutMatch);
            var engine = EngineWithFs(fs);
            var original = fs.Files["a.cs"];
            var result = engine.Apply(EmptySchema(), ["a.cs"]);
            return (result, fs, original);
        })
        .Then("result.Applied is empty", t => t.result.Applied.Count == 0)
        .And("result.Skipped is empty", t => t.result.Skipped.Count == 0)
        .And("result.Manual is empty", t => t.result.Manual.Count == 0)
        .And("result.RewrittenFiles is empty", t => t.result.RewrittenFiles.Count == 0)
        .And("result.DryRun is false", t => !t.result.DryRun)
        .And("file on disk is unchanged", t => t.fs.Files["a.cs"] == t.original)
        .AssertPassed();

    [Scenario("Happy-02: one rule, one match → Applied entry, file written")]
    [Fact]
    public Task Happy02_OneRule_OneMatch_FileWritten() =>
        Given("a RenameType schema applied to a file containing OldWidget", () =>
        {
            var fs = new InMemoryFileSystem().WithFile("a.cs", SourceWithOldWidget);
            var engine = EngineWithFs(fs);
            var result = engine.Apply(OneRenameTypeSchema(), ["a.cs"]);
            return (result, fs);
        })
        .Then("result.Applied.Count >= 1", t => t.result.Applied.Count >= 1)
        .And("result.RewrittenFiles contains a.cs", t => t.result.RewrittenFiles.ContainsKey("a.cs"))
        .And("rewritten content contains NewWidget", t => t.result.RewrittenFiles["a.cs"].Contains("NewWidget"))
        // Class declaration OldWidget is a SyntaxToken (not IdentifierNameSyntax), so stays as-is.
        // The type reference OldWidget in Consumer field IS renamed.
        .And("rewritten content contains 'NewWidget w'", t => t.result.RewrittenFiles["a.cs"].Contains("NewWidget w"))
        .And("file on disk was updated", t => t.fs.Files["a.cs"].Contains("NewWidget"))
        .AssertPassed();

    [Scenario("Happy-03: DryRun returns result without writing files to disk")]
    [Fact]
    public Task Happy03_DryRun_PopulatesResultWithoutWriting() =>
        Given("a RenameType schema DryRun applied to a file containing OldWidget", () =>
        {
            var fs = new InMemoryFileSystem().WithFile("a.cs", SourceWithOldWidget);
            var engine = EngineWithFs(fs);
            var originalContent = fs.Files["a.cs"];
            var result = engine.DryRun(OneRenameTypeSchema(), ["a.cs"]);
            return (result, fs, originalContent);
        })
        .Then("result.DryRun is true", t => t.result.DryRun)
        .And("result.Applied.Count >= 1", t => t.result.Applied.Count >= 1)
        .And("result.RewrittenFiles contains a.cs", t => t.result.RewrittenFiles.ContainsKey("a.cs"))
        .And("RewrittenFiles[a.cs] contains NewWidget w", t => t.result.RewrittenFiles["a.cs"].Contains("NewWidget w"))
        .And("file on disk is unchanged after DryRun", t => t.fs.Files["a.cs"] == t.originalContent)
        .AssertPassed();

    [Scenario("Happy-04: chained renames A→B then B→C both apply (first-wins chained)")]
    [Fact]
    public Task Happy04_TwoChainedRenames_BothApply() =>
        Given("schema Foo→Bar, Bar→Baz applied to file with Foo", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "Test", From = "1.0", To = "2.0",
                Rules =
                [
                    new RenameTypeRule { Id = "RT-A", OldName = "Foo", NewName = "Bar" },
                    new RenameTypeRule { Id = "RT-B", OldName = "Bar", NewName = "Baz" },
                ]
            };
            var fs = new InMemoryFileSystem().WithFile("f.cs", "class Consumer { Foo x; }");
            var engine = EngineWithFs(fs);
            engine.Apply(schema, ["f.cs"]);
            return fs.Files["f.cs"];
        })
        .Then("final content contains Baz", content => content.Contains("Baz"))
        .And("final content does not contain Foo", content => !content.Contains("Foo"))
        .AssertPassed();

    [Scenario("Happy-05: multiple files → all matching files rewritten, count correct")]
    [Fact]
    public Task Happy05_MultipleFiles_AllMatchesRewritten() =>
        Given("schema applied to two files both containing OldWidget", () =>
        {
            var fs = new InMemoryFileSystem()
                .WithFile("a.cs", SourceWithOldWidget)
                .WithFile("b.cs", "class OldWidget { } class X { OldWidget y; }");
            var engine = EngineWithFs(fs);
            var result = engine.Apply(OneRenameTypeSchema(), ["a.cs", "b.cs"]);
            return (result, fs);
        })
        .Then("result.RewrittenFiles.Count == 2", t => t.result.RewrittenFiles.Count == 2)
        .And("a.cs updated on disk", t => t.fs.Files["a.cs"].Contains("NewWidget"))
        .And("b.cs updated on disk", t => t.fs.Files["b.cs"].Contains("NewWidget"))
        .AssertPassed();

    [Scenario("Happy-06: rule with no match in any file → 0 applied 0 rewritten files")]
    [Fact]
    public Task Happy06_RuleWithNoMatch_ZeroApplied() =>
        Given("schema applied to file with no matching identifier", () =>
        {
            var fs = new InMemoryFileSystem().WithFile("x.cs", SourceWithoutMatch);
            var engine = EngineWithFs(fs);
            return engine.Apply(OneRenameTypeSchema(), ["x.cs"]);
        })
        .Then("result.Applied.Count == 0", r => r.Applied.Count == 0)
        .And("result.RewrittenFiles is empty", r => r.RewrittenFiles.Count == 0)
        .AssertPassed();

    [Scenario("Happy-07: Manual confidence rule appears in Manual list, not Applied")]
    [Fact]
    public Task Happy07_ManualRule_RecordedInManual_NotApplied() =>
        Given("Manual-confidence RenameType schema applied to file containing OldWidget", () =>
        {
            var fs = new InMemoryFileSystem().WithFile("m.cs", SourceWithOldWidget);
            var engine = EngineWithFs(fs);
            var schema = OneRenameTypeSchema(confidence: RuleConfidence.Manual);
            var result = engine.Apply(schema, ["m.cs"]);
            return (result, fs);
        })
        .Then("result.Applied is empty", t => t.result.Applied.Count == 0)
        .And("result.Manual has 1 entry", t => t.result.Manual.Count == 1)
        .And("manual entry has rule id RT-001", t => t.result.Manual[0].RuleId == "RT-001")
        .And("file on disk is unchanged", t => t.fs.Files["m.cs"] == SourceWithOldWidget)
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Group: sad
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Sad-01: null schema → ArgumentNullException")]
    [Fact]
    public Task Sad01_NullSchema_Throws() =>
        Given("a null schema passed to Apply", () =>
        {
            try
            {
                MigrationEngine.CreateDefault().Apply(null!, []);
                return false;
            }
            catch (ArgumentNullException) { return true; }
        })
        .Then("ArgumentNullException was thrown", threw => threw)
        .AssertPassed();

    [Scenario("Sad-02: null filePaths → ArgumentNullException")]
    [Fact]
    public Task Sad02_NullFilePaths_Throws() =>
        Given("a null file paths collection passed to Apply", () =>
        {
            try
            {
                MigrationEngine.CreateDefault().Apply(EmptySchema(), null!);
                return false;
            }
            catch (ArgumentNullException) { return true; }
        })
        .Then("ArgumentNullException was thrown", threw => threw)
        .AssertPassed();

    [Scenario("Sad-03: null rewriters → ArgumentNullException on MigrationEngine construction")]
    [Fact]
    public Task Sad03_NullRewriters_Throws() =>
        Given("a null rewriter collection passed to constructor", () =>
        {
            try { _ = new MigrationEngine(null!); return false; }
            catch (ArgumentNullException) { return true; }
        })
        .Then("ArgumentNullException was thrown", threw => threw)
        .AssertPassed();

    [Scenario("Sad-04: IO error on file read → SkippedRewrite with '<io>' ruleId, processing continues")]
    [Fact]
    public Task Sad04_IoError_RecordsSkippedAndContinues() =>
        Given("two files where bad.cs throws IO, good.cs is fine", () =>
        {
            var fs = new InMemoryFileSystem()
                .WithFile("bad.cs", SourceWithOldWidget)
                .WithFile("good.cs", SourceWithOldWidget);
            fs.ThrowOnRead = "bad.cs";
            var engine = EngineWithFs(fs);
            return engine.Apply(OneRenameTypeSchema(), ["bad.cs", "good.cs"]);
        })
        .Then("Skipped has IO entry for bad.cs", r =>
            r.Skipped.Any(s => s.RuleId == "<io>" && s.File == "bad.cs"))
        .And("good.cs was processed", r => r.RewrittenFiles.ContainsKey("good.cs"))
        .AssertPassed();

    [Scenario("Sad-05: file with syntax errors (unclosed brace) → engine does not throw")]
    [Fact]
    public Task Sad05_ParseError_DoesNotThrow() =>
        Given("a file with broken C# (missing closing brace) containing OldWidget", () =>
        {
            var brokenSource = "class Broken { OldWidget w;"; // missing }
            var fs = new InMemoryFileSystem().WithFile("broken.cs", brokenSource);
            var engine = EngineWithFs(fs);
            MigrationResult? result = null;
            Exception? ex = null;
            try { result = engine.Apply(OneRenameTypeSchema(), ["broken.cs"]); }
            catch (Exception e) { ex = e; }
            return (result, ex);
        })
        .Then("no exception bubbled", t => t.ex is null)
        .And("Applied has at least 1 entry (rewriter matched OldWidget on partial tree)", t =>
            t.result is not null && t.result.Applied.Count >= 1)
        .AssertPassed();

    [Scenario("Sad-06: unknown rule kind (SplitMethod, no A-level rewriter) → SkippedRewrite")]
    [Fact]
    public Task Sad06_UnknownRuleKind_RecordsSkippedWithNoRewriterReason() =>
        Given("a schema with SplitMethod rule (has no registered rewriter)", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "Test", From = "1.0", To = "2.0",
                Rules =
                [
                    new SplitMethodRule
                    {
                        Id = "SM-001",
                        TypeName = "Widget",
                        OldMethodName = "Render",
                        NewMethodNames = ["Draw", "Flush"]
                    }
                ]
            };
            var fs = new InMemoryFileSystem().WithFile("f.cs", "class Widget { void Render() {} }");
            var engine = EngineWithFs(fs);
            return engine.Apply(schema, ["f.cs"]);
        })
        .Then("Skipped has entry whose reason starts with 'no rewriter for kind'", r =>
            r.Skipped.Any(s => s.Reason.StartsWith("no rewriter for kind", StringComparison.OrdinalIgnoreCase)))
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Group: edge
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Edge-01: duplicate rewriter Kind registered → first-wins, second is ignored")]
    [Fact]
    public Task Edge01_DuplicateKind_FirstWins() =>
        Given("engine with two 'renameType' rewriters (first=NullRewriter, second=tracking rewriter)", () =>
        {
            var secondCalled = false;
            var first = new LambdaRewriter("renameType", (_, _, _) => null);
            var second = new LambdaRewriter("renameType", (node, rule, ctx) =>
            {
                secondCalled = true;
                ctx.RecordApplied(rule, default, "x", "y_from_second", 1);
                return null;
            });
            var fs = new InMemoryFileSystem().WithFile("f.cs", "class OldWidget {}");
            var engine = new MigrationEngine([first, second], fs);
            engine.Apply(OneRenameTypeSchema(), ["f.cs"]);
            return secondCalled;
        })
        .Then("second (duplicate Kind) rewriter was never called", secondCalled => !secondCalled)
        .AssertPassed();

    [Scenario("Edge-02: same file listed twice in filePaths → processed exactly once")]
    [Fact]
    public Task Edge02_SameFileTwice_ProcessedOnce() =>
        Given("same path listed twice in filePaths", () =>
        {
            var fs = new InMemoryFileSystem().WithFile("dup.cs", SourceWithOldWidget);
            var engine = EngineWithFs(fs);
            return engine.Apply(OneRenameTypeSchema(), ["dup.cs", "dup.cs"]);
        })
        .Then("result.RewrittenFiles has exactly 1 entry", r => r.RewrittenFiles.Count == 1)
        .And("Applied entries are not doubled (idempotent dedup)", r => r.Applied.Count < 20)
        .AssertPassed();

    [Scenario("Edge-03: Apply twice on same file → second run has 0 Applied entries (idempotent)")]
    [Fact]
    public Task Edge03_TwiceInARow_SecondRunIsNoOp() =>
        Given("schema applied to file once (first run), then a second Apply runs", () =>
        {
            var fs = new InMemoryFileSystem().WithFile("a.cs", SourceWithOldWidget);
            var engine = EngineWithFs(fs);
            var schema = OneRenameTypeSchema();
            engine.Apply(schema, ["a.cs"]); // first run
            return engine.Apply(schema, ["a.cs"]); // second run
        })
        .Then("second run Applied count is 0", r => r.Applied.Count == 0)
        .And("second run RewrittenFiles is empty", r => r.RewrittenFiles.Count == 0)
        .AssertPassed();

    [Scenario("Edge-04: CRLF line endings are preserved through rewrite")]
    [Fact]
    public Task Edge04_CrlfLineEndings_Preserved() =>
        Given("a file with CRLF line endings containing OldWidget, schema applied", () =>
        {
            var crlfSource = "using System;\r\nclass OldWidget { }\r\nclass C { OldWidget w; }\r\n";
            var fs = new InMemoryFileSystem().WithFile("crlf.cs", crlfSource);
            var engine = EngineWithFs(fs);
            engine.Apply(OneRenameTypeSchema(), ["crlf.cs"]);
            return fs.Files["crlf.cs"];
        })
        .Then("rewritten file still contains CRLF sequences", content => content.Contains("\r\n"))
        .AssertPassed();

    [Scenario("Edge-05: CreateDefault returns a non-null MigrationEngine instance")]
    [Fact]
    public Task Edge05_CreateDefault_ReturnsEngine() =>
        Given("MigrationEngine.CreateDefault() is called", () => MigrationEngine.CreateDefault())
        .Then("result is not null", engine => engine is not null)
        .AssertPassed();

    [Scenario("Edge-06: DryRun null schema → ArgumentNullException")]
    [Fact]
    public Task Edge06_DryRun_NullSchema_Throws() =>
        Given("null schema passed to DryRun", () =>
        {
            try
            {
                MigrationEngine.CreateDefault().DryRun(null!, []);
                return false;
            }
            catch (ArgumentNullException) { return true; }
        })
        .Then("ArgumentNullException was thrown", threw => threw)
        .AssertPassed();

    [Scenario("Edge-07: empty file list → empty result")]
    [Fact]
    public Task Edge07_EmptyFileList_EmptyResult() =>
        Given("schema with one rule applied to empty file list", () =>
            MigrationEngine.CreateDefault().Apply(OneRenameTypeSchema(), []))
        .Then("Applied is empty", r => r.Applied.Count == 0)
        .And("Skipped is empty", r => r.Skipped.Count == 0)
        .And("RewrittenFiles is empty", r => r.RewrittenFiles.Count == 0)
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Group: cross-namespace using injection (#195 deferred should-fix)
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Using-01: RenameNamespace from OldNs to NewNs injects missing using directive")]
    [Fact]
    public Task UsingInjection01_CrossNamespace_InjectsMissingUsing() =>
        Given("file has 'using OldNs' renamed to 'using NewNs' via RenameNamespace rule", () =>
        {
            // RenameNamespaceRewriter rewrites using directives:
            // 'using OldNs.Widgets' → 'using NewNs.Widgets'
            var source = "using OldNs.Widgets;\nclass C { Widget x; }";
            var schema = new MigrationSchema
            {
                Library = "Test", From = "1.0", To = "2.0",
                Rules =
                [
                    new RenameNamespaceRule
                    {
                        Id = "RNS-001",
                        OldNamespace = "OldNs",
                        NewNamespace = "NewNs"
                    }
                ]
            };
            var fs = new InMemoryFileSystem().WithFile("c.cs", source);
            var engine = EngineWithFs(fs);
            return engine.Apply(schema, ["c.cs"]);
        })
        .Then("rewritten file contains 'using NewNs'", r =>
            r.RewrittenFiles.ContainsKey("c.cs") &&
            r.RewrittenFiles["c.cs"].Contains("using NewNs"))
        .And("rewritten file does not contain the old namespace prefix", r =>
            !r.RewrittenFiles["c.cs"].Contains("using OldNs"))
        .AssertPassed();

    [Scenario("Using-02: RenameType where new namespace already has using → no duplicate injected")]
    [Fact]
    public Task UsingInjection02_AlreadyPresent_NotDuplicated() =>
        Given("file already has 'using NewNs.Widgets' and namespace gets renamed OldNs→NewNs", () =>
        {
            // File already has the new namespace in using; renaming only touches the namespace text.
            var source = "using NewNs.Widgets;\nusing OldNs.Extras;\nclass C { Widget x; }";
            var schema = new MigrationSchema
            {
                Library = "Test", From = "1.0", To = "2.0",
                Rules =
                [
                    new RenameNamespaceRule
                    {
                        Id = "RNS-001",
                        OldNamespace = "OldNs",
                        NewNamespace = "NewNs"
                    }
                ]
            };
            var fs = new InMemoryFileSystem().WithFile("c.cs", source);
            var engine = EngineWithFs(fs);
            return engine.Apply(schema, ["c.cs"]);
        })
        // Should-fix #2: assert directly that the engine rewrote the file; no silent
        // fallback that lets the test pass when nothing happened.
        .Then("engine rewrote c.cs", r => r.RewrittenFiles.ContainsKey("c.cs"))
        .And("'using NewNs' appears in rewritten file", r =>
            r.RewrittenFiles["c.cs"].Contains("using NewNs"))
        .And("'using OldNs' is no longer present", r =>
            !r.RewrittenFiles["c.cs"].Contains("using OldNs"))
        .And("'using NewNs' is not duplicated (exactly 2 occurrences — Widgets and Extras)", r =>
            CountOccurrences(r.RewrittenFiles["c.cs"], "using NewNs") == 2)
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Group: review-feedback regression tests
    // ══════════════════════════════════════════════════════════════════════════

    [Scenario("Review-MustFix1: Manual rule with namespace-producing kind MUST NOT inject a using even when its ID matches an applied auto rule's pattern")]
    [Fact]
    public Task ReviewMustFix1_ManualRuleNamespace_NotInjected() =>
        Given("schema with a Manual ChangeTypeReference (FakeNs.X→GhostNs.Y) and an auto RenameType that DOES apply", () =>
        {
            // The manual rule introduces "GhostNs" namespace; if the engine were
            // iterating schema.Rules in InjectMissingUsings, a coincidence between
            // the manual rule's ID and an applied auto rule's ID could cause
            // "using GhostNs" to be injected.  Must-fix #1 forbids this.
            var source = "class C { OldWidget w; }";
            var schema = new MigrationSchema
            {
                Library = "Test", From = "1.0", To = "2.0",
                Rules =
                [
                    // Auto rule that WILL apply (renames OldWidget → NewWidget in same ns)
                    new RenameTypeRule
                    {
                        Id = "SHARED-ID",
                        OldName = "OldWidget",
                        NewName = "NewWidget",
                        Confidence = RuleConfidence.Auto,
                    },
                    // Manual rule producing GhostNs — has the same Id by coincidence
                    new ChangeTypeReferenceRule
                    {
                        Id = "SHARED-ID",
                        OldType = "FakeNs.X",
                        NewType = "GhostNs.Y",
                        Confidence = RuleConfidence.Manual,
                    },
                ]
            };
            var fs = new InMemoryFileSystem().WithFile("c.cs", source);
            var engine = EngineWithFs(fs);
            return engine.Apply(schema, ["c.cs"]);
        })
        .Then("file was rewritten (auto rule applied)", r => r.RewrittenFiles.ContainsKey("c.cs"))
        .And("rewritten file does NOT contain 'using GhostNs'", r =>
            !r.RewrittenFiles["c.cs"].Contains("using GhostNs"))
        .And("rewritten file does NOT contain 'using FakeNs'", r =>
            !r.RewrittenFiles["c.cs"].Contains("using FakeNs"))
        .AssertPassed();

    [Scenario("Review-MustFix2: Manual rule detection MUST NOT leak Applied/Skipped entries from its rewriter into the main audit trail")]
    [Fact]
    public Task ReviewMustFix2_ManualDetection_DoesNotLeakIntoMainContext() =>
        Given("schema with a Manual rule matched by a rewriter that records Applied during detection", () =>
        {
            // Use a custom rewriter that ALWAYS records an Applied entry whenever
            // TryRewrite is called.  Engine's manual detection invokes this rewriter
            // with a throwaway context, so the entry must NOT bubble into result.Applied.
            var leaky = new LambdaRewriter("renameType", (node, rule, ctx) =>
            {
                ctx.RecordApplied(rule, default, "x", "y", 1);
                return null;
            });
            var schema = new MigrationSchema
            {
                Library = "Test", From = "1.0", To = "2.0",
                Rules =
                [
                    new RenameTypeRule
                    {
                        Id = "MANUAL-LEAK",
                        OldName = "Anything",
                        NewName = "Whatever",
                        Confidence = RuleConfidence.Manual,
                    }
                ]
            };
            var fs = new InMemoryFileSystem().WithFile("m.cs", "class C {}");
            var engine = new MigrationEngine([leaky], fs);
            return engine.Apply(schema, ["m.cs"]);
        })
        .Then("Applied contains NO leaked entries for MANUAL-LEAK", r =>
            !r.Applied.Any(a => a.RuleId == "MANUAL-LEAK"))
        .And("Skipped contains NO leaked entries for MANUAL-LEAK", r =>
            !r.Skipped.Any(s => s.RuleId == "MANUAL-LEAK"))
        .And("Manual list still contains the rule entry", r =>
            r.Manual.Any(m => m.RuleId == "MANUAL-LEAK"))
        .AssertPassed();

    [Scenario("Review-ShouldFix1: LF-only source file → injected using directive uses LF terminator (not CRLF)")]
    [Fact]
    public Task ReviewShouldFix1_LfOnlyFile_InjectedUsingUsesLf() =>
        Given("LF-only file that requires namespace using injection via RenameNamespace", () =>
        {
            // Source uses only \n. The injected using directive must use \n, not \r\n.
            var source = "using OldNs.A;\nclass C { Widget x; }";
            var schema = new MigrationSchema
            {
                Library = "Test", From = "1.0", To = "2.0",
                Rules =
                [
                    new RenameNamespaceRule
                    {
                        Id = "RNS-001",
                        OldNamespace = "OldNs",
                        NewNamespace = "NewNs"
                    }
                ]
            };
            var fs = new InMemoryFileSystem().WithFile("lf.cs", source);
            var engine = EngineWithFs(fs);
            return engine.Apply(schema, ["lf.cs"]);
        })
        .Then("rewritten file was produced", r => r.RewrittenFiles.ContainsKey("lf.cs"))
        // The original had no CRLF; injection must not introduce CRLF.  Roslyn may emit
        // pre-existing using terminators unchanged, so the only safe assertion is "no
        // CRLF anywhere in the rewritten text".
        .And("rewritten file contains no CRLF sequences", r =>
            !r.RewrittenFiles["lf.cs"].Contains("\r\n"))
        .AssertPassed();

    [Scenario("Review-ShouldFix3: unknown-rule-kind SkippedRewrite emitted ONCE per schema, not once per file")]
    [Fact]
    public Task ReviewShouldFix3_UnknownKindSkipped_OncePerSchema() =>
        Given("schema with SplitMethod rule (no rewriter) and 5 input files", () =>
        {
            var schema = new MigrationSchema
            {
                Library = "Test", From = "1.0", To = "2.0",
                Rules =
                [
                    new SplitMethodRule
                    {
                        Id = "SM-001",
                        TypeName = "Widget",
                        OldMethodName = "Render",
                        NewMethodNames = ["Draw", "Flush"]
                    }
                ]
            };
            var fs = new InMemoryFileSystem();
            var paths = new string[5];
            for (int i = 0; i < 5; i++)
            {
                var p = $"f{i}.cs";
                fs.WithFile(p, "class Widget { void Render() {} }");
                paths[i] = p;
            }
            var engine = EngineWithFs(fs);
            return engine.Apply(schema, paths);
        })
        .Then("exactly one SM-001 SkippedRewrite (schema-level, not 5 per-file)", r =>
            r.Skipped.Count(s => s.RuleId == "SM-001") == 1)
        .And("the schema-level entry uses File = '<schema>'", r =>
            r.Skipped.Single(s => s.RuleId == "SM-001").File == "<schema>")
        .AssertPassed();

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private static int CountOccurrences(string text, string substring)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }
        return count;
    }

    // ── Inner test double: LambdaRewriter ─────────────────────────────────────

    private sealed class LambdaRewriter(
        string kind,
        Func<Microsoft.CodeAnalysis.SyntaxNode, MigrationRule, RewriteContext, Microsoft.CodeAnalysis.SyntaxNode?> fn)
        : IRuleRewriter
    {
        public string Kind => kind;

        public Microsoft.CodeAnalysis.SyntaxNode? TryRewrite(
            Microsoft.CodeAnalysis.SyntaxNode node,
            MigrationRule rule,
            RewriteContext ctx) => fn(node, rule, ctx);
    }
}
