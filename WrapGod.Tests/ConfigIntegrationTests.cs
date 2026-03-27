using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Generator;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Config pipeline integration")]
public sealed class ConfigIntegrationTests(ITestOutputHelper output) : TinyBddXunitBase(output)
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
                },
                {
                  "stableId": "Acme.Lib.FooService.Name",
                  "name": "Name",
                  "kind": "property",
                  "returnType": "string",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false,
                  "isVirtual": false,
                  "isAbstract": false,
                  "hasGetter": true,
                  "hasSetter": false
                }
              ]
            }
          ]
        }
        """;

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

    private static string GetSource(GeneratorDriverRunResult result, string hintName)
        => result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(s => s.HintName == hintName)
            .SourceText.ToString();

    private static bool HasSource(GeneratorDriverRunResult result, string hintName)
        => result.Results
            .SelectMany(r => r.GeneratedSources)
            .Any(s => s.HintName == hintName);

    // -- Scenarios: type exclusion -------------------------------------

    [Scenario("Config excludes type -- no output generated for that type")]
    [Fact]
    public Task ConfigExcludesType()
        => Given("a generator run with a config that excludes FooService", () => RunGenerator(
                SimpleManifest,
                """
                {
                  "types": [
                    { "sourceType": "Acme.Lib.FooService", "include": false }
                  ]
                }
                """))
            .Then("no interface source is emitted", result =>
                !HasSource(result, "IWrappedFooService.g.cs"))
            .And("no facade source is emitted", result =>
                !HasSource(result, "FooServiceFacade.g.cs"))
            .AssertPassed();

    // -- Scenarios: type rename ----------------------------------------

    [Scenario("Config renames type -- interface and facade use new name")]
    [Fact]
    public Task ConfigRenamesType()
        => Given("a generator run with a config that renames FooService to BetterFoo", () => RunGenerator(
                SimpleManifest,
                """
                {
                  "types": [
                    { "sourceType": "Acme.Lib.FooService", "include": true, "targetName": "BetterFoo" }
                  ]
                }
                """))
            .Then("the interface uses the renamed type name", result =>
                HasSource(result, "IWrappedBetterFoo.g.cs"))
            .And("the facade uses the renamed type name", result =>
                HasSource(result, "BetterFooFacade.g.cs"))
            .And("the interface declares the correct interface name", result =>
                GetSource(result, "IWrappedBetterFoo.g.cs")
                    .Contains("public interface IWrappedBetterFoo", StringComparison.Ordinal))
            .And("the facade still delegates to the original type", result =>
                GetSource(result, "BetterFooFacade.g.cs")
                    .Contains("Acme.Lib.FooService", StringComparison.Ordinal))
            .AssertPassed();

    // -- Scenarios: member exclusion -----------------------------------

    [Scenario("Config excludes member -- member not in generated interface")]
    [Fact]
    public Task ConfigExcludesMember()
        => Given("a generator run with a config that excludes the DoWork member", () => RunGenerator(
                SimpleManifest,
                """
                {
                  "types": [
                    {
                      "sourceType": "Acme.Lib.FooService",
                      "include": true,
                      "members": [
                        { "sourceMember": "DoWork", "include": false }
                      ]
                    }
                  ]
                }
                """))
            .Then("the interface does not contain DoWork", result =>
                !GetSource(result, "IWrappedFooService.g.cs")
                    .Contains("DoWork", StringComparison.Ordinal))
            .And("the interface still contains Name", result =>
                GetSource(result, "IWrappedFooService.g.cs")
                    .Contains("Name", StringComparison.Ordinal))
            .AssertPassed();

    // -- Scenarios: member rename --------------------------------------

    [Scenario("Config renames member -- facade uses new name but delegates to original")]
    [Fact]
    public Task ConfigRenamesMember()
        => Given("a generator run with a config that renames DoWork to Execute", () => RunGenerator(
                SimpleManifest,
                """
                {
                  "types": [
                    {
                      "sourceType": "Acme.Lib.FooService",
                      "include": true,
                      "members": [
                        { "sourceMember": "DoWork", "include": true, "targetName": "Execute" }
                      ]
                    }
                  ]
                }
                """))
            .Then("the interface declares Execute instead of DoWork", result =>
                GetSource(result, "IWrappedFooService.g.cs")
                    .Contains("Execute(", StringComparison.Ordinal))
            .And("the facade public method is named Execute", result =>
                GetSource(result, "FooServiceFacade.g.cs")
                    .Contains("Execute(", StringComparison.Ordinal))
            .And("the facade still delegates to inner.DoWork", result =>
                GetSource(result, "FooServiceFacade.g.cs")
                    .Contains("_inner.DoWork(", StringComparison.Ordinal))
            .AssertPassed();

    // -- Scenarios: no config (backward compatibility) -----------------

    [Scenario("No config file -- generates everything unchanged")]
    [Fact]
    public Task NoConfigGeneratesEverything()
        => Given("a generator run without any config file", () => RunGenerator(SimpleManifest))
            .Then("the interface source is emitted", result =>
                HasSource(result, "IWrappedFooService.g.cs"))
            .And("the facade source is emitted", result =>
                HasSource(result, "FooServiceFacade.g.cs"))
            .And("the interface contains both members", result =>
            {
                var src = GetSource(result, "IWrappedFooService.g.cs");
                return src.Contains("DoWork", StringComparison.Ordinal)
                       && src.Contains("Name", StringComparison.Ordinal);
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
