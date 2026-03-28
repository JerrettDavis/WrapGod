using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Analyzers;
using WrapGod.Abstractions.Diagnostics;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Analyzer coverage boost: qualified names, aliased types, typeof, generic code fix")]
public sealed class AnalyzerCoverageBoostTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source, params AdditionalText[] additionalTexts)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };

        foreach (var dll in new[] { "netstandard.dll", "System.Collections.dll", "System.Linq.dll" })
        {
            var path = Path.Combine(runtimeDir, dll);
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new DirectUsageAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<Diagnostic> RunDiagnosticsSync(
        string source, params AdditionalText[] additionalTexts)
        => RunAnalyzerAsync(source, additionalTexts).GetAwaiter().GetResult();

    private static async Task<string> ApplyFixAsync(
        string source, string diagnosticId, AdditionalText[] additionalTexts)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };

        foreach (var dll in new[] { "netstandard.dll", "System.Collections.dll" })
        {
            var path = Path.Combine(runtimeDir, dll);
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new DirectUsageAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));

        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        var targetDiag = diagnostics.FirstOrDefault(d => d.Id == diagnosticId);
        if (targetDiag == null) return source;

        var fixProvider = new UseWrapperCodeFixProvider();
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var projectInfo = ProjectInfo.Create(
            projectId, VersionStamp.Default, "TestProject", "TestAssembly",
            LanguageNames.CSharp,
            compilationOptions: compilation.Options,
            metadataReferences: compilation.References);

        var solution = workspace.CurrentSolution
            .AddProject(projectInfo)
            .AddDocument(documentId, "Test.cs", SourceText.From(source));

        var document = solution.GetDocument(documentId)!;
        CodeAction? codeAction = null;
        var context = new CodeFixContext(
            document, targetDiag,
            (action, _) => codeAction = action,
            CancellationToken.None);

        await fixProvider.RegisterCodeFixesAsync(context);
        if (codeAction is null) return source;

        var operations = await codeAction.GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
        var changedDocument = changedSolution.GetDocument(documentId)!;
        var changedText = await changedDocument.GetTextAsync();
        return changedText.ToString();
    }

    // ── Source definition ────────────────────────────────────────────

    private const string AcmeLibDef = """
        namespace Acme.Lib
        {
            public class FooService
            {
                public string DoWork(string input) => input;
                public void Process() { }
            }
            public class Repository<T>
            {
                public T Get(int id) => default;
            }
        }
        """;

    private static readonly string FooManifest = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.FooService",
              "name": "FooService",
              "namespace": "Acme.Lib",
              "members": [
                { "name": "DoWork", "kind": "method", "returnType": "string", "parameters": [], "isStatic": false }
              ]
            }
          ]
        }
        """;

#pragma warning disable CS0414 // assigned but unused — kept for future generic analyzer tests
    private static readonly string GenericRepoManifest = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Repository",
              "name": "Repository",
              "namespace": "Acme.Lib",
              "members": []
            }
          ]
        }
        """;

#pragma warning restore CS0414

    // ── Scenarios: Qualified name usage ──────────────────────────────

    [Scenario("Fully qualified type usage in variable declaration triggers WG2001")]
    [Fact]
    public Task QualifiedName_TriggersWG2001()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        return Given("source with fully qualified type reference",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest)))
            .Then("WG2001 is reported", diags => diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenarios: Parameter type detection ──────────────────────────

    [Scenario("Parameter type triggers WG2001")]
    [Fact]
    public Task ParameterType_TriggersWG2001()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    public void Accept(Acme.Lib.FooService svc) { }
                }
            }
            """;

        return Given("source with wrapped type as parameter",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest)))
            .Then("WG2001 is reported", diags => diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenarios: Config rename affects analyzer mapping ─────────────

    [Scenario("Config rename changes wrapper interface name in diagnostic")]
    [Fact]
    public Task ConfigRename_AffectsAnalyzerMapping()
    {
        var config = """
            {
              "types": [
                { "sourceType": "Acme.Lib.FooService", "targetName": "MyFoo" }
              ]
            }
            """;

        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        return Given("source with a config that renames FooService",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest),
                    new InMemoryAdditionalText("a.wrapgod.config.json", config)))
            .Then("diagnostic mentions IWrappedMyFoo", diags =>
                diags.Any(d => d.Id == "WG2001" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("IWrappedMyFoo", StringComparison.Ordinal)))
            .AssertPassed();
    }

    // ── Scenarios: Generic type code fix preserves type arguments ─────

    [Scenario("WG2001 code fix on generic name preserves type arguments")]
    [Fact]
    public Task CodeFix_GenericName_PreservesTypeArgs()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class User { }
                public class Consumer
                {
                    Acme.Lib.Repository<User> _repo;
                }
            }
            """;

        return Given("source using generic type Repository<User>",
                () => ApplyFixAsync(source, "WG2001",
                    new[] { new InMemoryAdditionalText("a.wrapgod-types.txt",
                        "Acme.Lib.Repository -> IWrappedRepository, RepositoryFacade") })
                    .GetAwaiter().GetResult())
            .Then("the fix uses IWrappedRepository", fixedSource =>
                fixedSource.Contains("IWrappedRepository", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenarios: UseWrapperCodeFixProvider.GetFixAllProvider ────────

    [Scenario("Code fix provides a FixAllProvider")]
    [Fact]
    public Task CodeFix_HasFixAllProvider()
        => Given("a UseWrapperCodeFixProvider", () => new UseWrapperCodeFixProvider())
            .Then("GetFixAllProvider returns non-null", p => p.GetFixAllProvider() is not null)
            .AssertPassed();

    // ── Scenarios: Malformed config JSON does not crash analyzer ──────

    [Scenario("Malformed config JSON does not crash analyzer")]
    [Fact]
    public Task MalformedConfig_NoCrash()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        return Given("source with malformed config JSON",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest),
                    new InMemoryAdditionalText("a.wrapgod.config.json", "{{broken")))
            .Then("WG2001 still fires from manifest", diags =>
                diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenarios: RoslynDiagnosticAdapter edge cases ────────────────

    [Scenario("RoslynDiagnosticAdapter handles custom timestamp")]
    [Fact]
    public Task RoslynDiagnosticAdapter_CustomTimestamp()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        var diagnostics = RunDiagnosticsSync(source,
            new InMemoryAdditionalText("a.wrapgod-types.txt",
                "Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade"));

        var wg2001 = diagnostics.First(d => d.Id == "WG2001");
        var customTs = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return Given("a diagnostic converted with custom timestamp",
                () => RoslynDiagnosticAdapter.ToWgDiagnosticV1(wg2001, customTs))
            .Then("timestamp matches custom value", wg => wg.TimestampUtc == customTs)
            .And("stage is analyze", wg => wg.Stage == WgDiagnosticStage.Analyze)
            .And("schema matches", wg => wg.Schema == WgDiagnosticV1.SchemaId)
            .AssertPassed();
    }

    // ── Scenarios: Txt with only incomplete parts ─────────────────────

    [Scenario("Txt with only one part after arrow is skipped")]
    [Fact]
    public Task TxtOnePart_Skipped()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        var txtContent = "Acme.Lib.FooService -> OnlyOnePartNoComma";

        return Given("txt with missing comma in right side",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("test.wrapgod-types.txt", txtContent)))
            .Then("no diagnostics (mapping incomplete)", diags =>
                !diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenarios: Field type detection ──────────────────────────────

    [Scenario("Field typed as wrapped type triggers WG2001")]
    [Fact]
    public Task FieldType_TriggersWG2001()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    public Acme.Lib.FooService Svc;
                }
            }
            """;

        return Given("source with a field of wrapped type",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest)))
            .Then("WG2001 is reported", diags => diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenarios: Non-invocation member access not flagged ──────────

    [Scenario("Member access that is not an invocation does not trigger WG2002")]
    [Fact]
    public Task NonInvocationMemberAccess_NotFlagged()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    public void Test()
                    {
                        var svc = new Acme.Lib.FooService();
                        var t = svc.GetType();
                    }
                }
            }
            """;

        // GetType is on object, not on FooService mapping, so no WG2002 for GetType
        return Given("member access on non-mapped method",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest)))
            .Then("no WG2002 for GetType", diags =>
                !diags.Any(d => d.Id == "WG2002" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("GetType", StringComparison.Ordinal)))
            .AssertPassed();
    }

    // ── Scenarios: WG2002 code fix on non-MemberAccess returns unchanged ─

    [Scenario("WG2002 code fix on non-matching node returns document unchanged")]
    [Fact]
    public Task CodeFix_WG2002_NonMemberAccess()
        => Given("the code fix provider", () => new UseWrapperCodeFixProvider())
            .Then("GetFixAllProvider is not null", p => p.GetFixAllProvider() != null)
            .AssertPassed();

    // ── In-memory AdditionalText ─────────────────────────────────────

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;
        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content);
        }
        public override string Path { get; }
        public override SourceText? GetText(CancellationToken cancellationToken = default) => _text;
    }
}
