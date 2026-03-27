using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Abstractions.Config;
using WrapGod.Generator;
using Xunit.Abstractions;
using GenerationPlan = WrapGod.Generator.GenerationPlan;

namespace WrapGod.Tests;

[Feature("Generator coverage boost 2: WrapGodIncrementalGenerator pipeline, TypePlan/MemberPlan edge cases")]
public sealed class GeneratorCoverageBoost2Tests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static GeneratorDriverRunResult RunGeneratorWithAttribute(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(typeof(WrapTypeAttribute).Assembly.Location),
        };

        // Add netstandard if present
        var netstdPath = Path.Combine(runtimeDir, "netstandard.dll");
        if (File.Exists(netstdPath))
            references.Add(MetadataReference.CreateFromFile(netstdPath));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        IIncrementalGenerator generator = new WrapGodIncrementalGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() });

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

    // ── Scenarios: [WrapType] attribute with @self ────────────────────

    [Scenario("WrapType(@self) attribute triggers code generation")]
    [Fact]
    public Task WrapType_Self_GeneratesWrapper()
    {
        var source = """
            using WrapGod.Abstractions.Config;

            namespace MyApp
            {
                [WrapType("@self")]
                public class MyService
                {
                    public string GetData() => "hello";
                    public int Count { get; set; }
                }
            }
            """;

        return Given("a type annotated with [WrapType(\"@self\")]",
                () => RunGeneratorWithAttribute(source))
            .Then("wrapper interface is generated", result =>
                AllGeneratedHintNames(result).Contains("IWrappedMyService.g.cs"))
            .And("facade is generated", result =>
                AllGeneratedHintNames(result).Contains("MyServiceFacade.g.cs"))
            .And("interface contains GetData", result =>
                GetSource(result, "IWrappedMyService.g.cs")
                    .Contains("GetData", StringComparison.Ordinal))
            .And("interface contains Count property", result =>
                GetSource(result, "IWrappedMyService.g.cs")
                    .Contains("Count", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("WrapType with Include=false generates nothing")]
    [Fact]
    public Task WrapType_IncludeFalse_NoGeneration()
    {
        var source = """
            using WrapGod.Abstractions.Config;

            namespace MyApp
            {
                [WrapType("@self", Include = false)]
                public class Excluded
                {
                    public void Do() { }
                }
            }
            """;

        return Given("a type with Include=false", () => RunGeneratorWithAttribute(source))
            .Then("no IWrappedExcluded generated", result =>
                !AllGeneratedHintNames(result).Any(n => n.Contains("Excluded")))
            .AssertPassed();
    }

    [Scenario("WrapType with TargetName produces renamed wrapper")]
    [Fact]
    public Task WrapType_TargetName_RenamedWrapper()
    {
        var source = """
            using WrapGod.Abstractions.Config;

            namespace MyApp
            {
                [WrapType("@self", TargetName = "CustomName")]
                public class Original
                {
                    public void Act() { }
                }
            }
            """;

        return Given("a type with TargetName=CustomName",
                () => RunGeneratorWithAttribute(source))
            .Then("uses custom name for interface", result =>
                AllGeneratedHintNames(result).Contains("IWrappedCustomName.g.cs"))
            .And("uses custom name for facade", result =>
                AllGeneratedHintNames(result).Contains("CustomNameFacade.g.cs"))
            .AssertPassed();
    }

    [Scenario("WrapType with a named external type from compilation")]
    [Fact]
    public Task WrapType_ExternalType()
    {
        var source = """
            using WrapGod.Abstractions.Config;

            namespace Ext { public class Api { public int Run() => 0; } }
            namespace MyApp
            {
                [WrapType("Ext.Api")]
                public class MyWrapper { }
            }
            """;

        return Given("WrapType referencing an external type by metadata name",
                () => RunGeneratorWithAttribute(source))
            .Then("wrapper for Api is generated", result =>
                AllGeneratedHintNames(result).Contains("IWrappedApi.g.cs"))
            .And("facade delegates to Ext.Api", result =>
                GetSource(result, "ApiFacade.g.cs")
                    .Contains("Ext.Api", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("WrapType referencing non-existent type generates nothing")]
    [Fact]
    public Task WrapType_NonExistentType_NoOutput()
    {
        var source = """
            using WrapGod.Abstractions.Config;

            namespace MyApp
            {
                [WrapType("NonExistent.Type")]
                public class Wrapper { }
            }
            """;

        return Given("WrapType referencing a type that doesn't exist",
                () => RunGeneratorWithAttribute(source))
            .Then("no output for the non-existent type", result =>
                !AllGeneratedHintNames(result).Any(n => n.Contains("Type")))
            .AssertPassed();
    }

    // ── Scenarios: TypePlan equality edge cases ──────────────────────

    [Scenario("TypePlan with different members count not equal")]
    [Fact]
    public Task TypePlan_DifferentMembersCount()
    {
        var t1 = new TypePlan("A", "A", "N",
            new List<MemberPlan> { new("M", "method", "void", new List<ParameterPlan>(), false, false) });
        var t2 = new TypePlan("A", "A", "N", new List<MemberPlan>());

        return Given("two TypePlans with different member counts", () => (t1, t2))
            .Then("not equal", pair => !pair.t1.Equals(pair.t2))
            .AssertPassed();
    }

    [Scenario("TypePlan with different introduced/removed versions")]
    [Fact]
    public Task TypePlan_DifferentVersions()
    {
        var t1 = new TypePlan("A", "A", "N", new List<MemberPlan>(), introducedIn: "1.0");
        var t2 = new TypePlan("A", "A", "N", new List<MemberPlan>(), introducedIn: "2.0");
        var t3 = new TypePlan("A", "A", "N", new List<MemberPlan>(), introducedIn: "1.0", removedIn: "3.0");

        return Given("TypePlans with different version metadata", () => (t1, t2, t3))
            .Then("different introduced not equal", t => !t.t1.Equals(t.t2))
            .And("different removed not equal", t => !t.t1.Equals(t.t3))
            .AssertPassed();
    }

    [Scenario("TypePlan EffectiveName uses TargetName when set")]
    [Fact]
    public Task TypePlan_EffectiveName()
    {
        var t1 = new TypePlan("A", "A", "N", new List<MemberPlan>());
        var t2 = new TypePlan("A", "A", "N", new List<MemberPlan>(), targetName: "B");

        return Given("TypePlans with/without target name", () => (t1, t2))
            .Then("without target: EffectiveName is Name", t => t.t1.EffectiveName == "A")
            .And("with target: EffectiveName is TargetName", t => t.t2.EffectiveName == "B")
            .AssertPassed();
    }

    // ── Scenarios: MemberPlan equality edge cases ────────────────────

    [Scenario("MemberPlan with different kind, returnType, getter/setter")]
    [Fact]
    public Task MemberPlan_EqualityEdgeCases()
    {
        var m1 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false);
        var m2 = new MemberPlan("M", "property", "void", new List<ParameterPlan>(), false, false);
        var m3 = new MemberPlan("M", "method", "int", new List<ParameterPlan>(), false, false);
        var m4 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), true, false);
        var m5 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, true);
        var m6 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false, isStatic: true);

        return Given("MemberPlans with various differences", () => (m1, m2, m3, m4, m5, m6))
            .Then("different kind not equal", t => !t.m1.Equals(t.m2))
            .And("different return type not equal", t => !t.m1.Equals(t.m3))
            .And("different hasGetter not equal", t => !t.m1.Equals(t.m4))
            .And("different hasSetter not equal", t => !t.m1.Equals(t.m5))
            .And("different isStatic not equal", t => !t.m1.Equals(t.m6))
            .AssertPassed();
    }

    [Scenario("MemberPlan EffectiveName uses TargetName when set")]
    [Fact]
    public Task MemberPlan_EffectiveName()
    {
        var m1 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false);
        var m2 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false, targetName: "N");

        return Given("MemberPlans with/without target name", () => (m1, m2))
            .Then("without: EffectiveName is Name", t => t.m1.EffectiveName == "M")
            .And("with: EffectiveName is TargetName", t => t.m2.EffectiveName == "N")
            .AssertPassed();
    }

    [Scenario("MemberPlan with version metadata")]
    [Fact]
    public Task MemberPlan_VersionMetadata()
    {
        var m1 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false, introducedIn: "1.0");
        var m2 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false, introducedIn: "2.0");
        var m3 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false, introducedIn: "1.0", removedIn: "3.0");

        return Given("MemberPlans with different versions", () => (m1, m2, m3))
            .Then("different introduced not equal", t => !t.m1.Equals(t.m2))
            .And("different removed not equal", t => !t.m1.Equals(t.m3))
            .And("hash codes differ", t => t.m1.GetHashCode() != t.m3.GetHashCode())
            .AssertPassed();
    }

    // ── Scenarios: ConfigTypePlan equality deeper ────────────────────

    [Scenario("ConfigTypePlan with different member count")]
    [Fact]
    public Task ConfigTypePlan_DifferentMemberCount()
    {
        var ct1 = new ConfigTypePlan("A", true, null, new List<ConfigMemberPlan>());
        var ct2 = new ConfigTypePlan("A", true, null,
            new List<ConfigMemberPlan> { new("M", true, null) });

        return Given("ConfigTypePlans with different member counts", () => (ct1, ct2))
            .Then("not equal", pair => !pair.ct1.Equals(pair.ct2))
            .AssertPassed();
    }

    [Scenario("ConfigTypePlan with different include")]
    [Fact]
    public Task ConfigTypePlan_DifferentInclude()
    {
        var ct1 = new ConfigTypePlan("A", true, null, new List<ConfigMemberPlan>());
        var ct2 = new ConfigTypePlan("A", false, null, new List<ConfigMemberPlan>());

        return Given("ConfigTypePlans with different include", () => (ct1, ct2))
            .Then("not equal", pair => !pair.ct1.Equals(pair.ct2))
            .AssertPassed();
    }

    // ── Scenarios: ParseManifest with generic parameters ─────────────

    [Scenario("ParseManifest: generic parameter with empty name skipped")]
    [Fact]
    public Task ParseManifest_EmptyGenericParamNameSkipped()
        => Given("manifest with empty generic param name",
                () => WrapGodIncrementalGenerator.ParseManifest("""
                    {
                      "assembly": { "name": "Lib" },
                      "types": [
                        {
                          "fullName": "Lib.Box",
                          "name": "Box",
                          "namespace": "Lib",
                          "genericParameters": [
                            { "name": "", "constraints": [] },
                            { "name": "T", "constraints": ["class"] }
                          ],
                          "members": []
                        }
                      ]
                    }
                    """))
            .Then("type has only valid generic parameter", plan =>
                plan != null && plan.Types[0].GenericTypeParameters.Count == 1
                && plan.Types[0].GenericTypeParameters[0].Name == "T")
            .AssertPassed();

    // ── Scenarios: ParseConfig member with empty sourceMember skipped ─

    [Scenario("ParseConfig: member with empty sourceMember skipped")]
    [Fact]
    public Task ParseConfig_EmptySourceMemberSkipped()
        => Given("config with empty source member",
                () => WrapGodIncrementalGenerator.ParseConfig("""
                    {
                      "types": [{
                        "sourceType": "A",
                        "members": [
                          { "sourceMember": "", "include": true },
                          { "sourceMember": "Valid", "include": true }
                        ]
                      }]
                    }
                    """))
            .Then("only valid member included", c =>
                c != null && c.Types[0].Members.Count == 1
                && c.Types[0].Members[0].SourceMember == "Valid")
            .AssertPassed();

    // ── Scenarios: ParseConfig sourceType empty skipped ──────────────

    [Scenario("ParseConfig: type with empty sourceType skipped")]
    [Fact]
    public Task ParseConfig_EmptySourceTypeSkipped()
        => Given("config with empty source type",
                () => WrapGodIncrementalGenerator.ParseConfig("""
                    {
                      "types": [
                        { "sourceType": "" },
                        { "sourceType": "Valid" }
                      ]
                    }
                    """))
            .Then("only valid type included", c =>
                c != null && c.Types.Count == 1
                && c.Types[0].SourceType == "Valid")
            .AssertPassed();

    // ── Scenarios: GenerationPlan self reference equality ─────────────

    [Scenario("GenerationPlan ReferenceEquals returns true")]
    [Fact]
    public Task GenerationPlan_SelfReference()
    {
        var plan = new GenerationPlan("Lib", new List<TypePlan>());

        return Given("same GenerationPlan reference", () => plan)
            .Then("ReferenceEquals returns true for Equals", p => p.Equals(p))
            .AssertPassed();
    }

    [Scenario("TypePlan ReferenceEquals returns true")]
    [Fact]
    public Task TypePlan_SelfReference()
    {
        var tp = new TypePlan("A", "A", "N", new List<MemberPlan>());

        return Given("same TypePlan reference", () => tp)
            .Then("ReferenceEquals returns true", p => p.Equals(p))
            .AssertPassed();
    }

    [Scenario("GenerationPlan: different type counts")]
    [Fact]
    public Task GenerationPlan_DifferentTypeCounts()
    {
        var p1 = new GenerationPlan("Lib", new List<TypePlan>());
        var p2 = new GenerationPlan("Lib", new List<TypePlan>
        {
            new("A", "A", "N", new List<MemberPlan>()),
        });

        return Given("plans with different type counts", () => (p1, p2))
            .Then("not equal", pair => !pair.p1.Equals(pair.p2))
            .AssertPassed();
    }

    // ── Scenarios: Combined manifest + config pipeline ───────────────

    [Scenario("Manifest + config combined: type excluded and member renamed")]
    [Fact]
    public Task ManifestPlusConfig_CombinedPipeline()
    {
        var manifest = """
            {
              "assembly": { "name": "Lib" },
              "types": [
                {
                  "fullName": "Lib.Alpha",
                  "name": "Alpha",
                  "namespace": "Lib",
                  "members": [
                    { "name": "Run", "kind": "method", "returnType": "void", "parameters": [], "genericParameters": [], "isStatic": false }
                  ]
                },
                {
                  "fullName": "Lib.Beta",
                  "name": "Beta",
                  "namespace": "Lib",
                  "members": [
                    { "name": "Go", "kind": "method", "returnType": "int", "parameters": [], "genericParameters": [], "isStatic": false }
                  ]
                }
              ]
            }
            """;
        var config = """
            {
              "types": [
                { "sourceType": "Lib.Alpha", "include": false },
                {
                  "sourceType": "Lib.Beta",
                  "include": true,
                  "targetName": "MyBeta",
                  "members": [
                    { "sourceMember": "Go", "include": true, "targetName": "DoIt" }
                  ]
                }
              ]
            }
            """;

        var plan = WrapGodIncrementalGenerator.ParseManifest(manifest)!;
        var cfg = WrapGodIncrementalGenerator.ParseConfig(config)!;
        var result = WrapGodIncrementalGenerator.ApplyConfig(
            ImmutableArray.Create(plan),
            ImmutableArray.Create(cfg));

        return Given("manifest + config applied", () => result)
            .Then("Alpha is excluded", r =>
                !r[0].Types.Any(t => t.Name == "Alpha"))
            .And("Beta is renamed to MyBeta", r =>
                r[0].Types[0].EffectiveName == "MyBeta")
            .And("Go is renamed to DoIt", r =>
                r[0].Types[0].Members[0].EffectiveName == "DoIt")
            .AssertPassed();
    }
}
