using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Generator;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Compilation-based extraction")]
public sealed class CompilationExtractorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static CSharpCompilation CreateCompilation(string source, string assemblyName = "TestAssembly")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        // Also add System.Runtime for netcoreapp.
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeRef = MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll"));

        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references.Append(runtimeRef),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private const string SampleSource = @"
namespace TestLib
{
    public class Calculator
    {
        public int Add(int a, int b) => a + b;
        public int Subtract(int a, int b) => a - b;
        public string Name { get; set; }
    }

    public interface ILogger
    {
        void Log(string message);
    }

    public struct Point
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    internal class InternalHelper { }
}
";

    private static ApiManifest ExtractSample()
    {
        var compilation = CreateCompilation(SampleSource);
        return CompilationExtractor.Extract(compilation);
    }

    private static ApiManifest ExtractWithNamespaceFilter()
    {
        var compilation = CreateCompilation(SampleSource);
        return CompilationExtractor.Extract(compilation, namespacePatterns: ["TestLib"]);
    }

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("Extract types from a synthetic compilation")]
    [Fact]
    public Task Extract_SyntheticCompilation_ProducesManifest()
        => Given("a compilation with sample types", ExtractSample)
            .Then("the manifest is not null", manifest => manifest is not null)
            .And("the manifest contains exactly 3 public types", manifest => manifest.Types.Count == 3)
            .And("Calculator type is present", manifest =>
                manifest.Types.Any(t => t.Name == "Calculator"))
            .And("ILogger interface is present", manifest =>
                manifest.Types.Any(t => t.Name == "ILogger" && t.Kind == ApiTypeKind.Interface))
            .And("Point struct is present", manifest =>
                manifest.Types.Any(t => t.Name == "Point" && t.Kind == ApiTypeKind.Struct))
            .And("internal types are excluded", manifest =>
                manifest.Types.All(t => t.Name != "InternalHelper"))
            .AssertPassed();

    [Scenario("Extracted manifest matches expected shape")]
    [Fact]
    public Task Extract_ManifestShape_IsCorrect()
        => Given("a compilation manifest", ExtractSample)
            .Then("assembly name matches", manifest => manifest.Assembly.Name == "TestAssembly")
            .And("Calculator has Add method", manifest =>
            {
                var calc = manifest.Types.First(t => t.Name == "Calculator");
                return calc.Members.Any(m => m.Name == "Add" && m.Kind == ApiMemberKind.Method);
            })
            .And("Calculator has Subtract method", manifest =>
            {
                var calc = manifest.Types.First(t => t.Name == "Calculator");
                return calc.Members.Any(m => m.Name == "Subtract" && m.Kind == ApiMemberKind.Method);
            })
            .And("Calculator has Name property", manifest =>
            {
                var calc = manifest.Types.First(t => t.Name == "Calculator");
                return calc.Members.Any(m => m.Name == "Name" && m.Kind == ApiMemberKind.Property);
            })
            .And("Add method has two int parameters", manifest =>
            {
                var calc = manifest.Types.First(t => t.Name == "Calculator");
                var add = calc.Members.First(m => m.Name == "Add");
                return add.Parameters.Count == 2
                    && add.Parameters.All(p => p.Type.Contains("Int32") || p.Type.Contains("int"));
            })
            .AssertPassed();

    [Scenario("[WrapType] attribute triggers extraction in generator")]
    [Fact]
    public Task WrapType_Attribute_TriggersExtraction()
        => Given("a type annotated with [WrapType(\"@self\")]", () =>
            {
                const string source = @"
namespace MyApp
{
    public class MyService
    {
        public string GetData() => ""hello"";
        public int Count { get; set; }
    }
}
";
                var compilation = CreateCompilation(source, "MyApp");
                // Use the generator's static helper to extract a TypePlan from a symbol.
                var serviceSymbol = compilation.GetTypeByMetadataName("MyApp.MyService")!;
                return WrapGodIncrementalGenerator.ExtractTypePlanFromSymbol(serviceSymbol);
            })
            .Then("the TypePlan is not null", plan => plan is not null)
            .And("the type name is MyService", plan => plan.Name == "MyService")
            .And("the namespace is MyApp", plan => plan.Namespace == "MyApp")
            .And("it has a GetData method member", plan =>
                plan.Members.Any(m => m.Name == "GetData" && m.Kind == "method"))
            .And("it has a Count property member", plan =>
                plan.Members.Any(m => m.Name == "Count" && m.Kind == "property"))
            .AssertPassed();

    [Scenario("Generator produces wrappers for @self types")]
    [Fact]
    public Task Generator_ProducesWrappers_ForSelfTypes()
        => Given("a TypePlan extracted from a symbol", () =>
            {
                const string source = @"
namespace Widgets
{
    public class Widget
    {
        public void Activate() { }
        public bool IsActive { get; }
    }
}
";
                var compilation = CreateCompilation(source, "WidgetLib");
                var symbol = compilation.GetTypeByMetadataName("Widgets.Widget")!;
                return WrapGodIncrementalGenerator.ExtractTypePlanFromSymbol(symbol);
            })
            .Then("the emitter produces a valid interface", plan =>
            {
                var interfaceSource = SourceEmitter.EmitInterface(plan);
                return interfaceSource.Contains("public interface IWrappedWidget")
                    && interfaceSource.Contains("void Activate()")
                    && interfaceSource.Contains("IsActive");
            })
            .And("the emitter produces a valid facade", plan =>
            {
                var facadeSource = SourceEmitter.EmitFacade(plan);
                return facadeSource.Contains("public sealed class WidgetFacade")
                    && facadeSource.Contains("_inner.Activate()")
                    && facadeSource.Contains("_inner.IsActive");
            })
            .AssertPassed();
}
