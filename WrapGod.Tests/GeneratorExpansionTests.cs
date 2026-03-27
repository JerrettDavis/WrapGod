using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Generator;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Generator expansion: methods, properties, constructors")]
public sealed class GeneratorExpansionTests(ITestOutputHelper output) : TinyBddXunitBase(output)
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

    private static string GetFacadeSource(GeneratorDriverRunResult result, string typeName)
        => result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(s => s.HintName == typeName + "Facade.g.cs")
            .SourceText.ToString();

    private static string GetInterfaceSource(GeneratorDriverRunResult result, string typeName)
        => result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(s => s.HintName == "IWrapped" + typeName + ".g.cs")
            .SourceText.ToString();

    // -- Manifests ----------------------------------------------------

    private static readonly string MethodWithParametersManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Calculator",
              "name": "Calculator",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "Add",
                  "kind": "method",
                  "returnType": "int",
                  "parameters": [
                    { "name": "a", "type": "int" },
                    { "name": "b", "type": "int" }
                  ],
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

    private static readonly string PropertyManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Config",
              "name": "Config",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "Timeout",
                  "kind": "property",
                  "returnType": "int",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false,
                  "hasGetter": true,
                  "hasSetter": true
                },
                {
                  "name": "Name",
                  "kind": "property",
                  "returnType": "string",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false,
                  "hasGetter": true,
                  "hasSetter": false
                }
              ]
            }
          ]
        }
        """;

    private static readonly string VoidMethodManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Notifier",
              "name": "Notifier",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "Send",
                  "kind": "method",
                  "returnType": "void",
                  "parameters": [
                    { "name": "message", "type": "string" }
                  ],
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

    private static readonly string GenericMethodManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Mapper",
              "name": "Mapper",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "Convert",
                  "kind": "method",
                  "returnType": "TOut",
                  "parameters": [
                    { "name": "input", "type": "TIn" }
                  ],
                  "genericParameters": [
                    { "name": "TIn", "constraints": [] },
                    { "name": "TOut", "constraints": [] }
                  ],
                  "isStatic": false,
                  "hasGetter": false,
                  "hasSetter": false
                }
              ]
            }
          ]
        }
        """;

    private static readonly string StaticMemberManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Helper",
              "name": "Helper",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "InstanceMethod",
                  "kind": "method",
                  "returnType": "void",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false,
                  "hasGetter": false,
                  "hasSetter": false
                },
                {
                  "name": "StaticMethod",
                  "kind": "method",
                  "returnType": "void",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": true,
                  "hasGetter": false,
                  "hasSetter": false
                }
              ]
            }
          ]
        }
        """;

    private static readonly string RefOutManifest = """
        {
          "schemaVersion": "1.0",
          "generatedAt": "2026-03-27T00:00:00Z",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Parser",
              "name": "Parser",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "TryParse",
                  "kind": "method",
                  "returnType": "bool",
                  "parameters": [
                    { "name": "input", "type": "string" },
                    { "name": "result", "type": "int", "isOut": true }
                  ],
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

    // -- Scenarios -----------------------------------------------------

    [Scenario("Method with parameters generates correct interface signature")]
    [Fact]
    public Task MethodWithParametersGeneratesCorrectSignature()
        => Given("a manifest with a method taking two int parameters",
                () => RunGeneratorWithManifest(MethodWithParametersManifest))
            .Then("the interface declares the correct method signature", result =>
                GetInterfaceSource(result, "Calculator")
                    .Contains("int Add(int a, int b);", StringComparison.Ordinal))
            .And("the facade declares a matching public method", result =>
                GetFacadeSource(result, "Calculator")
                    .Contains("public int Add(int a, int b)", StringComparison.Ordinal))
            .And("the facade forwards arguments to the inner instance", result =>
                GetFacadeSource(result, "Calculator")
                    .Contains("_inner.Add(a, b)", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Property generates get/set forwarding")]
    [Fact]
    public Task PropertyGeneratesGetSetForwarding()
        => Given("a manifest with a read-write and a read-only property",
                () => RunGeneratorWithManifest(PropertyManifest))
            .Then("the interface declares the read-write property", result =>
                GetInterfaceSource(result, "Config")
                    .Contains("int Timeout { get; set; }", StringComparison.Ordinal))
            .And("the interface declares the read-only property", result =>
                GetInterfaceSource(result, "Config")
                    .Contains("string Name { get; }", StringComparison.Ordinal))
            .And("the facade getter forwards to inner", result =>
                GetFacadeSource(result, "Config")
                    .Contains("get => _inner.Timeout;", StringComparison.Ordinal))
            .And("the facade setter forwards to inner", result =>
                GetFacadeSource(result, "Config")
                    .Contains("set => _inner.Timeout = value;", StringComparison.Ordinal))
            .And("the read-only property has no setter", result =>
                !GetFacadeSource(result, "Config")
                    .Contains("_inner.Name = value", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Constructor injects wrapped instance")]
    [Fact]
    public Task ConstructorInjectsWrappedInstance()
        => Given("a manifest with a type",
                () => RunGeneratorWithManifest(MethodWithParametersManifest))
            .Then("the facade constructor takes the wrapped type", result =>
                GetFacadeSource(result, "Calculator")
                    .Contains("public CalculatorFacade(Acme.Lib.Calculator inner)", StringComparison.Ordinal))
            .And("the constructor stores the instance in a field", result =>
                GetFacadeSource(result, "Calculator")
                    .Contains("_inner = inner ?? throw new System.ArgumentNullException(nameof(inner));",
                        StringComparison.Ordinal))
            .And("the field is declared as readonly", result =>
                GetFacadeSource(result, "Calculator")
                    .Contains("private readonly Acme.Lib.Calculator _inner;", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Void method forwards correctly without return")]
    [Fact]
    public Task VoidMethodForwardsCorrectly()
        => Given("a manifest with a void method",
                () => RunGeneratorWithManifest(VoidMethodManifest))
            .Then("the interface declares a void method", result =>
                GetInterfaceSource(result, "Notifier")
                    .Contains("void Send(string message);", StringComparison.Ordinal))
            .And("the facade forwards without a return keyword", result =>
                GetFacadeSource(result, "Notifier")
                    .Contains("=> _inner.Send(message);", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Generic method emits type parameters")]
    [Fact]
    public Task GenericMethodEmitsTypeParameters()
        => Given("a manifest with a generic method",
                () => RunGeneratorWithManifest(GenericMethodManifest))
            .Then("the interface includes type parameters", result =>
                GetInterfaceSource(result, "Mapper")
                    .Contains("TOut Convert<TIn, TOut>(TIn input);", StringComparison.Ordinal))
            .And("the facade forwards with type parameters", result =>
                GetFacadeSource(result, "Mapper")
                    .Contains("_inner.Convert<TIn, TOut>(input)", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Static members are skipped in generated output")]
    [Fact]
    public Task StaticMembersAreSkipped()
        => Given("a manifest with both static and instance methods",
                () => RunGeneratorWithManifest(StaticMemberManifest))
            .Then("the interface contains the instance method", result =>
                GetInterfaceSource(result, "Helper")
                    .Contains("void InstanceMethod()", StringComparison.Ordinal))
            .And("the interface does not contain the static method", result =>
                !GetInterfaceSource(result, "Helper")
                    .Contains("StaticMethod", StringComparison.Ordinal))
            .And("the facade does not contain the static method", result =>
                !GetFacadeSource(result, "Helper")
                    .Contains("StaticMethod", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Out parameter generates correct modifier in signature and forwarding")]
    [Fact]
    public Task OutParameterGeneratesCorrectModifier()
        => Given("a manifest with an out parameter",
                () => RunGeneratorWithManifest(RefOutManifest))
            .Then("the interface declares the out parameter", result =>
                GetInterfaceSource(result, "Parser")
                    .Contains("bool TryParse(string input, out int result);", StringComparison.Ordinal))
            .And("the facade forwards with the out keyword", result =>
                GetFacadeSource(result, "Parser")
                    .Contains("_inner.TryParse(input, out result)", StringComparison.Ordinal))
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
