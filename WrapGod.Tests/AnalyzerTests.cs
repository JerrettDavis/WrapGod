using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Analyzers;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Direct usage analyzer")]
public sealed class AnalyzerTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private const string MappingFileName = "test.wrapgod-types.txt";

    private static readonly string DefaultMappings =
        "Acme.Lib.FooService -> IWrappedFooService, FooServiceFacade";

    /// <summary>
    /// Manifest JSON for Acme.Lib.FooService with a DoWork method.
    /// </summary>
    private static readonly string DefaultManifest = """
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
                  "isStatic": false,
                  "isVirtual": false,
                  "isAbstract": false,
                  "hasGetter": false,
                  "hasSetter": false
                }
              ]
            }
          ]
        }
        """;

    /// <summary>
    /// Compiles the given source with the <see cref="DirectUsageAnalyzer"/>
    /// registered and returns the reported diagnostics.
    /// Accepts legacy .txt mappings via <paramref name="mappings"/>.
    /// </summary>
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        string? mappings = null)
    {
        var additionalTexts = new AdditionalText[]
        {
            new InMemoryAdditionalText(MappingFileName, mappings ?? DefaultMappings),
        };

        return await RunAnalyzerAsync(source, additionalTexts);
    }

    /// <summary>
    /// Compiles the given source with the <see cref="DirectUsageAnalyzer"/>
    /// registered and the supplied additional files.
    /// </summary>
    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source,
        AdditionalText[] additionalTexts)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };

        // Add the netstandard reference so netstandard2.0 types resolve.
        var netStandardPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "netstandard.dll");
        var refList = references.ToList();
        if (System.IO.File.Exists(netStandardPath))
            refList.Add(MetadataReference.CreateFromFile(netStandardPath));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            refList,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new DirectUsageAnalyzer();

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<Diagnostic> RunDiagnostics(string source, string? mappings = null)
        => GetDiagnosticsAsync(source, mappings).GetAwaiter().GetResult();

    private static ImmutableArray<Diagnostic> RunDiagnosticsWithFiles(
        string source, params AdditionalText[] additionalTexts)
        => RunAnalyzerAsync(source, additionalTexts).GetAwaiter().GetResult();

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

    private const string WrapperInterfaceUsageSource = AcmeLibDefinition + """

        namespace MyApp
        {
            public interface IWrappedFooService
            {
                string DoWork(string input);
            }

            public class Consumer
            {
                private readonly IWrappedFooService _svc;

                public Consumer(IWrappedFooService svc)
                {
                    _svc = svc;
                }

                public void Run()
                {
                    _svc.DoWork("hello");
                }
            }
        }
        """;

    // ── Scenarios: legacy .txt files (backward compat) ───────────────

    [Scenario("Direct type usage reports WG2001")]
    [Fact]
    public Task DirectTypeUsage_ReportsWG2001()
        => Given("source code that uses a wrapped type directly",
                () => RunDiagnostics(DirectTypeUsageSource))
            .Then("at least one WG2001 diagnostic is reported", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001"))
            .And("the diagnostic message mentions FooService", diagnostics =>
                diagnostics.First(d => d.Id == "WG2001")
                    .GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("FooService", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Direct method call reports WG2002")]
    [Fact]
    public Task DirectMethodCall_ReportsWG2002()
        => Given("source code that calls a method on a wrapped type",
                () => RunDiagnostics(DirectMethodCallSource))
            .Then("at least one WG2002 diagnostic is reported", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2002"))
            .And("the diagnostic message mentions DoWork", diagnostics =>
                diagnostics.First(d => d.Id == "WG2002")
                    .GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("DoWork", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Usage through wrapper interface reports no diagnostic")]
    [Fact]
    public Task WrapperInterfaceUsage_ReportsNoDiagnostic()
        => Given("source code that uses the wrapper interface",
                () => RunDiagnostics(WrapperInterfaceUsageSource))
            .Then("no WG2001 diagnostics are reported", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2001"))
            .And("no WG2002 diagnostics are reported", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2002"))
            .AssertPassed();

    [Scenario("Backward compat: .txt file mappings still work when no manifest is present")]
    [Fact]
    public Task TxtFileFallback_StillWorks()
        => Given("source with only a legacy .wrapgod-types.txt file (no manifest)",
                () => RunDiagnostics(DirectTypeUsageSource))
            .Then("WG2001 is reported via .txt fallback", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001"))
            .And("the diagnostic suggests the wrapper from the .txt mapping", diagnostics =>
                diagnostics.First(d => d.Id == "WG2001")
                    .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                    .Contains("IWrappedFooService", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenarios: manifest-based discovery ──────────────────────────

    [Scenario("Analyzer discovers types from manifest (no .txt file)")]
    [Fact]
    public Task ManifestDiscovery_ReportsWG2001()
        => Given("source with a .wrapgod.json manifest and no .txt file",
                () => RunDiagnosticsWithFiles(
                    DirectTypeUsageSource,
                    new InMemoryAdditionalText("acme.wrapgod.json", DefaultManifest)))
            .Then("at least one WG2001 diagnostic is reported", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001"))
            .And("the diagnostic suggests IWrappedFooService", diagnostics =>
                diagnostics.First(d => d.Id == "WG2001")
                    .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                    .Contains("IWrappedFooService", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Analyzer discovers method calls from manifest (no .txt file)")]
    [Fact]
    public Task ManifestDiscovery_ReportsWG2002()
        => Given("source with a .wrapgod.json manifest and no .txt file",
                () => RunDiagnosticsWithFiles(
                    DirectMethodCallSource,
                    new InMemoryAdditionalText("acme.wrapgod.json", DefaultManifest)))
            .Then("at least one WG2002 diagnostic is reported", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2002"))
            .And("the diagnostic suggests FooServiceFacade", diagnostics =>
                diagnostics.First(d => d.Id == "WG2002")
                    .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                    .Contains("FooServiceFacade", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenarios: config renames ────────────────────────────────────

    [Scenario("Analyzer applies config rename to wrapper names")]
    [Fact]
    public Task ConfigRename_AppliesTargetName()
        => Given("a manifest with a config that renames FooService to BetterFoo",
                () => RunDiagnosticsWithFiles(
                    DirectTypeUsageSource,
                    new InMemoryAdditionalText("acme.wrapgod.json", DefaultManifest),
                    new InMemoryAdditionalText("acme.wrapgod.config.json", """
                        {
                          "types": [
                            { "sourceType": "Acme.Lib.FooService", "include": true, "targetName": "BetterFoo" }
                          ]
                        }
                        """)))
            .Then("WG2001 is reported", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001"))
            .And("the diagnostic suggests IWrappedBetterFoo (renamed)", diagnostics =>
                diagnostics.First(d => d.Id == "WG2001")
                    .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                    .Contains("IWrappedBetterFoo", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Config rename also applies to facade name in WG2002")]
    [Fact]
    public Task ConfigRename_AppliesToFacade()
        => Given("a manifest with a config that renames FooService to BetterFoo",
                () => RunDiagnosticsWithFiles(
                    DirectMethodCallSource,
                    new InMemoryAdditionalText("acme.wrapgod.json", DefaultManifest),
                    new InMemoryAdditionalText("acme.wrapgod.config.json", """
                        {
                          "types": [
                            { "sourceType": "Acme.Lib.FooService", "include": true, "targetName": "BetterFoo" }
                          ]
                        }
                        """)))
            .Then("WG2002 is reported", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2002"))
            .And("the diagnostic suggests BetterFooFacade (renamed)", diagnostics =>
                diagnostics.First(d => d.Id == "WG2002")
                    .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                    .Contains("BetterFooFacade", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Config excludes type -- no diagnostic reported")]
    [Fact]
    public Task ConfigExclude_SuppressesDiagnostic()
        => Given("a manifest with a config that excludes FooService",
                () => RunDiagnosticsWithFiles(
                    DirectTypeUsageSource,
                    new InMemoryAdditionalText("acme.wrapgod.json", DefaultManifest),
                    new InMemoryAdditionalText("acme.wrapgod.config.json", """
                        {
                          "types": [
                            { "sourceType": "Acme.Lib.FooService", "include": false }
                          ]
                        }
                        """)))
            .Then("no WG2001 diagnostics are reported", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2001"))
            .And("no WG2002 diagnostics are reported", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2002"))
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
