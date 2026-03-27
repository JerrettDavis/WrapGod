using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Generic manifest extraction")]
public sealed class GenericManifestExtractionTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Extractor captures generic parameters, constraints, variance, and open generic flags")]
    [Fact]
    public Task Extract_CapturesGenericMetadata()
        => Given("a compiled assembly with representative generic types and methods", BuildGenericFixtureManifest)
            .Then("type generic variance and positions are captured", manifest =>
            {
                var covariant = manifest.Types.Single(t => t.StableId == "Fixture.ICovariant`1");
                var contravariant = manifest.Types.Single(t => t.StableId == "Fixture.IContravariant`1");

                return covariant.IsGenericType
                    && covariant.IsGenericTypeDefinition
                    && !covariant.IsConstructedGenericType
                    && covariant.ContainsGenericParameters
                    && covariant.GenericParameters.Single().Variance == GenericParameterVariance.Out
                    && covariant.GenericParameters.Single().Position == 0
                    && contravariant.GenericParameters.Single().Variance == GenericParameterVariance.In;
            })
            .And("special and type constraints are captured deterministically", manifest =>
            {
                var genericSample = manifest.Types.Single(t => t.StableId == "Fixture.GenericSample`3");
                var tClass = genericSample.GenericParameters.Single(p => p.Name == "TClass");
                var tStruct = genericSample.GenericParameters.Single(p => p.Name == "TStruct");
                var tConstrained = genericSample.GenericParameters.Single(p => p.Name == "TConstrained");

                return tClass.Constraints.Contains("class")
                    && tClass.Constraints.Contains("new()")
                    && tStruct.Constraints.Contains("struct")
                    && tConstrained.Constraints.Contains("System.IDisposable");
            })
            .And("generic method metadata and constraints are captured", manifest =>
            {
                var genericSample = manifest.Types.Single(t => t.StableId == "Fixture.GenericSample`3");
                var method = genericSample.Members.Single(m => m.StableId.StartsWith("Fixture.GenericSample`3.Transform`2(", StringComparison.Ordinal));

                var tIn = method.GenericParameters.Single(p => p.Name == "TIn");
                var tOut = method.GenericParameters.Single(p => p.Name == "TOut");

                return method.IsGenericMethod
                    && method.IsGenericMethodDefinition
                    && !method.IsConstructedGenericMethod
                    && method.ContainsGenericParameters
                    && tIn.Position == 0
                    && tOut.Position == 1
                    && tOut.Constraints.Contains("System.IDisposable")
                    && tOut.Constraints.Contains("new()");
            })
            .AssertPassed();

    [Scenario("Multi-version merge preserves generic metadata from canonical nodes")]
    [Fact]
    public Task Merge_PreservesGenericMetadata()
        => Given("two manifests with changed generic metadata for the same type/member", BuildMergedResultWithGenericMetadata)
            .Then("merged type keeps latest generic parameter metadata", result =>
            {
                var type = result.MergedManifest.Types.Single(t => t.StableId == "Fixture.Box`1");
                var gp = type.GenericParameters.Single();
                return gp.Name == "T"
                    && gp.Position == 0
                    && gp.Variance == GenericParameterVariance.Out
                    && gp.Constraints.SequenceEqual(["System.IDisposable", "class"])
                    && type.IsGenericTypeDefinition
                    && type.ContainsGenericParameters;
            })
            .And("merged member keeps latest generic method metadata", result =>
            {
                var member = result.MergedManifest.Types.Single(t => t.StableId == "Fixture.Box`1")
                    .Members.Single(m => m.StableId == "Fixture.Box`1.Map`1(T)");
                var gp = member.GenericParameters.Single();
                return member.IsGenericMethod
                    && member.IsGenericMethodDefinition
                    && !member.IsConstructedGenericMethod
                    && member.ContainsGenericParameters
                    && gp.Variance == GenericParameterVariance.None
                    && gp.Constraints.SequenceEqual(["new()"]);
            })
            .AssertPassed();

    private static ApiManifest BuildGenericFixtureManifest()
    {
        const string source = """
namespace Fixture;

public interface ICovariant<out T> { }
public interface IContravariant<in T> { }

public class GenericSample<TClass, TStruct, TConstrained>
    where TClass : class, new()
    where TStruct : struct
    where TConstrained : System.IDisposable
{
    public TOut Transform<TIn, TOut>(TIn input)
        where TOut : System.IDisposable, new()
    {
        return new TOut();
    }
}
""";

        var assemblyPath = CompileToTempAssembly(source);
        try
        {
            return AssemblyExtractor.Extract(assemblyPath);
        }
        finally
        {
            File.Delete(assemblyPath);
        }
    }

    private static MultiVersionExtractor.MultiVersionResult BuildMergedResultWithGenericMetadata()
    {
        var v1 = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "Fixture.Box`1",
                    FullName = "Fixture.Box<T>",
                    Name = "Box`1",
                    Namespace = "Fixture",
                    Kind = ApiTypeKind.Interface,
                    IsGenericType = true,
                    IsGenericTypeDefinition = true,
                    ContainsGenericParameters = true,
                    GenericParameters = [new GenericParameterInfo { Name = "T", Position = 0, Variance = GenericParameterVariance.None, Constraints = ["class"] }],
                    Members =
                    [
                        new ApiMemberNode
                        {
                            StableId = "Fixture.Box`1.Map`1(T)",
                            Name = "Map",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "U",
                            Parameters = [new ApiParameterInfo { Name = "value", Type = "T" }],
                            IsGenericMethod = true,
                            IsGenericMethodDefinition = true,
                            ContainsGenericParameters = true,
                            GenericParameters = [new GenericParameterInfo { Name = "U", Position = 0, Constraints = [] }],
                        },
                    ],
                },
            ],
        };

        var v2 = new ApiManifest
        {
            Types =
            [
                new ApiTypeNode
                {
                    StableId = "Fixture.Box`1",
                    FullName = "Fixture.Box<T>",
                    Name = "Box`1",
                    Namespace = "Fixture",
                    Kind = ApiTypeKind.Interface,
                    IsGenericType = true,
                    IsGenericTypeDefinition = true,
                    ContainsGenericParameters = true,
                    GenericParameters = [new GenericParameterInfo { Name = "T", Position = 0, Variance = GenericParameterVariance.Out, Constraints = ["System.IDisposable", "class"] }],
                    Members =
                    [
                        new ApiMemberNode
                        {
                            StableId = "Fixture.Box`1.Map`1(T)",
                            Name = "Map",
                            Kind = ApiMemberKind.Method,
                            ReturnType = "U",
                            Parameters = [new ApiParameterInfo { Name = "value", Type = "T" }],
                            IsGenericMethod = true,
                            IsGenericMethodDefinition = true,
                            ContainsGenericParameters = true,
                            GenericParameters = [new GenericParameterInfo { Name = "U", Position = 0, Constraints = ["new()"] }],
                        },
                    ],
                },
            ],
        };

        return MultiVersionExtractor.Merge([("1.0.0", v1), ("2.0.0", v2)]);
    }

    private static string CompileToTempAssembly(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IDisposable).Assembly.Location),
        };

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var netStandardPath = Path.Combine(runtimeDir, "netstandard.dll");
        if (File.Exists(netStandardPath))
        {
            references.Add(MetadataReference.CreateFromFile(netStandardPath));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName: $"WrapGod.GenericFixture.{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: references.DistinctBy(r => r.Display, StringComparer.Ordinal).ToList(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var outputPath = Path.Combine(Path.GetTempPath(), $"WrapGod.GenericFixture.{Guid.NewGuid():N}.dll");
        var emitResult = compilation.Emit(outputPath);
        if (!emitResult.Success)
        {
            var errors = string.Join(Environment.NewLine, emitResult.Diagnostics.Select(d => d.ToString()));
            throw new InvalidOperationException($"Failed to compile generic fixture assembly:{Environment.NewLine}{errors}");
        }

        return outputPath;
    }
}
