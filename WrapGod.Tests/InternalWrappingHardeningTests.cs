using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Internal wrapping hardening — generic, partial, nested, determinism")]
public sealed class InternalWrappingHardeningTests(ITestOutputHelper output) : TinyBddXunitBase(output)
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

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeRef = MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll"));
        var collectionsRef = MetadataReference.CreateFromFile(
            Path.Combine(runtimeDir, "System.Collections.dll"));

        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references.Append(runtimeRef).Append(collectionsRef),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateCompilation(string[] sources, string assemblyName = "TestAssembly")
    {
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var runtimeRef = MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll"));
        var collectionsRef = MetadataReference.CreateFromFile(
            Path.Combine(runtimeDir, "System.Collections.dll"));

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references.Append(runtimeRef).Append(collectionsRef),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    // ── Generic internal type extraction ─────────────────────────────

    private const string GenericSource = @"
namespace TestLib
{
    public class Repository<T> where T : class, new()
    {
        public T GetById(int id) => default!;
        public System.Collections.Generic.List<T> GetAll() => new();
        public void Save(T item) { }
    }

    public class Pair<TKey, TValue>
    {
        public TKey Key { get; set; } = default!;
        public TValue Value { get; set; } = default!;
    }

    public interface ITransformer<in TIn, out TOut>
    {
        TOut Transform(TIn input);
    }
}
";

    [Scenario("Generic internal type extraction captures type parameters and constraints")]
    [Fact]
    public Task GenericTypeExtraction() =>
        Given("a compilation with generic types", () =>
        {
            var compilation = CreateCompilation(GenericSource);
            return CompilationExtractor.Extract(compilation);
        })
        .Then("Repository<T> is extracted", manifest =>
            manifest.Types.Any(t => t.Name == "Repository`1"))
        .And("Repository<T> has one generic parameter", manifest =>
            manifest.Types.First(t => t.Name == "Repository`1").GenericParameters.Count == 1)
        .And("Repository<T> parameter has class and new() constraints", manifest =>
        {
            var gp = manifest.Types.First(t => t.Name == "Repository`1").GenericParameters[0];
            return gp.Constraints.Contains("class") && gp.Constraints.Contains("new()");
        })
        .And("Pair<TKey, TValue> has two generic parameters", manifest =>
            manifest.Types.First(t => t.Name == "Pair`2").GenericParameters.Count == 2)
        .And("ITransformer has in/out variance", manifest =>
        {
            var t = manifest.Types.First(t => t.Name == "ITransformer`2");
            return t.GenericParameters[0].Variance == GenericParameterVariance.In
                && t.GenericParameters[1].Variance == GenericParameterVariance.Out;
        })
        .AssertPassed();

    // ── Partial class extraction ─────────────────────────────────────

    private const string PartialSource1 = @"
namespace TestLib
{
    public partial class UserService
    {
        public string GetName() => ""name"";
    }
}
";

    private const string PartialSource2 = @"
namespace TestLib
{
    public partial class UserService
    {
        public int GetAge() => 30;
        public string Email { get; set; } = """";
    }
}
";

    [Scenario("Partial class extraction merges members from multiple files")]
    [Fact]
    public Task PartialClassExtraction() =>
        Given("a compilation with a partial class split across two files", () =>
        {
            var compilation = CreateCompilation([PartialSource1, PartialSource2]);
            return CompilationExtractor.Extract(compilation);
        })
        .Then("UserService is extracted as a single type", manifest =>
            manifest.Types.Count(t => t.Name == "UserService") == 1)
        .And("UserService has GetName from first partial", manifest =>
            manifest.Types.First(t => t.Name == "UserService")
                .Members.Any(m => m.Name == "GetName"))
        .And("UserService has GetAge from second partial", manifest =>
            manifest.Types.First(t => t.Name == "UserService")
                .Members.Any(m => m.Name == "GetAge"))
        .And("UserService has Email property from second partial", manifest =>
            manifest.Types.First(t => t.Name == "UserService")
                .Members.Any(m => m.Name == "Email" && m.Kind == ApiMemberKind.Property))
        .AssertPassed();

    // ── Nested type extraction ───────────────────────────────────────

    private const string NestedSource = @"
namespace TestLib
{
    public class Outer
    {
        public void OuterMethod() { }

        public class Inner
        {
            public void InnerMethod() { }

            public class DeepNested
            {
                public void DeepMethod() { }
            }
        }

        public enum Status
        {
            Active,
            Inactive
        }
    }
}
";

    [Scenario("Nested type extraction produces types with stable IDs reflecting containment")]
    [Fact]
    public Task NestedTypeExtraction() =>
        Given("a compilation with nested types", () =>
        {
            var compilation = CreateCompilation(NestedSource);
            return CompilationExtractor.Extract(compilation);
        })
        .Then("Outer type is extracted", manifest =>
            manifest.Types.Any(t => t.Name == "Outer"))
        .And("Inner type is extracted", manifest =>
            manifest.Types.Any(t => t.Name == "Inner"))
        .And("Inner stableId contains Outer", manifest =>
            manifest.Types.First(t => t.Name == "Inner").StableId.Contains("Outer+Inner"))
        .And("DeepNested type is extracted", manifest =>
            manifest.Types.Any(t => t.Name == "DeepNested"))
        .And("DeepNested stableId reflects full nesting path", manifest =>
            manifest.Types.First(t => t.Name == "DeepNested").StableId.Contains("Outer+Inner+DeepNested"))
        .And("Nested enum Status is extracted", manifest =>
            manifest.Types.Any(t => t.Name == "Status" && t.Kind == ApiTypeKind.Enum))
        .AssertPassed();

    // ── Merge with AdditionalFiles manifest ──────────────────────────

    private const string AdditionalFileSource = @"
namespace TestLib
{
    public class ServiceA
    {
        public void DoWork() { }
    }

    public class ServiceB
    {
        public int Compute(int x) => x * 2;
    }
}
";

    [Scenario("Extraction filtered by type patterns merges only matching types")]
    [Fact]
    public Task AdditionalFilesManifestMerge() =>
        Given("extraction filtered to ServiceA only via type pattern", () =>
        {
            var compilation = CreateCompilation(AdditionalFileSource);
            return CompilationExtractor.Extract(compilation, typePatterns: ["TestLib.ServiceA"]);
        })
        .Then("only ServiceA is extracted", manifest =>
            manifest.Types.Count == 1 && manifest.Types[0].Name == "ServiceA")
        .And("ServiceA has DoWork method", manifest =>
            manifest.Types[0].Members.Any(m => m.Name == "DoWork"))
        .AssertPassed();

    // ── Deterministic output across repeated runs ────────────────────

    private const string DeterminismSource = @"
namespace TestLib
{
    public class Zeta { public void Z() { } }
    public class Alpha { public void A() { } }
    public class Mu { public void M() { } }
    public interface IBeta { void B(); }
    public struct Gamma { public int G { get; set; } }
}
";

    [Scenario("Deterministic output — repeated extractions produce identical manifests")]
    [Fact]
    public Task DeterministicOutput() =>
        Given("two extractions from the same source", () =>
        {
            var c1 = CreateCompilation(DeterminismSource, "DetTest");
            var c2 = CreateCompilation(DeterminismSource, "DetTest");
            var m1 = CompilationExtractor.Extract(c1);
            var m2 = CompilationExtractor.Extract(c2);
            return (M1: m1, M2: m2);
        })
        .Then("type count matches", r => r.M1.Types.Count == r.M2.Types.Count)
        .And("type order is identical", r =>
            r.M1.Types.Select(t => t.StableId).SequenceEqual(
                r.M2.Types.Select(t => t.StableId)))
        .And("member order within each type is identical", r =>
            r.M1.Types.Zip(r.M2.Types).All(pair =>
                pair.First.Members.Select(m => m.StableId).SequenceEqual(
                    pair.Second.Members.Select(m => m.StableId))))
        .And("source hash is identical", r => r.M1.SourceHash == r.M2.SourceHash)
        .And("types are sorted by StableId (alphabetical)", r =>
        {
            var ids = r.M1.Types.Select(t => t.StableId).ToList();
            var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();
            return ids.SequenceEqual(sorted);
        })
        .AssertPassed();
}
