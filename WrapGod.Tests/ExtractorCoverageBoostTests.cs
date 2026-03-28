using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Extractor;
using WrapGod.Manifest;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Extractor coverage boost: NuGetPackageResolver, CompilationExtractor, ExtractorCache, AssemblyExtractor")]
public sealed class ExtractorCoverageBoostTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static CSharpCompilation CreateCompilation(string source, string assemblyName = "TestAssembly")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
        };

        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string CreateTempCacheDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wrapgod-ecb-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static readonly string CoreLibPath = typeof(object).Assembly.Location;
    private static readonly byte[] MzHeaderStub = [0x4D, 0x5A];

    // ═══════════════════════════════════════════════════════════════════
    //  NuGetPackageResolver
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("NuGetPackageResolver: custom cache root creates directory")]
    [Fact]
    public Task Resolver_CustomCacheRoot()
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "wrapgod-nr-" + Guid.NewGuid().ToString("N")[..8]);
        return Given("a resolver with a custom cache root", () => new NuGetPackageResolver(cacheRoot))
            .Then("GetPackageDirectory returns a path under the cache root", resolver =>
            {
                var dir = resolver.GetPackageDirectory("MyPkg", "1.0.0");
                return dir.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase);
            })
            .And("the package directory contains lowercased package id", resolver =>
            {
                var dir = resolver.GetPackageDirectory("MyPkg", "1.0.0");
                return dir.Contains("mypkg", StringComparison.Ordinal);
            })
            .AssertPassed();
    }

    [Scenario("NuGetPackageResolver: default constructor does not throw")]
    [Fact]
    public Task Resolver_DefaultConstructor()
        => Given("a resolver with default cache root", () => new NuGetPackageResolver())
            .Then("the resolver is not null", resolver => resolver is not null)
            .And("GetPackageDirectory returns a non-empty path", resolver =>
                !string.IsNullOrEmpty(resolver.GetPackageDirectory("Pkg", "1.0")))
            .AssertPassed();

    [Scenario("NuGetPackageResolver: null/empty package id throws ArgumentException")]
    [Fact]
    public Task Resolver_NullPackageId_Throws()
        => Given("a resolver", () => new NuGetPackageResolver(CreateTempCacheDir()))
            .Then("resolving with empty package id throws", (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
            {
                try
                {
                    await resolver.ResolveAsync("", "1.0.0");
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            }))
            .AssertPassed();

    [Scenario("NuGetPackageResolver: null/empty version throws ArgumentException")]
    [Fact]
    public Task Resolver_NullVersion_Throws()
        => Given("a resolver", () => new NuGetPackageResolver(CreateTempCacheDir()))
            .Then("resolving with empty version throws", (Func<NuGetPackageResolver, Task<bool>>)(async resolver =>
            {
                try
                {
                    await resolver.ResolveAsync("SomePkg", "");
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            }))
            .AssertPassed();

    [Scenario("NuGetPackageResolver: TFM selection fallback to first available")]
    [Fact]
    public Task Resolver_TfmFallback_FirstAvailable()
    {
        var cacheRoot = CreateTempCacheDir();
        var resolver = new NuGetPackageResolver(cacheRoot);
        var pkgDir = resolver.GetPackageDirectory("CustomPkg", "2.0.0");

        // Create only a non-preferred TFM.
        var weirdTfm = "netcoreapp3.1";
        var tfmDir = Path.Combine(pkgDir, "lib", weirdTfm);
        Directory.CreateDirectory(tfmDir);
        File.WriteAllBytes(Path.Combine(tfmDir, "CustomPkg.dll"), MzHeaderStub);
        File.WriteAllText(Path.Combine(pkgDir, ".extracted"), "done");

        return Given("a package with only netcoreapp3.1 TFM", () => resolver)
            .Then("ResolveAsync falls back to the only available TFM", (Func<NuGetPackageResolver, Task<bool>>)(async r =>
            {
                var path = await r.ResolveAsync("CustomPkg", "2.0.0");
                return path.Contains(weirdTfm, StringComparison.OrdinalIgnoreCase);
            }))
            .AssertPassed();
    }

    [Scenario("NuGetPackageResolver: explicit TFM that does not exist throws")]
    [Fact]
    public Task Resolver_ExplicitTfm_NotFound_Throws()
    {
        var cacheRoot = CreateTempCacheDir();
        var resolver = new NuGetPackageResolver(cacheRoot);
        var pkgDir = resolver.GetPackageDirectory("TfmPkg", "1.0.0");

        var tfmDir = Path.Combine(pkgDir, "lib", "net8.0");
        Directory.CreateDirectory(tfmDir);
        File.WriteAllBytes(Path.Combine(tfmDir, "TfmPkg.dll"), MzHeaderStub);
        File.WriteAllText(Path.Combine(pkgDir, ".extracted"), "done");

        return Given("a package with net8.0 only", () => resolver)
            .Then("requesting net6.0 explicitly throws InvalidOperationException", (Func<NuGetPackageResolver, Task<bool>>)(async r =>
            {
                try
                {
                    await r.ResolveAsync("TfmPkg", "1.0.0", targetFramework: "net6.0");
                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("net6.0");
                }
            }))
            .AssertPassed();
    }

    [Scenario("NuGetPackageResolver: no lib folder throws")]
    [Fact]
    public Task Resolver_NoLibFolder_Throws()
    {
        var cacheRoot = CreateTempCacheDir();
        var resolver = new NuGetPackageResolver(cacheRoot);
        var pkgDir = resolver.GetPackageDirectory("EmptyPkg", "1.0.0");

        Directory.CreateDirectory(pkgDir);
        File.WriteAllText(Path.Combine(pkgDir, ".extracted"), "done");

        return Given("a package directory with no lib folder", () => resolver)
            .Then("ResolveAsync throws InvalidOperationException", (Func<NuGetPackageResolver, Task<bool>>)(async r =>
            {
                try
                {
                    await r.ResolveAsync("EmptyPkg", "1.0.0");
                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("lib/");
                }
            }))
            .AssertPassed();
    }

    [Scenario("NuGetPackageResolver: empty lib folder (no TFM subdirs) throws")]
    [Fact]
    public Task Resolver_EmptyLibFolder_Throws()
    {
        var cacheRoot = CreateTempCacheDir();
        var resolver = new NuGetPackageResolver(cacheRoot);
        var pkgDir = resolver.GetPackageDirectory("EmptyLibPkg", "1.0.0");

        Directory.CreateDirectory(Path.Combine(pkgDir, "lib"));
        File.WriteAllText(Path.Combine(pkgDir, ".extracted"), "done");

        return Given("a package with an empty lib folder", () => resolver)
            .Then("throws InvalidOperationException about no TFM subfolders", (Func<NuGetPackageResolver, Task<bool>>)(async r =>
            {
                try
                {
                    await r.ResolveAsync("EmptyLibPkg", "1.0.0");
                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("TFM subfolders");
                }
            }))
            .AssertPassed();
    }

    [Scenario("NuGetPackageResolver: TFM dir with no DLLs throws")]
    [Fact]
    public Task Resolver_NoDlls_Throws()
    {
        var cacheRoot = CreateTempCacheDir();
        var resolver = new NuGetPackageResolver(cacheRoot);
        var pkgDir = resolver.GetPackageDirectory("NoDllPkg", "1.0.0");

        var tfmDir = Path.Combine(pkgDir, "lib", "net8.0");
        Directory.CreateDirectory(tfmDir);
        // Don't create any .dll files
        File.WriteAllText(Path.Combine(tfmDir, "readme.txt"), "not a dll");
        File.WriteAllText(Path.Combine(pkgDir, ".extracted"), "done");

        return Given("a TFM directory with no DLL files", () => resolver)
            .Then("throws about no DLLs found", (Func<NuGetPackageResolver, Task<bool>>)(async r =>
            {
                try
                {
                    await r.ResolveAsync("NoDllPkg", "1.0.0");
                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("No DLLs");
                }
            }))
            .AssertPassed();
    }

    [Scenario("NuGetPackageResolver: prefers DLL matching package name")]
    [Fact]
    public Task Resolver_PrefersDllMatchingPackageName()
    {
        var cacheRoot = CreateTempCacheDir();
        var resolver = new NuGetPackageResolver(cacheRoot);
        var pkgDir = resolver.GetPackageDirectory("MyLib", "1.0.0");

        var tfmDir = Path.Combine(pkgDir, "lib", "net8.0");
        Directory.CreateDirectory(tfmDir);
        File.WriteAllBytes(Path.Combine(tfmDir, "Other.dll"), MzHeaderStub);
        File.WriteAllBytes(Path.Combine(tfmDir, "MyLib.dll"), MzHeaderStub);
        File.WriteAllText(Path.Combine(pkgDir, ".extracted"), "done");

        return Given("a package with multiple DLLs including one matching the package name", () => resolver)
            .Then("the primary DLL matches the package name", (Func<NuGetPackageResolver, Task<bool>>)(async r =>
            {
                var path = await r.ResolveAsync("MyLib", "1.0.0");
                return Path.GetFileNameWithoutExtension(path)
                    .Equals("MyLib", StringComparison.OrdinalIgnoreCase);
            }))
            .AssertPassed();
    }

    [Scenario("NuGetPackageResolver: selects netstandard2.1 when net8/7/6 absent")]
    [Fact]
    public Task Resolver_TfmSelection_NetStandard21()
    {
        var cacheRoot = CreateTempCacheDir();
        var resolver = new NuGetPackageResolver(cacheRoot);
        var pkgDir = resolver.GetPackageDirectory("NsPkg", "1.0.0");

        foreach (var tfm in new[] { "netstandard2.0", "netstandard2.1", "net462" })
        {
            var dir = Path.Combine(pkgDir, "lib", tfm);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "NsPkg.dll"), MzHeaderStub);
        }

        File.WriteAllText(Path.Combine(pkgDir, ".extracted"), "done");

        return Given("a package with netstandard2.0, netstandard2.1, net462", () => resolver)
            .Then("selects netstandard2.1 (preferred over 2.0)", (Func<NuGetPackageResolver, Task<bool>>)(async r =>
            {
                var path = await r.ResolveAsync("NsPkg", "1.0.0");
                return path.Contains("netstandard2.1", StringComparison.OrdinalIgnoreCase);
            }))
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  CompilationExtractor: interfaces, enums, delegates, nested, generics
    // ═══════════════════════════════════════════════════════════════════

    private const string AdvancedSource = @"
namespace TestLib
{
    public interface IRepository<out T> where T : class
    {
        T Get(int id);
        System.Collections.Generic.IEnumerable<T> GetAll();
    }

    public enum Color
    {
        Red = 0,
        Green = 1,
        Blue = 2,
    }

    public delegate void EventCallback(object sender, string message);

    public class Outer
    {
        public class Nested
        {
            public int Value { get; set; }
        }
    }

    public abstract class Shape
    {
        public abstract double Area();
        public virtual string Describe() => ""Shape"";
    }

    public class GenericConstrained<TKey, TValue>
        where TKey : struct, System.IComparable<TKey>
        where TValue : class, new()
    {
        public TValue Lookup(TKey key) => default;
    }

    public class WithOperators
    {
        public int Value { get; set; }
        public static WithOperators operator +(WithOperators a, WithOperators b) => new();
        public static implicit operator int(WithOperators w) => w.Value;
    }

    public class WithIndexer
    {
        private readonly int[] _data = new int[10];
        public int this[int index] { get => _data[index]; set => _data[index] = value; }
    }

    public class WithEvents
    {
        public event System.EventHandler? Changed;
        public event System.EventHandler<string>? NameChanged;
    }

    public class WithGenericMethods
    {
        public T Create<T>() where T : new() => new T();
        public void Process<T, U>(T input, U other) where T : notnull { }
    }

    public class WithStaticMembers
    {
        public static int Count { get; set; }
        public static void Reset() { }
    }

    public static class StaticHelper
    {
        public static string Format(int value) => value.ToString();
    }

    public class WithOptionalParams
    {
        public void DoWork(string name, int count = 5, bool verbose = false) { }
        public void DoMany(params string[] items) { }
    }

    public class WithRefOut
    {
        public bool TryParse(string input, out int result) { result = 0; return false; }
        public void Swap(ref int a, ref int b) { (a, b) = (b, a); }
    }

    public class WithFields
    {
        public const int MaxItems = 100;
        public static readonly string DefaultName = ""test"";
    }
}
";

    [Scenario("CompilationExtractor: interfaces extracted with variance")]
    [Fact]
    public Task CompilationExtractor_Interface_Variance()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest from advanced source", () => manifest)
            .Then("IRepository is extracted as Interface", m =>
                m.Types.Any(t => t.Name == "IRepository`1" && t.Kind == ApiTypeKind.Interface))
            .And("IRepository has generic parameter with Out variance", m =>
            {
                var repo = m.Types.First(t => t.Name == "IRepository`1");
                return repo.GenericParameters.Count == 1
                    && repo.GenericParameters[0].Name == "T"
                    && repo.GenericParameters[0].Variance == GenericParameterVariance.Out;
            })
            .And("IRepository generic parameter has class constraint", m =>
            {
                var repo = m.Types.First(t => t.Name == "IRepository`1");
                return repo.GenericParameters[0].Constraints.Contains("class");
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: enum type extracted")]
    [Fact]
    public Task CompilationExtractor_Enum()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("Color is extracted as Enum", m =>
                m.Types.Any(t => t.Name == "Color" && t.Kind == ApiTypeKind.Enum))
            .And("Color has three fields", m =>
            {
                var color = m.Types.First(t => t.Name == "Color");
                var fields = color.Members.Where(mem => mem.Kind == ApiMemberKind.Field).ToList();
                return fields.Count >= 3;
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: delegate type extracted")]
    [Fact]
    public Task CompilationExtractor_Delegate()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("EventCallback is extracted as Delegate", m =>
                m.Types.Any(t => t.Name == "EventCallback" && t.Kind == ApiTypeKind.Delegate))
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: nested types extracted")]
    [Fact]
    public Task CompilationExtractor_NestedType()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("Outer is present", m => m.Types.Any(t => t.Name == "Outer"))
            .And("Nested type is also present", m =>
                m.Types.Any(t => t.Name == "Nested"))
            .And("Nested type stable ID contains Outer", m =>
                m.Types.First(t => t.Name == "Nested").StableId.Contains("Outer"))
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: abstract members extracted")]
    [Fact]
    public Task CompilationExtractor_AbstractMembers()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("Shape is abstract", m =>
            {
                var shape = m.Types.First(t => t.Name == "Shape");
                return shape.IsAbstract;
            })
            .And("Shape.Area is abstract", m =>
            {
                var shape = m.Types.First(t => t.Name == "Shape");
                var area = shape.Members.First(mem => mem.Name == "Area");
                return area.IsAbstract;
            })
            .And("Shape.Describe is virtual", m =>
            {
                var shape = m.Types.First(t => t.Name == "Shape");
                var describe = shape.Members.First(mem => mem.Name == "Describe");
                return describe.IsVirtual;
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: generic type with constraints")]
    [Fact]
    public Task CompilationExtractor_GenericConstraints()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("GenericConstrained has two generic parameters", m =>
            {
                var gc = m.Types.First(t => t.Name == "GenericConstrained`2");
                return gc.GenericParameters.Count == 2;
            })
            .And("TKey has struct constraint", m =>
            {
                var gc = m.Types.First(t => t.Name == "GenericConstrained`2");
                var tkey = gc.GenericParameters.First(p => p.Name == "TKey");
                return tkey.Constraints.Contains("struct");
            })
            .And("TValue has class and new() constraints", m =>
            {
                var gc = m.Types.First(t => t.Name == "GenericConstrained`2");
                var tvalue = gc.GenericParameters.First(p => p.Name == "TValue");
                return tvalue.Constraints.Contains("class") && tvalue.Constraints.Contains("new()");
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: operators extracted")]
    [Fact]
    public Task CompilationExtractor_Operators()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("WithOperators has operator members", m =>
            {
                var wo = m.Types.First(t => t.Name == "WithOperators");
                return wo.Members.Any(mem => mem.Kind == ApiMemberKind.Operator);
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: indexer extracted")]
    [Fact]
    public Task CompilationExtractor_Indexer()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("WithIndexer has an Indexer member", m =>
            {
                var wi = m.Types.First(t => t.Name == "WithIndexer");
                return wi.Members.Any(mem => mem.Kind == ApiMemberKind.Indexer);
            })
            .And("the indexer has a getter and setter", m =>
            {
                var wi = m.Types.First(t => t.Name == "WithIndexer");
                var indexer = wi.Members.First(mem => mem.Kind == ApiMemberKind.Indexer);
                return indexer.HasGetter && indexer.HasSetter;
            })
            .And("the indexer has parameters", m =>
            {
                var wi = m.Types.First(t => t.Name == "WithIndexer");
                var indexer = wi.Members.First(mem => mem.Kind == ApiMemberKind.Indexer);
                return indexer.Parameters.Count > 0;
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: events extracted")]
    [Fact]
    public Task CompilationExtractor_Events()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("WithEvents has Event members", m =>
            {
                var we = m.Types.First(t => t.Name == "WithEvents");
                return we.Members.Any(mem => mem.Kind == ApiMemberKind.Event);
            })
            .And("Changed event is present", m =>
            {
                var we = m.Types.First(t => t.Name == "WithEvents");
                return we.Members.Any(mem => mem.Kind == ApiMemberKind.Event && mem.Name == "Changed");
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: generic methods")]
    [Fact]
    public Task CompilationExtractor_GenericMethods()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("WithGenericMethods.Create is a generic method", m =>
            {
                var wgm = m.Types.First(t => t.Name == "WithGenericMethods");
                var create = wgm.Members.First(mem => mem.Name == "Create");
                return create.IsGenericMethod && create.GenericParameters.Count == 1;
            })
            .And("Create has new() constraint", m =>
            {
                var wgm = m.Types.First(t => t.Name == "WithGenericMethods");
                var create = wgm.Members.First(mem => mem.Name == "Create");
                return create.GenericParameters[0].Constraints.Contains("new()");
            })
            .And("Process has notnull constraint", m =>
            {
                var wgm = m.Types.First(t => t.Name == "WithGenericMethods");
                var process = wgm.Members.First(mem => mem.Name == "Process");
                return process.GenericParameters.Count == 2
                    && process.GenericParameters[0].Constraints.Contains("notnull");
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: static members extracted correctly")]
    [Fact]
    public Task CompilationExtractor_StaticMembers()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("StaticHelper is static", m =>
                m.Types.First(t => t.Name == "StaticHelper").IsStatic)
            .And("WithStaticMembers.Count is static property", m =>
            {
                var wsm = m.Types.First(t => t.Name == "WithStaticMembers");
                var count = wsm.Members.First(mem => mem.Name == "Count");
                return count.IsStatic;
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: optional and params parameters")]
    [Fact]
    public Task CompilationExtractor_OptionalAndParams()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("DoWork has optional parameters", m =>
            {
                var wop = m.Types.First(t => t.Name == "WithOptionalParams");
                var doWork = wop.Members.First(mem => mem.Name == "DoWork");
                return doWork.Parameters.Any(p => p.IsOptional);
            })
            .And("DoMany has params parameter", m =>
            {
                var wop = m.Types.First(t => t.Name == "WithOptionalParams");
                var doMany = wop.Members.First(mem => mem.Name == "DoMany");
                return doMany.Parameters.Any(p => p.IsParams);
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: ref and out parameters")]
    [Fact]
    public Task CompilationExtractor_RefOut()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("TryParse has out parameter", m =>
            {
                var wro = m.Types.First(t => t.Name == "WithRefOut");
                var tryParse = wro.Members.First(mem => mem.Name == "TryParse");
                return tryParse.Parameters.Any(p => p.IsOut);
            })
            .And("Swap has ref parameters", m =>
            {
                var wro = m.Types.First(t => t.Name == "WithRefOut");
                var swap = wro.Members.First(mem => mem.Name == "Swap");
                return swap.Parameters.All(p => p.IsRef);
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: fields including constants")]
    [Fact]
    public Task CompilationExtractor_Fields()
    {
        var compilation = CreateCompilation(AdvancedSource);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("WithFields has MaxItems field", m =>
            {
                var wf = m.Types.First(t => t.Name == "WithFields");
                return wf.Members.Any(mem => mem.Kind == ApiMemberKind.Field && mem.Name == "MaxItems");
            })
            .And("WithFields has DefaultName field", m =>
            {
                var wf = m.Types.First(t => t.Name == "WithFields");
                return wf.Members.Any(mem => mem.Kind == ApiMemberKind.Field && mem.Name == "DefaultName");
            })
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: namespace filter applied")]
    [Fact]
    public Task CompilationExtractor_NamespaceFilter()
    {
        var source = @"
namespace Alpha { public class A { } }
namespace Beta { public class B { } }
";
        var compilation = CreateCompilation(source);
        var manifest = CompilationExtractor.Extract(compilation, namespacePatterns: ["Alpha"]);

        return Given("a manifest filtered by namespace Alpha", () => manifest)
            .Then("only Alpha types included", m => m.Types.All(t => t.Namespace == "Alpha"))
            .And("A is present", m => m.Types.Any(t => t.Name == "A"))
            .And("B is excluded", m => !m.Types.Any(t => t.Name == "B"))
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: type pattern filter (exact)")]
    [Fact]
    public Task CompilationExtractor_TypePatternFilter()
    {
        var source = @"
namespace Ns { public class Foo { } public class Bar { } }
";
        var compilation = CreateCompilation(source);
        var manifest = CompilationExtractor.Extract(compilation, typePatterns: ["Ns.Foo"]);

        return Given("a manifest filtered by type pattern Ns.Foo", () => manifest)
            .Then("only Foo is included", m => m.Types.Count == 1 && m.Types[0].Name == "Foo")
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: type pattern filter (wildcard)")]
    [Fact]
    public Task CompilationExtractor_TypePatternWildcard()
    {
        var source = @"
namespace Ns { public class FooService { } public class FooRepo { } public class BarService { } }
";
        var compilation = CreateCompilation(source);
        var manifest = CompilationExtractor.Extract(compilation, typePatterns: ["Ns.Foo*"]);

        return Given("a manifest filtered by wildcard Ns.Foo*", () => manifest)
            .Then("FooService is included", m => m.Types.Any(t => t.Name == "FooService"))
            .And("FooRepo is included", m => m.Types.Any(t => t.Name == "FooRepo"))
            .And("BarService is excluded", m => !m.Types.Any(t => t.Name == "BarService"))
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: null assembly name handled")]
    [Fact]
    public Task CompilationExtractor_NullAssemblyName()
    {
        var source = "public class Orphan { }";
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var compilation = CSharpCompilation.Create(
            null,  // null assembly name
            [syntaxTree],
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest from a compilation with null assembly name", () => manifest)
            .Then("assembly name defaults to Unknown", m => m.Assembly.Name == "Unknown")
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: source hash is deterministic")]
    [Fact]
    public Task CompilationExtractor_SourceHashDeterministic()
    {
        var source = "namespace X { public class Y { } }";
        var c1 = CreateCompilation(source);
        var c2 = CreateCompilation(source);
        var m1 = CompilationExtractor.Extract(c1);
        var m2 = CompilationExtractor.Extract(c2);

        return Given("two manifests from identical compilations", () => (m1, m2))
            .Then("source hashes are identical", pair => pair.m1.SourceHash == pair.m2.SourceHash)
            .AssertPassed();
    }

    [Scenario("CompilationExtractor: constructors extracted")]
    [Fact]
    public Task CompilationExtractor_Constructors()
    {
        var source = @"
namespace Ns
{
    public class WithCtors
    {
        public WithCtors() { }
        public WithCtors(int x) { }
    }
}
";
        var compilation = CreateCompilation(source);
        var manifest = CompilationExtractor.Extract(compilation);

        return Given("a manifest", () => manifest)
            .Then("WithCtors has constructor members", m =>
            {
                var wc = m.Types.First(t => t.Name == "WithCtors");
                return wc.Members.Count(mem => mem.Kind == ApiMemberKind.Constructor) >= 2;
            })
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ExtractorCache: version mismatch, corrupt file, store + retrieve
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("ExtractorCache: version mismatch returns null")]
    [Fact]
    public Task ExtractorCache_VersionMismatch()
    {
        var cacheDir = CreateTempCacheDir();
        try
        {
            var cache = new ExtractorCache(cacheDir);
            var manifest = AssemblyExtractor.Extract(CoreLibPath);
            cache.Store(CoreLibPath, manifest);

            // Tamper with the extractor version in the cache
            var cacheFiles = Directory.GetFiles(cacheDir, "*.json");
            foreach (var file in cacheFiles)
            {
                var content = File.ReadAllText(file);
                content = content.Replace("\"extractorVersion\":\"1\"", "\"extractorVersion\":\"999\"");
                File.WriteAllText(file, content);
            }

            var result = cache.TryGetCached(CoreLibPath);
            return Given("a cache entry with mismatched extractor version", () => result)
                .Then("TryGetCached returns null", r => r is null)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    [Scenario("ExtractorCache: corrupt JSON returns null")]
    [Fact]
    public Task ExtractorCache_CorruptJson()
    {
        var cacheDir = CreateTempCacheDir();
        try
        {
            var cache = new ExtractorCache(cacheDir);
            var manifest = AssemblyExtractor.Extract(CoreLibPath);
            cache.Store(CoreLibPath, manifest);

            // Corrupt all cache files
            var cacheFiles = Directory.GetFiles(cacheDir, "*.json");
            foreach (var file in cacheFiles)
            {
                File.WriteAllText(file, "{{{{not valid json at all!!!");
            }

            var result = cache.TryGetCached(CoreLibPath);
            return Given("a cache entry with corrupt JSON", () => result)
                .Then("TryGetCached returns null gracefully", r => r is null)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    [Scenario("ExtractorCache: Invalidate on non-existent entry does not throw")]
    [Fact]
    public Task ExtractorCache_InvalidateNonExistent()
    {
        var cacheDir = CreateTempCacheDir();
        try
        {
            var cache = new ExtractorCache(cacheDir);
            // Should not throw
            cache.Invalidate("/nonexistent/path/to/assembly.dll");

            return Given("invalidation of non-existent entry", () => true)
                .Then("no exception thrown", success => success)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    [Scenario("ExtractorCache: default constructor uses current directory")]
    [Fact]
    public Task ExtractorCache_DefaultConstructor()
        => Given("a cache with default directory", () => new ExtractorCache())
            .Then("the cache is not null", cache => cache is not null)
            .AssertPassed();

    [Scenario("ExtractorCache: TryGetCached on empty cache returns null")]
    [Fact]
    public Task ExtractorCache_EmptyCache_ReturnsNull()
    {
        var cacheDir = CreateTempCacheDir();
        try
        {
            var cache = new ExtractorCache(cacheDir);
            var result = cache.TryGetCached(CoreLibPath);

            return Given("a TryGetCached on empty cache", () => result)
                .Then("returns null", r => r is null)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  AssemblyExtractor: file not found, with cache, operators, events
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("AssemblyExtractor: file not found throws FileNotFoundException")]
    [Fact]
    public Task AssemblyExtractor_FileNotFound()
        => Given("a non-existent assembly path", () => "/nonexistent/assembly.dll")
            .Then("Extract throws FileNotFoundException", path =>
            {
                try
                {
                    AssemblyExtractor.Extract(path);
                    return false;
                }
                catch (FileNotFoundException)
                {
                    return true;
                }
            })
            .AssertPassed();

    [Scenario("AssemblyExtractor: file not found with useCache throws")]
    [Fact]
    public Task AssemblyExtractor_FileNotFound_WithCache()
        => Given("a non-existent assembly path", () => "/nonexistent/assembly.dll")
            .Then("Extract with useCache throws FileNotFoundException", path =>
            {
                try
                {
                    AssemblyExtractor.Extract(path, useCache: true);
                    return false;
                }
                catch (FileNotFoundException)
                {
                    return true;
                }
            })
            .AssertPassed();

    [Scenario("AssemblyExtractor: ExtractWithCache disabled falls back to direct extract")]
    [Fact]
    public Task AssemblyExtractor_ExtractWithCache_Disabled()
    {
        var options = new ExtractorCacheOptions { Enabled = false };
        var manifest = AssemblyExtractor.ExtractWithCache(CoreLibPath, options);

        return Given("an extraction with cache disabled", () => manifest)
            .Then("manifest is still produced", m => m is not null && m.Types.Count > 0)
            .AssertPassed();
    }

    [Scenario("AssemblyExtractor: ExtractWithCache enabled works")]
    [Fact]
    public Task AssemblyExtractor_ExtractWithCache_Enabled()
    {
        var cacheDir = CreateTempCacheDir();
        try
        {
            var options = new ExtractorCacheOptions
            {
                Enabled = true,
                SharedCacheRoot = cacheDir,
                ProjectCacheIndexRoot = Path.Combine(cacheDir, "index"),
            };
            var m1 = AssemblyExtractor.ExtractWithCache(CoreLibPath, options);
            var m2 = AssemblyExtractor.ExtractWithCache(CoreLibPath, options);

            return Given("two cached extractions", () => (m1, m2))
                .Then("both produce valid manifests", pair =>
                    pair.m1.Types.Count > 0 && pair.m2.Types.Count > 0)
                .And("assembly names match", pair =>
                    pair.m1.Assembly.Name == pair.m2.Assembly.Name)
                .AssertPassed();
        }
        finally
        {
            if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true);
        }
    }

    [Scenario("AssemblyExtractor: default options extraction")]
    [Fact]
    public Task AssemblyExtractor_DefaultOptions()
    {
        var manifest = AssemblyExtractor.ExtractWithCache(CoreLibPath);

        return Given("extraction with default options", () => manifest)
            .Then("manifest is produced", m => m is not null && m.Types.Count > 0)
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ExtractorCacheKey, ExtractorCacheOptions
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("ExtractorCacheKey: CreateForAssembly with non-existent file throws")]
    [Fact]
    public Task CacheKey_NonExistentFile_Throws()
        => Given("a non-existent file path", () => "/nonexistent/file.dll")
            .Then("CreateForAssembly throws FileNotFoundException", path =>
            {
                try
                {
                    ExtractorCacheKey.CreateForAssembly(path, ExtractorCacheOptions.Default);
                    return false;
                }
                catch (FileNotFoundException)
                {
                    return true;
                }
            })
            .AssertPassed();

    [Scenario("ExtractorCacheKey: deterministic hash for same assembly")]
    [Fact]
    public Task CacheKey_DeterministicHash()
    {
        var key1 = ExtractorCacheKey.CreateForAssembly(CoreLibPath, ExtractorCacheOptions.Default);
        var key2 = ExtractorCacheKey.CreateForAssembly(CoreLibPath, ExtractorCacheOptions.Default);

        return Given("two keys for the same assembly", () => (key1, key2))
            .Then("hashes are identical", pair => pair.key1.ComputeHash() == pair.key2.ComputeHash())
            .And("canonical JSON is identical", pair => pair.key1.ToCanonicalJson() == pair.key2.ToCanonicalJson())
            .AssertPassed();
    }

    [Scenario("ExtractorCacheOptions: Default has expected values")]
    [Fact]
    public Task CacheOptions_Defaults()
        => Given("default ExtractorCacheOptions", () => ExtractorCacheOptions.Default)
            .Then("Enabled is true", o => o.Enabled)
            .And("PublicOnly is true", o => o.PublicOnly)
            .And("IncludeObsoleteDetails is false", o => !o.IncludeObsoleteDetails)
            .And("Custom is empty", o => o.Custom.Count == 0)
            .And("ProjectCacheIndexRoot is null", o => o.ProjectCacheIndexRoot is null)
            .And("SharedCacheRoot is null", o => o.SharedCacheRoot is null)
            .AssertPassed();

    [Scenario("ExtractorCacheKey: custom options produce different hash")]
    [Fact]
    public Task CacheKey_DifferentOptions_DifferentHash()
    {
        var defaultOptions = ExtractorCacheOptions.Default;
        var customOptions = new ExtractorCacheOptions
        {
            PublicOnly = false,
            IncludeObsoleteDetails = true,
            Custom = new Dictionary<string, string?> { ["myKey"] = "myValue" },
        };

        var key1 = ExtractorCacheKey.CreateForAssembly(CoreLibPath, defaultOptions);
        var key2 = ExtractorCacheKey.CreateForAssembly(CoreLibPath, customOptions);

        return Given("two keys with different options", () => (key1, key2))
            .Then("hashes differ", pair => pair.key1.ComputeHash() != pair.key2.ComputeHash())
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ExtractorCacheContracts model tests
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("ExtractorCacheEnvelope properties")]
    [Fact]
    public Task CacheEnvelope_Properties()
    {
        var key = ExtractorCacheKey.CreateForAssembly(CoreLibPath, ExtractorCacheOptions.Default);
        var envelope = new ExtractorCacheEnvelope
        {
            CacheKeyHash = "abc123",
            CacheKey = key,
            CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Manifest = new ApiManifest { SchemaVersion = "1.0" },
        };

        return Given("an ExtractorCacheEnvelope", () => envelope)
            .Then("CacheKeyHash is set", e => e.CacheKeyHash == "abc123")
            .And("CacheKey is set", e => e.CacheKey is not null)
            .And("Manifest is set", e => e.Manifest.SchemaVersion == "1.0")
            .AssertPassed();
    }

    [Scenario("ExtractorCacheIndexRecord properties")]
    [Fact]
    public Task CacheIndexRecord_Properties()
    {
        var record = new ExtractorCacheIndexRecord
        {
            CacheKeyHash = "hash1",
            PayloadHash = "payload1",
            CreatedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };

        return Given("an ExtractorCacheIndexRecord", () => record)
            .Then("CacheKeyHash is set", r => r.CacheKeyHash == "hash1")
            .And("PayloadHash is set", r => r.PayloadHash == "payload1")
            .AssertPassed();
    }
}
