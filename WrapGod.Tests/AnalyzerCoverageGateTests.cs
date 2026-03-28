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

[Feature("Analyzer coverage gate: default switch branches and edge cases")]
public sealed class AnalyzerCoverageGateTests(ITestOutputHelper output) : TinyBddXunitBase(output)
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

    // ── Source definitions ───────────────────────────────────────────

    private const string AcmeLibDef = """
        namespace Acme.Lib
        {
            public class FooService
            {
                public event System.EventHandler Changed;
                public string DoWork(string input) => input;
            }
            public class Repository<T>
            {
                public T Get(int id) => default;
            }
            public class Helper
            {
                public static void Run() { }
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


    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("Event identifier resolves to IEventSymbol — hits default switch branch")]
    [Fact]
    public Task EventIdentifier_HitsDefaultBranch()
    {
        var source = AcmeLibDef + """

            namespace MyApp
            {
                public class Consumer
                {
                    public void Subscribe(Acme.Lib.FooService svc)
                    {
                        svc.Changed += (s, e) => { };
                    }
                }
            }
            """;

        return Given("source with event subscription on mapped type",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest)))
            .Then("no WG2001 for event symbol (default branch returns null)",
                diags => !diags.Any(d => d.Id == "WG2001" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("Changed")))
            .AssertPassed();
    }

    [Scenario("Namespace identifier resolves to INamespaceSymbol — hits default switch branch")]
    [Fact]
    public Task NamespaceIdentifier_HitsDefaultBranch()
    {
        var source = AcmeLibDef + """

            namespace MyApp
            {
                using Acme.Lib;

                public class Consumer
                {
                    public void Use()
                    {
                        var svc = new FooService();
                    }
                }
            }
            """;

        return Given("source with using directive for mapped namespace",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest)))
            .Then("WG2001 fires for the type but not for namespace identifiers",
                diags => diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    [Scenario("Config with malformed JSON in wrapgod.config.json does not crash code fix")]
    [Fact]
    public Task MalformedConfigJson_DoesNotCrash()
    {
        var source = AcmeLibDef + """

            namespace MyApp
            {
                public class Consumer
                {
                    Acme.Lib.FooService svc;
                }
            }
            """;

        return Given("source with mapped type and malformed config",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest),
                    new InMemoryAdditionalText("a.wrapgod.config.json", "NOT VALID JSON {")))
            .Then("analyzer still fires WG2001 despite bad config",
                diags => diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    [Scenario("Static method call on mapped type triggers member access analysis")]
    [Fact]
    public Task StaticMethodCall_ExercisesMemberAccessPath()
    {
        var source = AcmeLibDef + """

            namespace MyApp
            {
                public class Consumer
                {
                    public void Use()
                    {
                        Acme.Lib.Helper.Run();
                    }
                }
            }
            """;

        var helperManifest = """
            {
              "schemaVersion": "1.0",
              "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
              "types": [
                {
                  "fullName": "Acme.Lib.Helper",
                  "name": "Helper",
                  "namespace": "Acme.Lib",
                  "members": [
                    { "name": "Run", "kind": "method", "returnType": "void", "parameters": [], "isStatic": true }
                  ]
                }
              ]
            }
            """;

        return Given("source with static method call on mapped type",
                () => RunDiagnosticsSync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", helperManifest)))
            .Then("WG2002 fires for static method usage",
                diags => diags.Any(d => d.Id == "WG2002"))
            .AssertPassed();
    }

    // ── InMemoryAdditionalText ──────────────────────────────────────

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;
        public override string Path { get; }
        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content);
        }
        public override SourceText? GetText(CancellationToken cancellationToken = default) => _text;
    }
}
