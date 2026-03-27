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
using WrapGod.Generator;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("End-to-end pipeline: extract manifest, configure, generate, analyze, fix")]
public sealed class EndToEndTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Manifests ────────────────────────────────────────────────────

    /// <summary>
    /// A synthetic manifest containing two types: FooService and BarClient.
    /// FooService has two methods (DoWork, GetStatus) and a property (Name).
    /// BarClient has one method (Send).
    /// </summary>
    private static readonly string TwoTypeManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "stableId": "Acme.Lib.FooService",
              "fullName": "Acme.Lib.FooService",
              "name": "FooService",
              "namespace": "Acme.Lib",
              "kind": "class",
              "interfaces": [],
              "genericParameters": [],
              "members": [
                {
                  "stableId": "Acme.Lib.FooService.DoWork(System.String)",
                  "name": "DoWork",
                  "kind": "method",
                  "returnType": "string",
                  "parameters": [{ "name": "input", "type": "string" }],
                  "genericParameters": [],
                  "isStatic": false, "isVirtual": false, "isAbstract": false,
                  "hasGetter": false, "hasSetter": false
                },
                {
                  "stableId": "Acme.Lib.FooService.GetStatus()",
                  "name": "GetStatus",
                  "kind": "method",
                  "returnType": "int",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false, "isVirtual": false, "isAbstract": false,
                  "hasGetter": false, "hasSetter": false
                },
                {
                  "stableId": "Acme.Lib.FooService.Name",
                  "name": "Name",
                  "kind": "property",
                  "returnType": "string",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false, "isVirtual": false, "isAbstract": false,
                  "hasGetter": true, "hasSetter": false
                }
              ]
            },
            {
              "stableId": "Acme.Lib.BarClient",
              "fullName": "Acme.Lib.BarClient",
              "name": "BarClient",
              "namespace": "Acme.Lib",
              "kind": "class",
              "interfaces": [],
              "genericParameters": [],
              "members": [
                {
                  "stableId": "Acme.Lib.BarClient.Send(System.String)",
                  "name": "Send",
                  "kind": "method",
                  "returnType": "void",
                  "parameters": [{ "name": "payload", "type": "string" }],
                  "genericParameters": [],
                  "isStatic": false, "isVirtual": false, "isAbstract": false,
                  "hasGetter": false, "hasSetter": false
                }
              ]
            }
          ]
        }
        """;

    // ── Source snippets ──────────────────────────────────────────────

    private const string AcmeLibDefinition = """
        namespace Acme.Lib
        {
            public class FooService
            {
                public string DoWork(string input) => input;
                public int GetStatus() => 42;
                public string Name => "Foo";
            }

            public class BarClient
            {
                public void Send(string payload) { }
            }
        }
        """;

    private const string DirectFooUsageSource = AcmeLibDefinition + """

        namespace MyApp
        {
            public class Consumer
            {
                private Acme.Lib.FooService _svc = new Acme.Lib.FooService();
                public void Run()
                {
                    _svc.DoWork("hello");
                }
            }
        }
        """;

    private const string DirectBarUsageSource = AcmeLibDefinition + """

        namespace MyApp
        {
            public class Sender
            {
                private Acme.Lib.BarClient _client = new Acme.Lib.BarClient();
                public void Run()
                {
                    _client.Send("data");
                }
            }
        }
        """;

    // ── Pipeline result containers ───────────────────────────────────

    private sealed record PipelineResult(
        GeneratorDriverRunResult GeneratorResult,
        ImmutableArray<Diagnostic> AnalyzerDiagnostics,
        string? FixedSource);

    private sealed record GenerateAndAnalyzeResult(
        GeneratorDriverRunResult GeneratorResult,
        ImmutableArray<Diagnostic> AnalyzerDiagnostics);

    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full pipeline: generator -> analyzer -> code fix.
    /// </summary>
    private static PipelineResult RunFullPipeline(
        string manifest,
        string? config,
        string userSource,
        string mappings)
    {
        // Step 1: Run generator
        var genResult = RunGenerator(manifest, config);

        // Step 2: Run analyzer on user code
        var diagnostics = RunAnalyzer(userSource, mappings);

        // Step 3: Apply code fix for WG2001 if present
        string? fixedSource = null;
        var wg2001 = diagnostics.FirstOrDefault(d => d.Id == "WG2001");
        if (wg2001 is not null)
        {
            fixedSource = ApplyCodeFix(userSource, "WG2001", mappings);
        }

        return new PipelineResult(genResult, diagnostics, fixedSource);
    }

    private static GeneratorDriverRunResult RunGenerator(string manifest, string? config = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("namespace Placeholder; public class Marker { }");
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var additionalTexts = new List<AdditionalText>
        {
            new InMemoryAdditionalText("acme.wrapgod.json", manifest),
        };

        if (config != null)
        {
            additionalTexts.Add(new InMemoryAdditionalText("acme.wrapgod.config.json", config));
        }

        IIncrementalGenerator generator = new WrapGodIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: additionalTexts);

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static ImmutableArray<Diagnostic> RunAnalyzer(string source, string mappings)
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
        var additionalTexts = new AdditionalText[]
        {
            new InMemoryAdditionalText("test.wrapgod-types.txt", mappings),
        };

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    private static string ApplyCodeFix(string source, string diagnosticId, string mappings)
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
        var additionalTexts = new AdditionalText[]
        {
            new InMemoryAdditionalText("test.wrapgod-types.txt", mappings),
        };

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));

        var diagnostics = compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        var target = diagnostics.FirstOrDefault(d => d.Id == diagnosticId);
        if (target is null)
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
            target,
            (action, _) => codeAction = action,
            CancellationToken.None);

        fixProvider.RegisterCodeFixesAsync(context).GetAwaiter().GetResult();

        if (codeAction is null)
            return source;

        var operations = codeAction.GetOperationsAsync(CancellationToken.None).GetAwaiter().GetResult();
        var changedSolution = operations
            .OfType<ApplyChangesOperation>()
            .Single()
            .ChangedSolution;

        var changedDocument = changedSolution.GetDocument(documentId)!;
        var changedText = changedDocument.GetTextAsync().GetAwaiter().GetResult();
        return changedText.ToString();
    }

    private static bool HasSource(GeneratorDriverRunResult result, string hintName)
        => result.Results
            .SelectMany(r => r.GeneratedSources)
            .Any(s => s.HintName == hintName);

    private static string GetSource(GeneratorDriverRunResult result, string hintName)
        => result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(s => s.HintName == hintName)
            .SourceText.ToString();

    // ── Scenario 1: Full pipeline ────────────────────────────────────

    [Scenario("Full pipeline: manifest -> generate -> analyze -> fix")]
    [Fact]
    public Task FullPipeline_GenerateAnalyzeFix()
        => Given("a two-type manifest, no config, and user code with direct FooService usage", () =>
            {
                // Mapping line the analyzer needs (mirrors what the generator would produce)
                const string mappings =
                    "Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade\n" +
                    "Acme.Lib.BarClient -> IWrappedBarClient, BarClientFacade";

                return RunFullPipeline(TwoTypeManifest, config: null, DirectFooUsageSource, mappings);
            })
            // Generator produces wrappers for both types
            .Then("interface is generated for FooService", result =>
                HasSource(result.GeneratorResult, "IWrappedFooService.g.cs"))
            .And("facade is generated for FooService", result =>
                HasSource(result.GeneratorResult, "FooServiceFacade.g.cs"))
            .And("interface is generated for BarClient", result =>
                HasSource(result.GeneratorResult, "IWrappedBarClient.g.cs"))
            .And("facade is generated for BarClient", result =>
                HasSource(result.GeneratorResult, "BarClientFacade.g.cs"))
            // Generated interface contains expected members
            .And("FooService interface declares DoWork", result =>
                GetSource(result.GeneratorResult, "IWrappedFooService.g.cs")
                    .Contains("string DoWork(string input)", StringComparison.Ordinal))
            .And("FooService interface declares GetStatus", result =>
                GetSource(result.GeneratorResult, "IWrappedFooService.g.cs")
                    .Contains("int GetStatus()", StringComparison.Ordinal))
            .And("FooService interface declares Name property", result =>
                GetSource(result.GeneratorResult, "IWrappedFooService.g.cs")
                    .Contains("string Name { get; }", StringComparison.Ordinal))
            // Analyzer detects direct usage
            .And("WG2001 diagnostic is reported for direct type usage", result =>
                result.AnalyzerDiagnostics.Any(d => d.Id == "WG2001"))
            .And("WG2002 diagnostic is reported for direct method call", result =>
                result.AnalyzerDiagnostics.Any(d => d.Id == "WG2002"))
            // Code fix replaces direct type with wrapper interface
            .And("code fix replaces FooService with IWrappedFooService", result =>
                result.FixedSource != null &&
                result.FixedSource.Contains("IWrappedFooService", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenario 2: Filtered pipeline ────────────────────────────────

    [Scenario("Filtered pipeline: config excludes BarClient, generator skips it, analyzer only flags FooService")]
    [Fact]
    public Task FilteredPipeline_ConfigExcludesType()
        => Given("a config that excludes BarClient and user code using both types", () =>
            {
                const string config = """
                    {
                      "types": [
                        { "sourceType": "Acme.Lib.BarClient", "include": false }
                      ]
                    }
                    """;

                var genResult = RunGenerator(TwoTypeManifest, config);

                // Only FooService should have mappings (BarClient excluded)
                const string mappings =
                    "Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade";

                var fooDiagnostics = RunAnalyzer(DirectFooUsageSource, mappings);
                var barDiagnostics = RunAnalyzer(DirectBarUsageSource, mappings);

                return new
                {
                    GeneratorResult = genResult,
                    FooDiagnostics = fooDiagnostics,
                    BarDiagnostics = barDiagnostics,
                };
            })
            // Generator skips excluded type
            .Then("no interface is generated for BarClient", result =>
                !HasSource(result.GeneratorResult, "IWrappedBarClient.g.cs"))
            .And("no facade is generated for BarClient", result =>
                !HasSource(result.GeneratorResult, "BarClientFacade.g.cs"))
            // Generator still produces FooService wrappers
            .And("interface is generated for FooService", result =>
                HasSource(result.GeneratorResult, "IWrappedFooService.g.cs"))
            .And("facade is generated for FooService", result =>
                HasSource(result.GeneratorResult, "FooServiceFacade.g.cs"))
            // Analyzer flags FooService usage but not BarClient usage
            .And("FooService direct usage triggers WG2001", result =>
                result.FooDiagnostics.Any(d => d.Id == "WG2001"))
            .And("BarClient direct usage does NOT trigger WG2001", result =>
                !result.BarDiagnostics.Any(d => d.Id == "WG2001"))
            .And("BarClient direct usage does NOT trigger WG2002", result =>
                !result.BarDiagnostics.Any(d => d.Id == "WG2002"))
            .AssertPassed();

    // ── Scenario 3: Rename pipeline ──────────────────────────────────

    [Scenario("Rename pipeline: config renames FooService to BetterFoo, generated code uses new name")]
    [Fact]
    public Task RenamePipeline_ConfigRenamesType()
        => Given("a config that renames FooService to BetterFoo", () =>
            {
                const string config = """
                    {
                      "types": [
                        { "sourceType": "Acme.Lib.FooService", "include": true, "targetName": "BetterFoo" }
                      ]
                    }
                    """;

                var genResult = RunGenerator(TwoTypeManifest, config);

                // Mapping uses the renamed interface/facade names
                const string mappings =
                    "Acme.Lib.FooService -> IWrappedBetterFoo, BetterFooFacade\n" +
                    "Acme.Lib.BarClient -> IWrappedBarClient, BarClientFacade";

                var diagnostics = RunAnalyzer(DirectFooUsageSource, mappings);
                var fixedSource = ApplyCodeFix(DirectFooUsageSource, "WG2001", mappings);

                return new
                {
                    GeneratorResult = genResult,
                    Diagnostics = diagnostics,
                    FixedSource = fixedSource,
                };
            })
            // Generator uses renamed names
            .Then("interface uses renamed name IWrappedBetterFoo", result =>
                HasSource(result.GeneratorResult, "IWrappedBetterFoo.g.cs"))
            .And("facade uses renamed name BetterFooFacade", result =>
                HasSource(result.GeneratorResult, "BetterFooFacade.g.cs"))
            .And("renamed interface declares correct interface name", result =>
                GetSource(result.GeneratorResult, "IWrappedBetterFoo.g.cs")
                    .Contains("public interface IWrappedBetterFoo", StringComparison.Ordinal))
            .And("renamed facade still delegates to original Acme.Lib.FooService", result =>
                GetSource(result.GeneratorResult, "BetterFooFacade.g.cs")
                    .Contains("Acme.Lib.FooService", StringComparison.Ordinal))
            // Analyzer still detects direct usage of original type
            .And("WG2001 is reported for direct FooService usage", result =>
                result.Diagnostics.Any(d => d.Id == "WG2001"))
            // Code fix uses the renamed wrapper name
            .And("code fix inserts renamed wrapper IWrappedBetterFoo", result =>
                result.FixedSource.Contains("IWrappedBetterFoo", StringComparison.Ordinal))
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
