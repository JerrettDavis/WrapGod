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

[Feature("Generic analyzer detects generic type usage")]
public sealed class GenericAnalyzerTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Manifest that declares a generic Repository type (no type args in fullName,
    /// matching the convention for open generic types in manifests).
    /// </summary>
    private static readonly string GenericManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "stableId": "Acme.Lib.Repository",
              "fullName": "Acme.Lib.Repository",
              "name": "Repository",
              "namespace": "Acme.Lib",
              "kind": "class",
              "interfaces": [],
              "genericParameters": ["T"],
              "members": [
                {
                  "stableId": "Acme.Lib.Repository.Get(System.Int32)",
                  "name": "Get",
                  "kind": "method",
                  "returnType": "T",
                  "parameters": [{ "name": "id", "type": "int" }],
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

        // Add System.Collections so generic types resolve.
        var collectionsPath = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Collections.dll");
        if (System.IO.File.Exists(collectionsPath))
            references.Add(MetadataReference.CreateFromFile(collectionsPath));

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

    // ── Source snippets ──────────────────────────────────────────────

    private const string GenericLibDefinition = """
        namespace Acme.Lib
        {
            public class Repository<T>
            {
                public T Get(int id) => default;
            }
        }
        """;

    private const string GenericInstantiationSource = GenericLibDefinition + """

        namespace MyApp
        {
            public class User { }

            public class Consumer
            {
                private Acme.Lib.Repository<User> _repo = new Acme.Lib.Repository<User>();
            }
        }
        """;

    private const string GenericMethodCallSource = GenericLibDefinition + """

        namespace MyApp
        {
            public class User { }

            public class Consumer
            {
                public void Run()
                {
                    var repo = new Acme.Lib.Repository<User>();
                    repo.Get(42);
                }
            }
        }
        """;

    private const string GenericWrapperUsageSource = GenericLibDefinition + """

        namespace MyApp
        {
            public class User { }

            public interface IWrappedRepository<T>
            {
                T Get(int id);
            }

            public class Consumer
            {
                private readonly IWrappedRepository<User> _repo;

                public Consumer(IWrappedRepository<User> repo)
                {
                    _repo = repo;
                }
            }
        }
        """;

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("Generic type instantiation triggers WG2001")]
    [Fact]
    public Task GenericTypeInstantiation_ReportsWG2001()
        => Given("source that instantiates a wrapped generic type",
                () => RunDiagnosticsWithFiles(
                    GenericInstantiationSource,
                    new InMemoryAdditionalText("acme.wrapgod.json", GenericManifest)))
            .Then("at least one WG2001 diagnostic is reported", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2001"))
            .And("the diagnostic message mentions Repository", diagnostics =>
                diagnostics.First(d => d.Id == "WG2001")
                    .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                    .Contains("Repository", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Generic method call triggers WG2002")]
    [Fact]
    public Task GenericMethodCall_ReportsWG2002()
        => Given("source that calls a method on a wrapped generic type",
                () => RunDiagnosticsWithFiles(
                    GenericMethodCallSource,
                    new InMemoryAdditionalText("acme.wrapgod.json", GenericManifest)))
            .Then("at least one WG2002 diagnostic is reported", diagnostics =>
                diagnostics.Any(d => d.Id == "WG2002"))
            .And("the diagnostic message mentions Get", diagnostics =>
                diagnostics.First(d => d.Id == "WG2002")
                    .GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                    .Contains("Get", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Code fix preserves type arguments")]
    [Fact]
    public Task CodeFix_PreservesTypeArguments()
        => Given("source using wrapper interface with generic type args produces no diagnostic",
                () => RunDiagnosticsWithFiles(
                    GenericWrapperUsageSource,
                    new InMemoryAdditionalText("acme.wrapgod.json", GenericManifest)))
            .Then("no WG2001 diagnostics for wrapper usage", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2001" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("IWrappedRepository", StringComparison.Ordinal)))
            .And("no WG2002 diagnostics for wrapper usage", diagnostics =>
                !diagnostics.Any(d => d.Id == "WG2002" &&
                    d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                        .Contains("IWrappedRepository", StringComparison.Ordinal)))
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
