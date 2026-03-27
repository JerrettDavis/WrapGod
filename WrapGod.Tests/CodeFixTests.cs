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

[Feature("Code fix provider for wrapper migration")]
public sealed class CodeFixTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private const string MappingFileName = "test.wrapgod-types.txt";

    private static readonly string DefaultMappings =
        "Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade";

    /// <summary>
    /// Compiles the source, runs the analyzer, then applies the first
    /// code fix offered for the given diagnostic ID and returns the
    /// updated source text.
    /// </summary>
    private static async Task<(string FixedSource, int FixCount)> ApplyFixAsync(
        string source,
        string diagnosticId,
        string? mappings = null)
    {
        var (compilation, additionalTexts) = CreateCompilation(source, mappings);

        var analyzer = new DirectUsageAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));

        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        var targetDiagnostics = diagnostics.Where(d => d.Id == diagnosticId).ToList();

        if (targetDiagnostics.Count == 0)
            return (source, 0);

        var fixProvider = new UseWrapperCodeFixProvider();
        var document = CreateDocument(compilation, source);

        CodeAction? codeAction = null;
        var context = new CodeFixContext(
            document,
            targetDiagnostics[0],
            (action, _) => codeAction = action,
            CancellationToken.None);

        await fixProvider.RegisterCodeFixesAsync(context);

        if (codeAction is null)
            return (source, 0);

        var operations = await codeAction.GetOperationsAsync(CancellationToken.None);
        var changedSolution = operations
            .OfType<ApplyChangesOperation>()
            .Single()
            .ChangedSolution;

        var changedDocument = changedSolution.GetDocument(document.Id)!;
        var changedText = await changedDocument.GetTextAsync();

        return (changedText.ToString(), targetDiagnostics.Count);
    }

    /// <summary>
    /// Returns the number of diagnostics reported for the given ID.
    /// </summary>
    private static async Task<int> CountDiagnosticsAsync(
        string source,
        string diagnosticId,
        string? mappings = null)
    {
        var (compilation, additionalTexts) = CreateCompilation(source, mappings);

        var analyzer = new DirectUsageAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));

        var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        return diagnostics.Count(d => d.Id == diagnosticId);
    }

    private static (CSharpCompilation Compilation, AdditionalText[] AdditionalTexts)
        CreateCompilation(string source, string? mappings)
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

        var additionalTexts = new AdditionalText[]
        {
            new InMemoryAdditionalText(MappingFileName, mappings ?? DefaultMappings),
        };

        return (compilation, additionalTexts);
    }

    private static Document CreateDocument(
        CSharpCompilation compilation,
        string source)
    {
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

        return solution.GetDocument(documentId)!;
    }

    // ── Source snippets ──────────────────────────────────────────────

    private const string AcmeLibDefinition = """
        namespace Acme.Lib
        {
            public class FooService
            {
                public string DoWork(string input) => input;
            }
        }
        """;

    private const string DirectTypeUsageSource = AcmeLibDefinition + """

        namespace MyApp
        {
            public class Consumer
            {
                private Acme.Lib.FooService _svc = new Acme.Lib.FooService();
            }
        }
        """;

    private const string DirectMethodCallSource = AcmeLibDefinition + """

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

    private const string NoMappingSource = """
        namespace OtherLib
        {
            public class BarService
            {
                public void Execute() { }
            }
        }

        namespace MyApp
        {
            public class Consumer
            {
                private OtherLib.BarService _svc = new OtherLib.BarService();

                public void Run()
                {
                    _svc.Execute();
                }
            }
        }
        """;

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("WG2001 fix replaces type with wrapper interface")]
    [Fact]
    public Task WG2001Fix_ReplacesTypeWithWrapperInterface()
        => Given("source code that uses a wrapped type directly",
                () => ApplyFixAsync(DirectTypeUsageSource, "WG2001").GetAwaiter().GetResult())
            .Then("the fixed source contains the wrapper interface name", result =>
                result.FixedSource.Contains("IWrappedFooService", StringComparison.Ordinal))
            .And("at least one fix was available", result =>
                result.FixCount > 0)
            .AssertPassed();

    [Scenario("WG2002 fix replaces direct call with facade call")]
    [Fact]
    public Task WG2002Fix_ReplacesDirectCallWithFacadeCall()
        => Given("source code that calls a method on a wrapped type",
                () => ApplyFixAsync(DirectMethodCallSource, "WG2002").GetAwaiter().GetResult())
            .Then("the fixed source contains the facade type name", result =>
                result.FixedSource.Contains("FooServiceFacade", StringComparison.Ordinal))
            .And("at least one fix was available", result =>
                result.FixCount > 0)
            .AssertPassed();

    [Scenario("No fix offered when no mapping exists")]
    [Fact]
    public Task NoFixOffered_WhenNoMappingExists()
        => Given("source code using a type with no wrapper mapping",
                () => CountDiagnosticsAsync(NoMappingSource, "WG2001").GetAwaiter().GetResult())
            .Then("no WG2001 diagnostics are reported", count => count == 0)
            .AssertPassed();

    [Scenario("FixAllProvider is BatchFixer")]
    [Fact]
    public Task FixAllProvider_IsBatchFixer()
        => Given("a UseWrapperCodeFixProvider instance",
                () => new UseWrapperCodeFixProvider())
            .Then("FixAllProvider returns BatchFixer", provider =>
                provider.GetFixAllProvider() == WellKnownFixAllProviders.BatchFixer)
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
