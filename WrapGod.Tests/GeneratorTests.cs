using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Generator;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Incremental generator")]
public sealed class GeneratorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // -- Helpers -------------------------------------------------------

    private static readonly string SimpleManifest = """
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

    private static readonly string EmptyManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Empty.Lib", "version": "1.0.0" },
          "types": []
        }
        """;

    private static GeneratorDriverRunResult RunGeneratorWithManifest(string manifest)
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

        IIncrementalGenerator generator = new WrapGodIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: new[]
            {
                new InMemoryAdditionalText("acme.wrapgod.json", manifest),
            });

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static GeneratorDriverRunResult RunGeneratorWithSimpleManifest()
        => RunGeneratorWithManifest(SimpleManifest);

    private static GeneratorDriverRunResult RunGeneratorWithEmptyManifest()
        => RunGeneratorWithManifest(EmptyManifest);

    // -- Scenarios -----------------------------------------------------

    [Scenario("Generator produces interface source for a manifest type")]
    [Fact]
    public Task GeneratorProducesInterfaceSource()
        => Given("a generator run with a simple manifest", RunGeneratorWithSimpleManifest)
            .Then("an interface source file is emitted", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .Any(s => s.HintName == "IWrappedFooService.g.cs"))
            .And("the interface declares the expected method", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "IWrappedFooService.g.cs")
                    .SourceText.ToString()
                    .Contains("string DoWork(string input)", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Generator produces facade source for a manifest type")]
    [Fact]
    public Task GeneratorProducesFacadeSource()
        => Given("a generator run with a simple manifest", RunGeneratorWithSimpleManifest)
            .Then("a facade source file is emitted", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .Any(s => s.HintName == "FooServiceFacade.g.cs"))
            .And("the facade implements the interface", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "FooServiceFacade.g.cs")
                    .SourceText.ToString()
                    .Contains("FooServiceFacade : IWrappedFooService", StringComparison.Ordinal))
            .And("the facade delegates to the inner instance", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "FooServiceFacade.g.cs")
                    .SourceText.ToString()
                    .Contains("_inner.DoWork(input)", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Empty manifest produces no generated output")]
    [Fact]
    public Task EmptyManifestProducesNoOutput()
        => Given("a generator run with an empty manifest", RunGeneratorWithEmptyManifest)
            .Then("no source files are emitted", result =>
                !result.Results.SelectMany(r => r.GeneratedSources).Any())
            .AssertPassed();

    // -- In-memory AdditionalText for testing --------------------------

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
