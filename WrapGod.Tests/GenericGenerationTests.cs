using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Generator;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Generic wrapper and facade generation")]
public sealed class GenericGenerationTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // -- Helpers -------------------------------------------------------

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
                new InMemoryAdditionalText("test.wrapgod.json", manifest),
            });

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static string GetInterfaceSource(GeneratorDriverRunResult result, string typeName)
        => result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(s => s.HintName == "IWrapped" + typeName + ".g.cs")
            .SourceText.ToString();

    private static string GetFacadeSource(GeneratorDriverRunResult result, string typeName)
        => result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(s => s.HintName == typeName + "Facade.g.cs")
            .SourceText.ToString();

    // -- Manifests ----------------------------------------------------

    private static readonly string SingleGenericManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Repository<T>",
              "name": "Repository",
              "namespace": "Acme.Lib",
              "kind": "class",
              "interfaces": [],
              "isGenericType": true,
              "isGenericTypeDefinition": true,
              "genericParameters": [
                { "name": "T", "position": 0, "variance": "None", "constraints": ["class"] }
              ],
              "members": [
                {
                  "name": "Get",
                  "kind": "method",
                  "returnType": "T",
                  "parameters": [{ "name": "id", "type": "int" }],
                  "genericParameters": [],
                  "isStatic": false,
                  "hasGetter": false,
                  "hasSetter": false
                }
              ]
            }
          ]
        }
        """;

    private static readonly string MultiGenericManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Cache<TKey, TValue>",
              "name": "Cache",
              "namespace": "Acme.Lib",
              "kind": "class",
              "interfaces": [],
              "isGenericType": true,
              "isGenericTypeDefinition": true,
              "genericParameters": [
                { "name": "TKey", "position": 0, "variance": "None", "constraints": [] },
                { "name": "TValue", "position": 1, "variance": "None", "constraints": ["class"] }
              ],
              "members": [
                {
                  "name": "Lookup",
                  "kind": "method",
                  "returnType": "TValue",
                  "parameters": [{ "name": "key", "type": "TKey" }],
                  "genericParameters": [],
                  "isStatic": false,
                  "hasGetter": false,
                  "hasSetter": false
                }
              ]
            }
          ]
        }
        """;

    private static readonly string OpenGenericWrapperManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Wrapper<T>",
              "name": "Wrapper",
              "namespace": "Acme.Lib",
              "kind": "class",
              "interfaces": [],
              "isGenericType": true,
              "isGenericTypeDefinition": true,
              "genericParameters": [
                { "name": "T", "position": 0, "variance": "None", "constraints": [] }
              ],
              "members": [
                {
                  "name": "Value",
                  "kind": "property",
                  "returnType": "T",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false,
                  "hasGetter": true,
                  "hasSetter": true
                }
              ]
            }
          ]
        }
        """;

    // -- Scenarios: generic interface emission with constraints --------

    [Scenario("Generic interface emission includes type parameters and constraints")]
    [Fact]
    public Task GenericInterface_EmitsTypeParametersAndConstraints()
        => Given("a manifest with a single-param generic type with constraints",
                () => RunGeneratorWithManifest(SingleGenericManifest))
            .Then("the interface source contains <T>", result =>
                GetInterfaceSource(result, "Repository").Contains("<T>", StringComparison.Ordinal))
            .And("the interface source contains a where clause", result =>
                GetInterfaceSource(result, "Repository").Contains("where T : class", StringComparison.Ordinal))
            .And("the interface name is IWrappedRepository<T>", result =>
                GetInterfaceSource(result, "Repository").Contains("IWrappedRepository<T>", StringComparison.Ordinal))
            .AssertPassed();

    // -- Scenarios: multi-parameter generic facade --------------------

    [Scenario("Multi-parameter generic facade preserves all type parameters")]
    [Fact]
    public Task MultiParamGenericFacade_PreservesAllTypeParameters()
        => Given("a manifest with a two-param generic type",
                () => RunGeneratorWithManifest(MultiGenericManifest))
            .Then("the facade source contains <TKey, TValue>", result =>
                GetFacadeSource(result, "Cache").Contains("<TKey, TValue>", StringComparison.Ordinal))
            .And("the facade source contains the TValue constraint", result =>
                GetFacadeSource(result, "Cache").Contains("where TValue : class", StringComparison.Ordinal))
            .And("the facade implements IWrappedCache<TKey, TValue>", result =>
                GetFacadeSource(result, "Cache").Contains("IWrappedCache<TKey, TValue>", StringComparison.Ordinal))
            .AssertPassed();

    // -- Scenarios: open generic wrapper ------------------------------

    [Scenario("Open generic wrapper emits property forwarding with type parameter")]
    [Fact]
    public Task OpenGenericWrapper_EmitsPropertyForwarding()
        => Given("a manifest with an open generic type with a property",
                () => RunGeneratorWithManifest(OpenGenericWrapperManifest))
            .Then("the interface contains a T Value property", result =>
                GetInterfaceSource(result, "Wrapper").Contains("T Value", StringComparison.Ordinal))
            .And("the facade contains the generic parameter", result =>
                GetFacadeSource(result, "Wrapper").Contains("<T>", StringComparison.Ordinal))
            .And("the facade forwards the property", result =>
                GetFacadeSource(result, "Wrapper").Contains("_inner.Value", StringComparison.Ordinal))
            .AssertPassed();

    // -- In-memory AdditionalText ------------------------------------

    private sealed class InMemoryAdditionalText(string path, string content) : AdditionalText
    {
        private readonly SourceText _text = SourceText.From(content);
        public override string Path { get; } = path;
        public override SourceText? GetText(CancellationToken cancellationToken = default) => _text;
    }
}
