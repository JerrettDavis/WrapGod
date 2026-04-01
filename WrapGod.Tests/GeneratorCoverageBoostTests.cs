using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Generator;
using Xunit.Abstractions;
using GenerationPlan = WrapGod.Generator.GenerationPlan;

namespace WrapGod.Tests;

[Feature("Generator coverage boost: SourceEmitter, config, CompatibilityFilter, model equality")]
public sealed class GeneratorCoverageBoostTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static GeneratorDriverRunResult RunGenerator(
        string manifest, string? config = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            "namespace Placeholder; public class Marker { }");
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
            new InMemoryAdditionalText("test.wrapgod.json", manifest),
        };

        if (config != null)
        {
            additionalTexts.Add(
                new InMemoryAdditionalText("test.wrapgod.config.json", config));
        }

        IIncrementalGenerator generator = new WrapGodIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            additionalTexts: additionalTexts.ToArray());

        driver = driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static List<string> AllGeneratedHintNames(GeneratorDriverRunResult result)
        => result.Results.SelectMany(r => r.GeneratedSources).Select(s => s.HintName).ToList();

    private static string GetSource(GeneratorDriverRunResult result, string hintName)
        => result.Results
            .SelectMany(r => r.GeneratedSources)
            .First(s => s.HintName == hintName)
            .SourceText.ToString();

    // ── Manifests ────────────────────────────────────────────────────

    private static readonly string ManifestVoidMethod = """
        {
          "assembly": { "name": "Lib" },
          "types": [
            {
              "fullName": "Lib.Worker",
              "name": "Worker",
              "namespace": "Lib",
              "members": [
                {
                  "name": "Execute",
                  "kind": "method",
                  "returnType": "void",
                  "parameters": [],
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

    private static readonly string ManifestGenericMethod = """
        {
          "assembly": { "name": "Lib" },
          "types": [
            {
              "fullName": "Lib.Factory",
              "name": "Factory",
              "namespace": "Lib",
              "members": [
                {
                  "name": "Create",
                  "kind": "method",
                  "returnType": "T",
                  "parameters": [{ "name": "name", "type": "string" }],
                  "genericParameters": [{ "name": "T" }],
                  "isStatic": false,
                  "hasGetter": false,
                  "hasSetter": false
                }
              ]
            }
          ]
        }
        """;

    private static readonly string ManifestOutParam = """
        {
          "assembly": { "name": "Lib" },
          "types": [
            {
              "fullName": "Lib.Parser",
              "name": "Parser",
              "namespace": "Lib",
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

    private static readonly string ManifestMixedStaticInstance = """
        {
          "assembly": { "name": "Lib" },
          "types": [
            {
              "fullName": "Lib.Mixed",
              "name": "Mixed",
              "namespace": "Lib",
              "members": [
                {
                  "name": "StaticMethod",
                  "kind": "method",
                  "returnType": "void",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": true,
                  "hasGetter": false,
                  "hasSetter": false
                },
                {
                  "name": "InstanceMethod",
                  "kind": "method",
                  "returnType": "string",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false,
                  "hasGetter": false,
                  "hasSetter": false
                },
                {
                  "name": "StaticProp",
                  "kind": "property",
                  "returnType": "int",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": true,
                  "hasGetter": true,
                  "hasSetter": false
                },
                {
                  "name": "InstanceProp",
                  "kind": "property",
                  "returnType": "int",
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

    // ── Scenarios: Void return method ────────────────────────────────

    [Scenario("Void return method emitted correctly in interface and facade")]
    [Fact]
    public Task VoidReturnMethod()
        => Given("a manifest with a void method",
                () => RunGenerator(ManifestVoidMethod))
            .Then("the interface contains void Execute()", result =>
                GetSource(result, "IWrappedWorker.g.cs")
                    .Contains("void Execute()", StringComparison.Ordinal))
            .And("the facade delegates to _inner.Execute()", result =>
                GetSource(result, "WorkerFacade.g.cs")
                    .Contains("_inner.Execute()", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenarios: Generic method ────────────────────────────────────

    [Scenario("Generic method emits type parameter in interface")]
    [Fact]
    public Task GenericMethod_Interface()
        => Given("a manifest with a generic method",
                () => RunGenerator(ManifestGenericMethod))
            .Then("the interface contains generic Create<T>", result =>
                GetSource(result, "IWrappedFactory.g.cs")
                    .Contains("Create<T>(", StringComparison.Ordinal))
            .And("the facade forwards with type parameter", result =>
                GetSource(result, "FactoryFacade.g.cs")
                    .Contains("_inner.Create<T>(", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenarios: Out parameter ─────────────────────────────────────

    [Scenario("Out parameter forwarded in facade")]
    [Fact]
    public Task OutParameterForwarded()
        => Given("a manifest with out parameter",
                () => RunGenerator(ManifestOutParam))
            .Then("the interface contains out parameter", result =>
                GetSource(result, "IWrappedParser.g.cs")
                    .Contains("out int result", StringComparison.Ordinal))
            .And("the facade forwards with out keyword", result =>
                GetSource(result, "ParserFacade.g.cs")
                    .Contains("out result", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenarios: Mixed static and instance ─────────────────────────

    [Scenario("Mixed static/instance members: only instance members emitted")]
    [Fact]
    public Task MixedStaticInstance()
        => Given("a manifest with static and instance members",
                () => RunGenerator(ManifestMixedStaticInstance))
            .Then("interface does not contain StaticMethod", result =>
                !GetSource(result, "IWrappedMixed.g.cs")
                    .Contains("StaticMethod", StringComparison.Ordinal))
            .And("interface does not contain StaticProp", result =>
                !GetSource(result, "IWrappedMixed.g.cs")
                    .Contains("StaticProp", StringComparison.Ordinal))
            .And("interface contains InstanceMethod", result =>
                GetSource(result, "IWrappedMixed.g.cs")
                    .Contains("InstanceMethod", StringComparison.Ordinal))
            .And("interface contains InstanceProp", result =>
                GetSource(result, "IWrappedMixed.g.cs")
                    .Contains("InstanceProp", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenarios: Config with generic type renames ──────────────────

    [Scenario("Config renames generic type")]
    [Fact]
    public Task ConfigRenamesGenericType()
    {
        var manifest = """
            {
              "assembly": { "name": "Lib" },
              "types": [
                {
                  "fullName": "Lib.Cache",
                  "name": "Cache",
                  "namespace": "Lib",
                  "genericParameters": [{ "name": "T", "constraints": [] }],
                  "members": [
                    {
                      "name": "Get",
                      "kind": "method",
                      "returnType": "T",
                      "parameters": [{ "name": "key", "type": "string" }],
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
        var config = """
            {
              "types": [
                {
                  "sourceType": "Lib.Cache",
                  "include": true,
                  "targetName": "AppCache"
                }
              ]
            }
            """;

        return Given("a generic type with config rename",
                () => RunGenerator(manifest, config))
            .Then("interface uses renamed name IWrappedAppCache", result =>
                AllGeneratedHintNames(result).Contains("IWrappedAppCache.g.cs"))
            .And("facade uses renamed name AppCacheFacade", result =>
                AllGeneratedHintNames(result).Contains("AppCacheFacade.g.cs"))
            .And("generic parameter is preserved", result =>
                GetSource(result, "IWrappedAppCache.g.cs")
                    .Contains("<T>", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenarios: Config member-level edge cases ────────────────────

    [Scenario("Config excludes one member, renames another")]
    [Fact]
    public Task ConfigMemberExcludeAndRename()
    {
        var manifest = """
            {
              "assembly": { "name": "Lib" },
              "types": [
                {
                  "fullName": "Lib.Svc",
                  "name": "Svc",
                  "namespace": "Lib",
                  "members": [
                    {
                      "name": "Alpha",
                      "kind": "method",
                      "returnType": "void",
                      "parameters": [],
                      "genericParameters": [],
                      "isStatic": false,
                      "hasGetter": false,
                      "hasSetter": false
                    },
                    {
                      "name": "Beta",
                      "kind": "method",
                      "returnType": "int",
                      "parameters": [],
                      "genericParameters": [],
                      "isStatic": false,
                      "hasGetter": false,
                      "hasSetter": false
                    },
                    {
                      "name": "Gamma",
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
        var config = """
            {
              "types": [
                {
                  "sourceType": "Lib.Svc",
                  "include": true,
                  "members": [
                    { "sourceMember": "Alpha", "include": false },
                    { "sourceMember": "Beta", "include": true, "targetName": "BetaRenamed" }
                  ]
                }
              ]
            }
            """;

        return Given("a manifest with config: exclude Alpha, rename Beta",
                () => RunGenerator(manifest, config))
            .Then("Alpha is not in the interface", result =>
                !GetSource(result, "IWrappedSvc.g.cs")
                    .Contains("Alpha", StringComparison.Ordinal))
            .And("BetaRenamed is in the interface", result =>
                GetSource(result, "IWrappedSvc.g.cs")
                    .Contains("BetaRenamed", StringComparison.Ordinal))
            .And("Gamma remains unchanged", result =>
                GetSource(result, "IWrappedSvc.g.cs")
                    .Contains("Gamma", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenarios: CompatibilityFilter ───────────────────────────────

    [Scenario("CompatibilityFilter LCD mode: only common members")]
    [Fact]
    public Task CompatibilityFilter_Lcd()
    {
        var members = new List<MemberPlan>
        {
            new("Common", "method", "void", new List<ParameterPlan>(),
                false, false, introducedIn: "1.0"),
            new("V2Only", "method", "void", new List<ParameterPlan>(),
                false, false, introducedIn: "2.0"),
            new("Removed", "method", "void", new List<ParameterPlan>(),
                false, false, introducedIn: "1.0", removedIn: "3.0"),
        };
        var type = new TypePlan("Lib.Svc", "Svc", "Lib", members, introducedIn: "1.0");
        var plan = new GenerationPlan("Lib", new List<TypePlan> { type });
        var versions = new List<string> { "1.0", "2.0", "3.0" };

        return Given("a plan filtered with LCD mode",
                () => CompatibilityFilter.Apply(plan, CompatibilityMode.Lcd, versions))
            .Then("Common is included (introduced at earliest, never removed)", result =>
                result.Types[0].Members.Any(m => m.Name == "Common"))
            .And("V2Only is excluded (not introduced at earliest)", result =>
                !result.Types[0].Members.Any(m => m.Name == "V2Only"))
            .And("Removed is excluded (has removedIn)", result =>
                !result.Types[0].Members.Any(m => m.Name == "Removed"))
            .AssertPassed();
    }

    [Scenario("CompatibilityFilter Targeted mode: version-specific")]
    [Fact]
    public Task CompatibilityFilter_Targeted()
    {
        var members = new List<MemberPlan>
        {
            new("V1", "method", "void", new List<ParameterPlan>(),
                false, false, introducedIn: "1.0"),
            new("V2", "method", "void", new List<ParameterPlan>(),
                false, false, introducedIn: "2.0"),
            new("V3", "method", "void", new List<ParameterPlan>(),
                false, false, introducedIn: "3.0"),
            new("Removed", "method", "void", new List<ParameterPlan>(),
                false, false, introducedIn: "1.0", removedIn: "2.0"),
        };
        var type = new TypePlan("Lib.Svc", "Svc", "Lib", members, introducedIn: "1.0");
        var plan = new GenerationPlan("Lib", new List<TypePlan> { type });
        var versions = new List<string> { "1.0", "2.0", "3.0" };

        return Given("a plan filtered with Targeted mode for version 2.0",
                () => CompatibilityFilter.Apply(plan, CompatibilityMode.Targeted, versions, "2.0"))
            .Then("V1 is included", result =>
                result.Types[0].Members.Any(m => m.Name == "V1"))
            .And("V2 is included", result =>
                result.Types[0].Members.Any(m => m.Name == "V2"))
            .And("V3 is excluded (introduced after target)", result =>
                !result.Types[0].Members.Any(m => m.Name == "V3"))
            .And("Removed is excluded (removed at or before target)", result =>
                !result.Types[0].Members.Any(m => m.Name == "Removed"))
            .AssertPassed();
    }

    [Scenario("CompatibilityFilter Adaptive mode: keeps all")]
    [Fact]
    public Task CompatibilityFilter_Adaptive()
    {
        var members = new List<MemberPlan>
        {
            new("V1", "method", "void", new List<ParameterPlan>(),
                false, false, introducedIn: "1.0"),
            new("V3", "method", "void", new List<ParameterPlan>(),
                false, false, introducedIn: "3.0"),
        };
        var type = new TypePlan("Lib.Svc", "Svc", "Lib", members, introducedIn: "1.0");
        var plan = new GenerationPlan("Lib", new List<TypePlan> { type });
        var versions = new List<string> { "1.0", "2.0", "3.0" };

        return Given("a plan filtered with Adaptive mode",
                () => CompatibilityFilter.Apply(plan, CompatibilityMode.Adaptive, versions))
            .Then("all members are kept", result =>
                result.Types[0].Members.Count == 2)
            .AssertPassed();
    }

    [Scenario("CompatibilityFilter: null plan throws")]
    [Fact]
    public Task CompatibilityFilter_NullPlan_Throws()
        => Given("null plan", () => (object?)null)
            .Then("Apply throws ArgumentNullException", _ =>
            {
                try
                {
                    CompatibilityFilter.Apply(null!, CompatibilityMode.Lcd, new List<string> { "1.0" });
                    return false;
                }
                catch (ArgumentNullException)
                {
                    return true;
                }
            })
            .AssertPassed();

    [Scenario("CompatibilityFilter: empty versions throws")]
    [Fact]
    public Task CompatibilityFilter_EmptyVersions_Throws()
        => Given("empty versions", () => (object?)null)
            .Then("Apply throws ArgumentException", _ =>
            {
                try
                {
                    var plan = new GenerationPlan("Lib", new List<TypePlan>());
                    CompatibilityFilter.Apply(plan, CompatibilityMode.Lcd, new List<string>());
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            })
            .AssertPassed();

    [Scenario("CompatibilityFilter: Targeted without target version throws")]
    [Fact]
    public Task CompatibilityFilter_Targeted_NoTarget_Throws()
        => Given("targeted mode without target", () => (object?)null)
            .Then("Apply throws ArgumentException", _ =>
            {
                try
                {
                    var plan = new GenerationPlan("Lib", new List<TypePlan>());
                    CompatibilityFilter.Apply(plan, CompatibilityMode.Targeted, new List<string> { "1.0" });
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            })
            .AssertPassed();

    [Scenario("CompatibilityFilter: LCD filters out type introduced later")]
    [Fact]
    public Task CompatibilityFilter_Lcd_TypeIntroducedLater()
    {
        var type1 = new TypePlan("Lib.A", "A", "Lib", new List<MemberPlan>(), introducedIn: "1.0");
        var type2 = new TypePlan("Lib.B", "B", "Lib", new List<MemberPlan>(), introducedIn: "2.0");
        var plan = new GenerationPlan("Lib", new List<TypePlan> { type1, type2 });
        var versions = new List<string> { "1.0", "2.0" };

        return Given("two types, one introduced later",
                () => CompatibilityFilter.Apply(plan, CompatibilityMode.Lcd, versions))
            .Then("only type A survives LCD", result =>
                result.Types.Count == 1 && result.Types[0].Name == "A")
            .AssertPassed();
    }

    [Scenario("CompatibilityFilter: Targeted filters out type not present")]
    [Fact]
    public Task CompatibilityFilter_Targeted_TypeNotPresent()
    {
        var type1 = new TypePlan("Lib.A", "A", "Lib", new List<MemberPlan>(), introducedIn: "1.0");
        var type2 = new TypePlan("Lib.B", "B", "Lib", new List<MemberPlan>(), introducedIn: "3.0");
        var plan = new GenerationPlan("Lib", new List<TypePlan> { type1, type2 });
        var versions = new List<string> { "1.0", "2.0", "3.0" };

        return Given("type B introduced in 3.0, targeting 2.0",
                () => CompatibilityFilter.Apply(plan, CompatibilityMode.Targeted, versions, "2.0"))
            .Then("B is excluded", result =>
                result.Types.Count == 1 && result.Types[0].Name == "A")
            .AssertPassed();
    }

    // ── Scenarios: Adaptive mode version guard combinations ──────────

    [Scenario("Adaptive mode: read-only property with version guard")]
    [Fact]
    public Task AdaptiveMode_ReadOnlyProperty_Guard()
    {
        var members = new List<MemberPlan>
        {
            new("Name", "property", "string",
                new List<ParameterPlan>(),
                hasGetter: true, hasSetter: false, isStatic: false,
                genericParameters: new List<GenericTypeParameterPlan>(),
                introducedIn: "2.0"),
        };
        var type = new TypePlan("Lib.Svc", "Svc", "Lib", members);

        bool prev = SourceEmitter.AdaptiveMode;
        SourceEmitter.AdaptiveMode = true;
        try
        {
            var facade = SourceEmitter.EmitFacade(type);
            return Given("adaptive facade with read-only property", () => facade)
                .Then("has version guard in getter", src =>
                    src.Contains("IsMemberAvailable") && src.Contains("get"))
                .And("does not have a setter block", src =>
                    !src.Contains("set =>") && !src.Contains("set\n"))
                .AssertPassed();
        }
        finally
        {
            SourceEmitter.AdaptiveMode = prev;
        }
    }

    // ── Scenarios: Model equality edge cases ─────────────────────────

    [Scenario("TypePlan equality with different generic parameters")]
    [Fact]
    public Task TypePlan_Equality_DifferentGenerics()
    {
        var t1 = new TypePlan("Lib.A", "A", "Lib", new List<MemberPlan>(),
            genericTypeParameters: new List<GenericTypeParameterPlan>
            {
                new("T", new List<string> { "class" }),
            });
        var t2 = new TypePlan("Lib.A", "A", "Lib", new List<MemberPlan>(),
            genericTypeParameters: new List<GenericTypeParameterPlan>
            {
                new("T", new List<string> { "struct" }),
            });

        return Given("two TypePlans with different generic constraints", () => (t1, t2))
            .Then("they are not equal", pair => !pair.t1.Equals(pair.t2))
            .AssertPassed();
    }

    [Scenario("MemberPlan equality with different parameters")]
    [Fact]
    public Task MemberPlan_Equality_DifferentParams()
    {
        var m1 = new MemberPlan("Foo", "method", "void",
            new List<ParameterPlan> { new("a", "int") },
            false, false);
        var m2 = new MemberPlan("Foo", "method", "void",
            new List<ParameterPlan> { new("a", "string") },
            false, false);

        return Given("two MemberPlans with different parameter types", () => (m1, m2))
            .Then("they are not equal", pair => !pair.m1.Equals(pair.m2))
            .AssertPassed();
    }

    [Scenario("ConfigPlan equality")]
    [Fact]
    public Task ConfigPlan_Equality()
    {
        var c1 = WrapGodIncrementalGenerator.ParseConfig("""
            { "types": [{ "sourceType": "A", "include": true }] }
            """)!;
        var c2 = WrapGodIncrementalGenerator.ParseConfig("""
            { "types": [{ "sourceType": "A", "include": true }] }
            """)!;
        var c3 = WrapGodIncrementalGenerator.ParseConfig("""
            { "types": [{ "sourceType": "B", "include": true }] }
            """)!;

        return Given("config plans", () => (c1, c2, c3))
            .Then("identical configs are equal", t => t.c1.Equals(t.c2))
            .And("different configs are not equal", t => !t.c1.Equals(t.c3))
            .And("identical configs have same hash code", t => t.c1.GetHashCode() == t.c2.GetHashCode())
            .And("Equals(null) returns false", t => !t.c1.Equals((ConfigPlan?)null))
            .And("Equals(object) works", t => t.c1.Equals((object)t.c2))
            .AssertPassed();
    }

    [Scenario("ConfigTypePlan equality")]
    [Fact]
    public Task ConfigTypePlan_Equality()
    {
        var ct1 = new ConfigTypePlan("A", true, null, new List<ConfigMemberPlan>());
        var ct2 = new ConfigTypePlan("A", true, null, new List<ConfigMemberPlan>());
        var ct3 = new ConfigTypePlan("A", true, "Renamed", new List<ConfigMemberPlan>());

        return Given("ConfigTypePlans", () => (ct1, ct2, ct3))
            .Then("identical are equal", t => t.ct1.Equals(t.ct2))
            .And("different are not equal", t => !t.ct1.Equals(t.ct3))
            .And("Equals(null) returns false", t => !t.ct1.Equals((ConfigTypePlan?)null))
            .And("Equals(object) works", t => t.ct1.Equals((object)t.ct2))
            .And("hash codes match for equal", t => t.ct1.GetHashCode() == t.ct2.GetHashCode())
            .AssertPassed();
    }

    [Scenario("ConfigMemberPlan equality")]
    [Fact]
    public Task ConfigMemberPlan_Equality()
    {
        var cm1 = new ConfigMemberPlan("Foo", true, null);
        var cm2 = new ConfigMemberPlan("Foo", true, null);
        var cm3 = new ConfigMemberPlan("Foo", false, null);

        return Given("ConfigMemberPlans", () => (cm1, cm2, cm3))
            .Then("identical are equal", t => t.cm1.Equals(t.cm2))
            .And("different include are not equal", t => !t.cm1.Equals(t.cm3))
            .And("Equals(null) returns false", t => !t.cm1.Equals((ConfigMemberPlan?)null))
            .And("Equals(object) works", t => t.cm1.Equals((object)t.cm2))
            .AssertPassed();
    }

    [Scenario("ParameterPlan equality")]
    [Fact]
    public Task ParameterPlan_Equality()
    {
        var p1 = new ParameterPlan("a", "int", "ref");
        var p2 = new ParameterPlan("a", "int", "ref");
        var p3 = new ParameterPlan("a", "int", "out");

        return Given("ParameterPlans", () => (p1, p2, p3))
            .Then("identical are equal", t => t.p1.Equals(t.p2))
            .And("different modifier are not equal", t => !t.p1.Equals(t.p3))
            .And("Equals(null) returns false", t => !t.p1.Equals((ParameterPlan?)null))
            .And("Equals(object) works", t => t.p1.Equals((object)t.p2))
            .And("hash codes match for equal", t => t.p1.GetHashCode() == t.p2.GetHashCode())
            .AssertPassed();
    }

    [Scenario("GenericTypeParameterPlan equality")]
    [Fact]
    public Task GenericTypeParameterPlan_Equality()
    {
        var g1 = new GenericTypeParameterPlan("T", new List<string> { "class" });
        var g2 = new GenericTypeParameterPlan("T", new List<string> { "class" });
        var g3 = new GenericTypeParameterPlan("T", new List<string> { "struct" });
        var g4 = new GenericTypeParameterPlan("U");

        return Given("GenericTypeParameterPlans", () => (g1, g2, g3, g4))
            .Then("identical are equal", t => t.g1.Equals(t.g2))
            .And("different constraints are not equal", t => !t.g1.Equals(t.g3))
            .And("different names are not equal", t => !t.g1.Equals(t.g4))
            .And("Equals(null) returns false", t => !t.g1.Equals((GenericTypeParameterPlan?)null))
            .And("Equals(object) works", t => t.g1.Equals((object)t.g2))
            .And("hash codes match for equal", t => t.g1.GetHashCode() == t.g2.GetHashCode())
            .AssertPassed();
    }

    [Scenario("MemberPlan with generic parameters equality")]
    [Fact]
    public Task MemberPlan_GenericParams_Equality()
    {
        var m1 = new MemberPlan("Foo", "method", "void",
            new List<ParameterPlan>(), false, false,
            genericParameters: new List<GenericTypeParameterPlan> { new("T", Array.Empty<string>()) });
        var m2 = new MemberPlan("Foo", "method", "void",
            new List<ParameterPlan>(), false, false,
            genericParameters: new List<GenericTypeParameterPlan> { new("T", Array.Empty<string>()) });
        var m3 = new MemberPlan("Foo", "method", "void",
            new List<ParameterPlan>(), false, false,
            genericParameters: new List<GenericTypeParameterPlan> { new("U", Array.Empty<string>()) });

        return Given("MemberPlans with generic parameters", () => (m1, m2, m3))
            .Then("identical are equal", t => t.m1.Equals(t.m2))
            .And("different generic params are not equal", t => !t.m1.Equals(t.m3))
            .AssertPassed();
    }

    [Scenario("GenerationPlan: different assembly names are not equal")]
    [Fact]
    public Task GenerationPlan_DifferentAssemblyName()
    {
        var p1 = new GenerationPlan("Lib1", new List<TypePlan>());
        var p2 = new GenerationPlan("Lib2", new List<TypePlan>());

        return Given("two GenerationPlans with different assembly names", () => (p1, p2))
            .Then("they are not equal", pair => !pair.p1.Equals(pair.p2))
            .AssertPassed();
    }

    [Scenario("ParseConfig with empty types array")]
    [Fact]
    public Task ParseConfig_EmptyTypes()
        => Given("config with empty types", () => WrapGodIncrementalGenerator.ParseConfig("""
                { "types": [] }
                """))
            .Then("result is not null with zero types", c => c != null && c.Types.Count == 0)
            .AssertPassed();

    [Scenario("ParseConfig with member targetName")]
    [Fact]
    public Task ParseConfig_MemberTargetName()
        => Given("config with member targetName", () => WrapGodIncrementalGenerator.ParseConfig("""
                {
                  "types": [{
                    "sourceType": "Svc",
                    "include": true,
                    "targetName": "MySvc",
                    "members": [
                      { "sourceMember": "Run", "include": true, "targetName": "Go" }
                    ]
                  }]
                }
                """))
            .Then("type has correct target name", c =>
                c != null && c.Types[0].TargetName == "MySvc")
            .And("member has correct target name", c =>
                c != null && c.Types[0].Members[0].TargetName == "Go")
            .AssertPassed();

    [Scenario("CompatibilityFilter: member with no version metadata passes LCD")]
    [Fact]
    public Task CompatibilityFilter_NoVersionMetadata_PassesLcd()
    {
        var members = new List<MemberPlan>
        {
            new("NoMeta", "method", "void", new List<ParameterPlan>(),
                false, false),
        };
        var type = new TypePlan("Lib.A", "A", "Lib", members);
        var plan = new GenerationPlan("Lib", new List<TypePlan> { type });
        var versions = new List<string> { "1.0", "2.0" };

        return Given("member with no version metadata in LCD mode",
                () => CompatibilityFilter.Apply(plan, CompatibilityMode.Lcd, versions))
            .Then("member is included", result =>
                result.Types[0].Members.Any(m => m.Name == "NoMeta"))
            .AssertPassed();
    }

    [Scenario("CompatibilityFilter: unknown target version in Targeted mode")]
    [Fact]
    public Task CompatibilityFilter_UnknownTargetVersion()
    {
        var members = new List<MemberPlan>
        {
            new("M1", "method", "void", new List<ParameterPlan>(),
                false, false, introducedIn: "1.0"),
        };
        var type = new TypePlan("Lib.A", "A", "Lib", members, introducedIn: "1.0");
        var plan = new GenerationPlan("Lib", new List<TypePlan> { type });
        var versions = new List<string> { "1.0", "2.0" };

        return Given("target version not in list",
                () => CompatibilityFilter.Apply(plan, CompatibilityMode.Targeted, versions, "9.9"))
            .Then("type is excluded since target not found", result =>
                result.Types.Count == 0)
            .AssertPassed();
    }

    // ── In-memory AdditionalText ─────────────────────────────────────

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
