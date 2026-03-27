using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Generator;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Generator skeleton")]
public sealed class GeneratorSkeletonTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static GeneratorDriverRunResult RunGenerator()
    {
        var syntaxTree = CSharpSyntaxTree.ParseText("namespace Demo; public class Marker { }");
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "Demo",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        IIncrementalGenerator incrementalGenerator = new WrapGodIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(incrementalGenerator);

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    [Scenario("incremental generator emits skeleton metadata source")]
    [Fact]
    public Task IncrementalGeneratorEmitsSkeletonMetadataSource()
        => Given("the generator run result", RunGenerator)
            .Then("at least one source is generated", result => result.Results.SelectMany(r => r.GeneratedSources).Any())
            .And("generated file includes generator metadata type", result =>
                result.Results
                    .SelectMany(r => r.GeneratedSources)
                    .Any(s => s.SourceText.ToString().Contains("WrapGodGeneratorMetadata", StringComparison.Ordinal)))
            .AssertPassed();
}
