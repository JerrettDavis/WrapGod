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
    /// Compiles the given source with the <see cref="DirectUsageAnalyzer"/>
    /// registered and returns the reported diagnostics.
    /// </summary>
    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        string? mappings = null)
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
        var additionalTexts = new AdditionalText[]
        {
            new InMemoryAdditionalText(MappingFileName, mappings ?? DefaultMappings),
        };

        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private static ImmutableArray<Diagnostic> RunDiagnostics(string source, string? mappings = null)
        => GetDiagnosticsAsync(source, mappings).GetAwaiter().GetResult();

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

    // ── Scenarios ────────────────────────────────────────────────────

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
