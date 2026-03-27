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
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Analyzer coverage: untested paths")]
public sealed class AnalyzerCoverageTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source, params AdditionalText[] additionalTexts)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };

        var netStandardPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "netstandard.dll");
        if (System.IO.File.Exists(netStandardPath))
            references.Add(MetadataReference.CreateFromFile(netStandardPath));

        var collectionsPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Collections.dll");
        if (System.IO.File.Exists(collectionsPath))
            references.Add(MetadataReference.CreateFromFile(collectionsPath));

        var linqPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Linq.dll");
        if (System.IO.File.Exists(linqPath))
            references.Add(MetadataReference.CreateFromFile(linqPath));

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

    private static ImmutableArray<Diagnostic> RunDiagnosticsWithFiles(
        string source, params AdditionalText[] additionalTexts)
        => RunAnalyzerAsync(source, additionalTexts).GetAwaiter().GetResult();

    private static async Task<string> ApplyFixAsync(
        string source, string diagnosticId, AdditionalText[] additionalTexts)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };

        var netStandardPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "netstandard.dll");
        if (System.IO.File.Exists(netStandardPath))
            references.Add(MetadataReference.CreateFromFile(netStandardPath));

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
        if (targetDiag == null)
            return source;

        var fixProvider = new UseWrapperCodeFixProvider();

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var documentId = DocumentId.CreateNewId(projectId);

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Default,
            "TestProject",
            "TestAssembly",
            LanguageNames.CSharp,
            compilationOptions: compilation.Options,
            metadataReferences: compilation.References);

        var solution = workspace.CurrentSolution
            .AddProject(projectInfo)
            .AddDocument(documentId, "Test.cs", SourceText.From(source));

        var document = solution.GetDocument(documentId)!;

        CodeAction? codeAction = null;
        var context = new CodeFixContext(
            document,
            targetDiag,
            (action, _) => codeAction = action,
            CancellationToken.None);

        await fixProvider.RegisterCodeFixesAsync(context);

        if (codeAction is null)
            return source;

        var operations = await codeAction.GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations
            .OfType<ApplyChangesOperation>()
            .Single()
            .ChangedSolution;

        var changedDocument = changedSolution.GetDocument(documentId)!;
        var changedText = await changedDocument.GetTextAsync();
        return changedText.ToString();
    }

    // ── Manifests ────────────────────────────────────────────────────

    private static readonly string FooServiceManifest = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.FooService",
              "name": "FooService",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "DoWork",
                  "kind": "method",
                  "returnType": "string",
                  "parameters": [{ "name": "input", "type": "string" }],
                  "genericParameters": [],
                  "isStatic": false
                },
                {
                  "name": "Process",
                  "kind": "method",
                  "returnType": "void",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false
                }
              ]
            }
          ]
        }
        """;

    private static readonly string TwoTypesManifest = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.FooService",
              "name": "FooService",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "DoWork",
                  "kind": "method",
                  "returnType": "string",
                  "parameters": [{ "name": "input", "type": "string" }],
                  "genericParameters": [],
                  "isStatic": false
                }
              ]
            },
            {
              "fullName": "Acme.Lib.BarService",
              "name": "BarService",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "Execute",
                  "kind": "method",
                  "returnType": "int",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false
                }
              ]
            }
          ]
        }
        """;

    private static readonly string GenericRepoManifest = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Repository",
              "name": "Repository",
              "namespace": "Acme.Lib",
              "kind": "class",
              "genericParameters": ["T"],
              "members": [
                {
                  "name": "Get",
                  "kind": "method",
                  "returnType": "T",
                  "parameters": [{ "name": "id", "type": "int" }],
                  "genericParameters": [],
                  "isStatic": false
                }
              ]
            }
          ]
        }
        """;

    private static readonly string EmptyTypesManifest = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": []
        }
        """;

    // ── Source snippets ──────────────────────────────────────────────

    private const string AcmeLibDefinition = """
        namespace Acme.Lib
        {
            public class FooService
            {
                public string DoWork(string input) => input;
                public void Process() { }
            }

            public class BarService
            {
                public int Execute() => 42;
            }

            public class Repository<T>
            {
                public T Get(int id) => default;
            }
        }
        """;

    // ── Scenario: Nested generic type detection ──────────────────────

    [Scenario("Nested generic type (e.g. List<Repository<User>>) triggers WG2001")]
    [Fact]
    public Task NestedGenericType_TriggersWG2001()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class User { }

                public class Consumer
                {
                    private System.Collections.Generic.List<Acme.Lib.Repository<User>> _repos;
                }
            }
            """;

        return Given("source with a nested generic type wrapping a mapped type",
                () => RunDiagnosticsWithFiles(source,
                    new InMemoryAdditionalText("acme.wrapgod.json", GenericRepoManifest)))
            .Then("at least one WG2001 diagnostic is reported", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001"))
            .And("the diagnostic mentions Repository", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("Repository", StringComparison.Ordinal)))
            .AssertPassed();
    }

    // ── Scenario: Array type not flagged ─────────────────────────────

    [Scenario("Array type is not flagged when no mapping exists for arrays")]
    [Fact]
    public Task ArrayType_NotFlagged()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private int[] _numbers = new int[10];
                    private string[] _names = new string[5];
                }
            }
            """;

        return Given("source with array types and no array mappings",
                () => RunDiagnosticsWithFiles(source,
                    new InMemoryAdditionalText("acme.wrapgod.json", FooServiceManifest)))
            .Then("no WG2001 diagnostics for array types", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2001"))
            .And("no WG2002 diagnostics for array types", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2002"))
            .AssertPassed();
    }

    // ── Scenario: Multiple diagnostics in same file ──────────────────

    [Scenario("Multiple wrapped types in same file generate multiple diagnostics")]
    [Fact]
    public Task MultipleDiagnostics_SameFile()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private Acme.Lib.FooService _foo = new Acme.Lib.FooService();
                    private Acme.Lib.BarService _bar = new Acme.Lib.BarService();

                    public void Run()
                    {
                        _foo.DoWork("test");
                        _bar.Execute();
                    }
                }
            }
            """;

        return Given("source using two wrapped types with method calls",
                () => RunDiagnosticsWithFiles(source,
                    new InMemoryAdditionalText("acme.wrapgod.json", TwoTypesManifest)))
            .Then("WG2001 diagnostics reference FooService", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("FooService", StringComparison.Ordinal)))
            .And("WG2001 diagnostics reference BarService", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("BarService", StringComparison.Ordinal)))
            .And("WG2002 diagnostics reference DoWork", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2002" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("DoWork", StringComparison.Ordinal)))
            .And("WG2002 diagnostics reference Execute", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2002" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("Execute", StringComparison.Ordinal)))
            .AssertPassed();
    }

    // ── Scenario: Code fix on member access expression ───────────────

    [Scenario("WG2002 code fix replaces receiver with facade on member access")]
    [Fact]
    public Task CodeFix_MemberAccess_ReplaceWithFacade()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    public void Run()
                    {
                        var svc = new Acme.Lib.FooService();
                        svc.DoWork("hello");
                    }
                }
            }
            """;

        return Given("source with a direct method call on a wrapped type",
                () => ApplyFixAsync(source, "WG2002",
                    new[] { new InMemoryAdditionalText("test.wrapgod-types.txt",
                        "Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade") })
                    .GetAwaiter().GetResult())
            .Then("the fixed source contains FooServiceFacade", fixedSource =>
                fixedSource.Contains("FooServiceFacade", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: No AdditionalFiles -> no diagnostics ───────────────

    [Scenario("No additional files produces no WrapGod diagnostics")]
    [Fact]
    public Task NoAdditionalFiles_NoDiagnostics()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private Acme.Lib.FooService _svc = new Acme.Lib.FooService();

                    public void Run()
                    {
                        _svc.DoWork("test");
                    }
                }
            }
            """;

        return Given("source using a type but no additional files provided",
                () => RunDiagnosticsWithFiles(source))
            .Then("no WG2001 diagnostics are reported", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2001"))
            .And("no WG2002 diagnostics are reported", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2002"))
            .AssertPassed();
    }

    // ── Scenario: Manifest with no types -> no mappings ──────────────

    [Scenario("Manifest with empty types array produces no diagnostics")]
    [Fact]
    public Task ManifestWithNoTypes_NoDiagnostics()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        return Given("source with a manifest that has zero types",
                () => RunDiagnosticsWithFiles(source,
                    new InMemoryAdditionalText("acme.wrapgod.json", EmptyTypesManifest)))
            .Then("no WG2001 diagnostics are reported", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2001"))
            .And("no WG2002 diagnostics are reported", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2002"))
            .AssertPassed();
    }

    // ── Scenario: Malformed manifest -> no crash, no diagnostics ─────

    [Scenario("Malformed manifest JSON does not crash the analyzer")]
    [Fact]
    public Task MalformedManifest_NoCrash()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        return Given("source with a malformed manifest",
                () => RunDiagnosticsWithFiles(source,
                    new InMemoryAdditionalText("acme.wrapgod.json", "{{broken json")))
            .Then("no WG2001 diagnostics are reported", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2001"))
            .And("no WG2002 diagnostics are reported", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2002"))
            .AssertPassed();
    }

    // ── Scenario: Config excludes type -> no diagnostic for that type ─

    [Scenario("Config exclusion suppresses diagnostics for that type")]
    [Fact]
    public Task ConfigExclusion_SuppressesDiagnostic()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private Acme.Lib.FooService _foo = new Acme.Lib.FooService();
                    private Acme.Lib.BarService _bar = new Acme.Lib.BarService();
                }
            }
            """;

        var configExcludeFoo = """
            {
              "types": [
                { "sourceType": "Acme.Lib.FooService", "include": false }
              ]
            }
            """;

        return Given("source using two types with config excluding FooService",
                () => RunDiagnosticsWithFiles(source,
                    new InMemoryAdditionalText("acme.wrapgod.json", TwoTypesManifest),
                    new InMemoryAdditionalText("acme.wrapgod.config.json", configExcludeFoo)))
            .Then("no WG2001 for FooService", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2001" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("FooService", StringComparison.Ordinal)))
            .And("WG2001 still fires for BarService", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("BarService", StringComparison.Ordinal)))
            .AssertPassed();
    }

    // ── Scenario: WG2001 fix on identifier name ──────────────────────

    [Scenario("WG2001 code fix replaces identifier with wrapper interface")]
    [Fact]
    public Task CodeFix_IdentifierName_ReplaceWithInterface()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        return Given("source with direct type usage",
                () => ApplyFixAsync(source, "WG2001",
                    new[] { new InMemoryAdditionalText("test.wrapgod-types.txt",
                        "Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade") })
                    .GetAwaiter().GetResult())
            .Then("the fixed source contains IWrappedFooService", fixedSource =>
                fixedSource.Contains("IWrappedFooService", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Analyzer reports correct supported diagnostics ─────

    [Scenario("Analyzer SupportedDiagnostics includes WG2001 and WG2002")]
    [Fact]
    public Task Analyzer_SupportedDiagnostics()
        => Given("a DirectUsageAnalyzer instance",
                () => new DirectUsageAnalyzer())
            .Then("supported diagnostics include WG2001", analyzer =>
                analyzer.SupportedDiagnostics.Any(d => d.Id == "WG2001"))
            .And("supported diagnostics include WG2002", analyzer =>
                analyzer.SupportedDiagnostics.Any(d => d.Id == "WG2002"))
            .And("WG2001 is a warning", analyzer =>
                analyzer.SupportedDiagnostics.First(d => d.Id == "WG2001").DefaultSeverity == DiagnosticSeverity.Warning)
            .And("WG2002 is a warning", analyzer =>
                analyzer.SupportedDiagnostics.First(d => d.Id == "WG2002").DefaultSeverity == DiagnosticSeverity.Warning)
            .AssertPassed();

    // ── Scenario: RoslynDiagnosticAdapter ────────────────────────────

    [Scenario("RoslynDiagnosticAdapter converts Roslyn diagnostic to WgDiagnosticV1")]
    [Fact]
    public Task RoslynDiagnosticAdapter_Converts()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        var diagnostics = RunDiagnosticsWithFiles(source,
            new InMemoryAdditionalText("test.wrapgod-types.txt",
                "Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade"));

        var wg2001 = diagnostics.FirstOrDefault(d => d.Id == "WG2001");

        return Given("a WG2001 diagnostic converted via adapter",
                () => RoslynDiagnosticAdapter.ToWgDiagnosticV1(wg2001!))
            .Then("the code is WG2001", wgDiag => wgDiag.Code == "WG2001")
            .And("the severity is warning", wgDiag => wgDiag.Severity == "warning")
            .And("the source tool is WrapGod", wgDiag => wgDiag.Source!.Tool == "WrapGod")
            .And("the message is not empty", wgDiag => !string.IsNullOrEmpty(wgDiag.Message))
            .AssertPassed();
    }

    // ── Scenario: Legacy .txt fallback still works alongside manifest ─

    [Scenario("Legacy .txt file is used as fallback when no manifest present")]
    [Fact]
    public Task LegacyTxtFallback()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        return Given("source with only a .wrapgod-types.txt file",
                () => RunDiagnosticsWithFiles(source,
                    new InMemoryAdditionalText("test.wrapgod-types.txt",
                        "Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade")))
            .Then("WG2001 is reported via txt fallback", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenario: Malformed .txt line is skipped ─────────────────────

    [Scenario("Malformed txt mapping line is silently skipped")]
    [Fact]
    public Task MalformedTxtLine_Skipped()
    {
        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        var txtContent = """
            # comment line
            bad line without arrow
            Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade
            another bad line
            """;

        return Given("source with a txt file containing malformed and valid lines",
                () => RunDiagnosticsWithFiles(source,
                    new InMemoryAdditionalText("test.wrapgod-types.txt", txtContent)))
            .Then("WG2001 is still reported for the valid line", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenario: UseWrapperCodeFixProvider.FixableDiagnosticIds ──────

    [Scenario("Code fix provider reports correct fixable diagnostic IDs")]
    [Fact]
    public Task CodeFixProvider_FixableIds()
        => Given("a UseWrapperCodeFixProvider instance",
                () => new UseWrapperCodeFixProvider())
            .Then("it fixes WG2001", provider =>
                provider.FixableDiagnosticIds.Contains("WG2001"))
            .And("it fixes WG2002", provider =>
                provider.FixableDiagnosticIds.Contains("WG2002"))
            .AssertPassed();

    // ── Scenario: Manifest type with missing fullName/name skipped ────

    [Scenario("Manifest type with missing fields is silently skipped")]
    [Fact]
    public Task ManifestTypeMissingFields_Skipped()
    {
        var manifest = """
            {
              "schemaVersion": "1.0",
              "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
              "types": [
                {
                  "fullName": "",
                  "name": "",
                  "namespace": "Acme.Lib",
                  "members": []
                }
              ]
            }
            """;

        var source = AcmeLibDefinition + """

            namespace MyApp
            {
                public class Consumer
                {
                    private Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                }
            }
            """;

        return Given("source with a manifest where type has empty name/fullName",
                () => RunDiagnosticsWithFiles(source,
                    new InMemoryAdditionalText("acme.wrapgod.json", manifest)))
            .Then("no WG2001 diagnostics (type skipped)", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

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
