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

    // -- OutputNamespace Scenarios -------------------------------------

    private static readonly string CustomNamespaceConfig = """
        {
          "outputNamespace": "MyCustomNamespace",
          "types": [
            { "sourceType": "Acme.Lib.FooService", "include": true }
          ]
        }
        """;

    private static GeneratorDriverRunResult RunGeneratorWithManifestAndConfig(string manifest, string config)
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
            additionalTexts: new AdditionalText[]
            {
                new InMemoryAdditionalText("acme.wrapgod.json", manifest),
                new InMemoryAdditionalText("acme.wrapgod.config.json", config),
            });

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    [Scenario("Custom outputNamespace in config overrides default namespace")]
    [Fact]
    public Task OutputNamespaceOverridesDefault()
        => Given("a generator run with a manifest and custom namespace config", () =>
                RunGeneratorWithManifestAndConfig(SimpleManifest, CustomNamespaceConfig))
            .Then("the interface uses the custom namespace", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "IWrappedFooService.g.cs")
                    .SourceText.ToString()
                    .Contains("namespace MyCustomNamespace;", StringComparison.Ordinal))
            .And("the facade uses the custom namespace", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "FooServiceFacade.g.cs")
                    .SourceText.ToString()
                    .Contains("namespace MyCustomNamespace;", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("No outputNamespace in config uses default WrapGod.Generated")]
    [Fact]
    public Task NoOutputNamespaceUsesDefault()
        => Given("a generator run with a manifest and config without outputNamespace", () =>
                RunGeneratorWithManifestAndConfig(SimpleManifest, """
                    {
                      "types": [
                        { "sourceType": "Acme.Lib.FooService", "include": true }
                      ]
                    }
                    """))
            .Then("the interface uses the default namespace", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "IWrappedFooService.g.cs")
                    .SourceText.ToString()
                    .Contains("namespace WrapGod.Generated;", StringComparison.Ordinal))
            .AssertPassed();

    // -- Static type facade tests ----------------------------------------

    private static readonly string StaticTypeManifest = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Extensions",
              "name": "Extensions",
              "namespace": "Acme.Lib",
              "isStatic": true,
              "members": [
                {
                  "name": "ShouldBe",
                  "kind": "method",
                  "returnType": "System.Void",
                  "parameters": [
                    { "name": "actual", "type": "T", "isThis": true },
                    { "name": "expected", "type": "T" }
                  ],
                  "isStatic": true,
                  "genericParameters": [{ "name": "T" }]
                },
                {
                  "name": "ShouldBeGreaterThan",
                  "kind": "method",
                  "returnType": "System.Void",
                  "parameters": [
                    { "name": "actual", "type": "T", "isThis": true },
                    { "name": "expected", "type": "T" }
                  ],
                  "isStatic": true,
                  "genericParameters": [{ "name": "T", "constraints": ["System.IComparable<T>"] }]
                }
              ]
            }
          ]
        }
        """;

    [Scenario("Static types produce static facade class, not interface + instance facade")]
    [Fact]
    public Task StaticType_ProducesStaticFacade()
        => Given("a generator run with a static type manifest", () =>
                RunGeneratorWithManifestAndConfig(StaticTypeManifest, """
                    {
                      "outputNamespace": "TestNs",
                      "types": [
                        { "sourceType": "Acme.Lib.Extensions", "include": true }
                      ]
                    }
                    """))
            .Then("no interface is generated for static types", result =>
                !result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .Any(s => s.HintName.Contains("IWrapped")))
            .And("a static facade class is generated", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .Any(s => s.HintName == "Extensions.g.cs"))
            .And("the facade uses the custom namespace", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "Extensions.g.cs")
                    .SourceText.ToString()
                    .Contains("namespace TestNs;", StringComparison.Ordinal))
            .And("the facade is a public static class", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "Extensions.g.cs")
                    .SourceText.ToString()
                    .Contains("public static class Extensions", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Static facade emits void methods correctly")]
    [Fact]
    public Task StaticFacade_VoidMethods()
        => Given("a generator run with a static type manifest", () =>
                RunGeneratorWithManifestAndConfig(StaticTypeManifest, """
                    {
                      "types": [
                        { "sourceType": "Acme.Lib.Extensions", "include": true }
                      ]
                    }
                    """))
            .Then("void return types use C# void keyword", result =>
            {
                var source = result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "Extensions.g.cs")
                    .SourceText.ToString();
                return source.Contains("public static void ShouldBe", StringComparison.Ordinal)
                    && !source.Contains("System.Void", StringComparison.Ordinal);
            })
            .AssertPassed();

    [Scenario("Static facade preserves extension method this parameter")]
    [Fact]
    public Task StaticFacade_PreservesThisParameter()
        => Given("a generator run with a static extension type", () =>
                RunGeneratorWithManifestAndConfig(StaticTypeManifest, """
                    {
                      "types": [
                        { "sourceType": "Acme.Lib.Extensions", "include": true }
                      ]
                    }
                    """))
            .Then("the this keyword is preserved on extension method parameters", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "Extensions.g.cs")
                    .SourceText.ToString()
                    .Contains("this T actual", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Static facade emits generic constraints")]
    [Fact]
    public Task StaticFacade_EmitsGenericConstraints()
        => Given("a generator run with a constrained generic method", () =>
                RunGeneratorWithManifestAndConfig(StaticTypeManifest, """
                    {
                      "types": [
                        { "sourceType": "Acme.Lib.Extensions", "include": true }
                      ]
                    }
                    """))
            .Then("where clause is emitted for constrained generic", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "Extensions.g.cs")
                    .SourceText.ToString()
                    .Contains("where T : System.IComparable<T>", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Type alias mapping: System.String becomes string")]
    [Fact]
    public Task StaticFacade_TypeAliasMapping()
        => Given("a generator run with System.String parameter types", () =>
        {
            var manifest = """
                {
                  "schemaVersion": "1.0",
                  "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
                  "types": [
                    {
                      "fullName": "Acme.Lib.StringExt",
                      "name": "StringExt",
                      "namespace": "Acme.Lib",
                      "isStatic": true,
                      "members": [
                        {
                          "name": "ShouldContain",
                          "kind": "method",
                          "returnType": "System.Void",
                          "parameters": [
                            { "name": "actual", "type": "System.String", "isThis": true },
                            { "name": "expected", "type": "System.String" }
                          ],
                          "isStatic": true
                        }
                      ]
                    }
                  ]
                }
                """;
            return RunGeneratorWithManifestAndConfig(manifest, """
                {
                  "types": [{ "sourceType": "Acme.Lib.StringExt", "include": true }]
                }
                """);
        })
            .Then("System.String is mapped to string", result =>
            {
                var source = result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .First(s => s.HintName == "StringExt.g.cs")
                    .SourceText.ToString();
                return source.Contains("this string actual", StringComparison.Ordinal)
                    && !source.Contains("System.String", StringComparison.Ordinal);
            })
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
