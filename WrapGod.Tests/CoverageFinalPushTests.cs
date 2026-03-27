using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Analyzers;
using WrapGod.Abstractions.Diagnostics;
using WrapGod.Generator;
using Xunit.Abstractions;
using GenerationPlan = WrapGod.Generator.GenerationPlan;

namespace WrapGod.Tests;

[Feature("Coverage final push: Generator ConfigMemberPlan, ConfigTypePlan, TypePlan, Analyzer paths")]
public sealed class CoverageFinalPushTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ═══════════════════════════════════════════════════════════════════
    //  ConfigMemberPlan: GetHashCode, Equals(object)
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("ConfigMemberPlan GetHashCode covers targetName path")]
    [Fact]
    public Task ConfigMemberPlan_HashCode_WithTargetName()
    {
        var cm1 = new ConfigMemberPlan("Foo", true, "Bar");
        var cm2 = new ConfigMemberPlan("Foo", true, "Bar");
        var cm3 = new ConfigMemberPlan("Foo", true, "Baz");

        return Given("ConfigMemberPlans with targetName", () => (cm1, cm2, cm3))
            .Then("same values have same hash", t => t.cm1.GetHashCode() == t.cm2.GetHashCode())
            .And("different targetName has different hash", t => t.cm1.GetHashCode() != t.cm3.GetHashCode())
            .And("Equals object works", t => t.cm1.Equals((object)t.cm2))
            .And("Equals different object fails", t => !t.cm1.Equals((object)t.cm3))
            .And("Equals wrong type fails", t => !t.cm1.Equals("not a config"))
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ConfigTypePlan: GetHashCode with targetName
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("ConfigTypePlan GetHashCode with targetName")]
    [Fact]
    public Task ConfigTypePlan_HashCode_WithTargetName()
    {
        var ct1 = new ConfigTypePlan("A", true, "Renamed",
            new List<ConfigMemberPlan> { new("M", true, null) });
        var ct2 = new ConfigTypePlan("A", true, "Renamed",
            new List<ConfigMemberPlan> { new("M", true, null) });
        var ct3 = new ConfigTypePlan("A", true, "Other",
            new List<ConfigMemberPlan> { new("M", true, null) });

        return Given("ConfigTypePlans with targetName", () => (ct1, ct2, ct3))
            .Then("same values same hash", t => t.ct1.GetHashCode() == t.ct2.GetHashCode())
            .And("different targetName different hash", t => t.ct1.GetHashCode() != t.ct3.GetHashCode())
            .And("Equals object works", t => t.ct1.Equals((object)t.ct2))
            .And("Equals wrong type fails", t => !t.ct1.Equals("not a config"))
            .AssertPassed();
    }

    [Scenario("ConfigTypePlan different source type")]
    [Fact]
    public Task ConfigTypePlan_DifferentSourceType()
    {
        var ct1 = new ConfigTypePlan("A", true, null, new List<ConfigMemberPlan>());
        var ct2 = new ConfigTypePlan("B", true, null, new List<ConfigMemberPlan>());

        return Given("different source types", () => (ct1, ct2))
            .Then("not equal", pair => !pair.ct1.Equals(pair.ct2))
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  TypePlan: GetHashCode with version info
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("TypePlan GetHashCode with version metadata")]
    [Fact]
    public Task TypePlan_HashCode_WithVersions()
    {
        var t1 = new TypePlan("A", "A", "N", new List<MemberPlan>(),
            targetName: "X", introducedIn: "1.0", removedIn: "2.0");
        var t2 = new TypePlan("A", "A", "N", new List<MemberPlan>(),
            targetName: "X", introducedIn: "1.0", removedIn: "2.0");

        return Given("TypePlans with version metadata", () => (t1, t2))
            .Then("same values same hash", t => t.t1.GetHashCode() == t.t2.GetHashCode())
            .And("Equals object works", t => t.t1.Equals((object)t.t2))
            .And("Equals wrong type fails", t => !t.t1.Equals("not a type"))
            .AssertPassed();
    }

    [Scenario("TypePlan with different fullName")]
    [Fact]
    public Task TypePlan_DifferentFullName()
    {
        var t1 = new TypePlan("A.X", "X", "A", new List<MemberPlan>());
        var t2 = new TypePlan("B.X", "X", "A", new List<MemberPlan>());

        return Given("TypePlans with different full name", () => (t1, t2))
            .Then("not equal", pair => !pair.t1.Equals(pair.t2))
            .AssertPassed();
    }

    [Scenario("TypePlan with different namespace")]
    [Fact]
    public Task TypePlan_DifferentNamespace()
    {
        var t1 = new TypePlan("A.X", "X", "A", new List<MemberPlan>());
        var t2 = new TypePlan("A.X", "X", "B", new List<MemberPlan>());

        return Given("TypePlans with different namespace", () => (t1, t2))
            .Then("not equal", pair => !pair.t1.Equals(pair.t2))
            .AssertPassed();
    }

    [Scenario("TypePlan with different targetName")]
    [Fact]
    public Task TypePlan_DifferentTargetName()
    {
        var t1 = new TypePlan("A", "A", "N", new List<MemberPlan>(), targetName: "X");
        var t2 = new TypePlan("A", "A", "N", new List<MemberPlan>(), targetName: "Y");

        return Given("TypePlans with different targetName", () => (t1, t2))
            .Then("not equal", pair => !pair.t1.Equals(pair.t2))
            .AssertPassed();
    }

    [Scenario("TypePlan with different generic param count")]
    [Fact]
    public Task TypePlan_DifferentGenericParamCount()
    {
        var t1 = new TypePlan("A", "A", "N", new List<MemberPlan>(),
            genericTypeParameters: new List<GenericTypeParameterPlan> { new("T") });
        var t2 = new TypePlan("A", "A", "N", new List<MemberPlan>());

        return Given("TypePlans with different generic param counts", () => (t1, t2))
            .Then("not equal", pair => !pair.t1.Equals(pair.t2))
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MemberPlan: GetHashCode with all version fields
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("MemberPlan GetHashCode with targetName and versions")]
    [Fact]
    public Task MemberPlan_HashCode_AllFields()
    {
        var m1 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false,
            targetName: "N", introducedIn: "1.0", removedIn: "2.0");
        var m2 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false,
            targetName: "N", introducedIn: "1.0", removedIn: "2.0");

        return Given("MemberPlans with all fields", () => (m1, m2))
            .Then("same hash", t => t.m1.GetHashCode() == t.m2.GetHashCode())
            .And("Equals object works", t => t.m1.Equals((object)t.m2))
            .And("Equals wrong type fails", t => !t.m1.Equals("not a member"))
            .AssertPassed();
    }

    [Scenario("MemberPlan with different targetName")]
    [Fact]
    public Task MemberPlan_DifferentTargetName()
    {
        var m1 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false,
            targetName: "X");
        var m2 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false,
            targetName: "Y");

        return Given("MemberPlans with different targetNames", () => (m1, m2))
            .Then("not equal", pair => !pair.m1.Equals(pair.m2))
            .AssertPassed();
    }

    [Scenario("MemberPlan with different param count")]
    [Fact]
    public Task MemberPlan_DifferentParamCount()
    {
        var m1 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false);
        var m2 = new MemberPlan("M", "method", "void",
            new List<ParameterPlan> { new("a", "int") }, false, false);

        return Given("MemberPlans with different param counts", () => (m1, m2))
            .Then("not equal", pair => !pair.m1.Equals(pair.m2))
            .AssertPassed();
    }

    [Scenario("MemberPlan with different generic param count")]
    [Fact]
    public Task MemberPlan_DifferentGenericParamCount()
    {
        var m1 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false);
        var m2 = new MemberPlan("M", "method", "void", new List<ParameterPlan>(), false, false,
            genericParameters: new List<string> { "T" });

        return Given("MemberPlans with different generic param counts", () => (m1, m2))
            .Then("not equal", pair => !pair.m1.Equals(pair.m2))
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  RoslynDiagnosticAdapter: severity mapping
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("RoslynDiagnosticAdapter maps all severity levels")]
    [Fact]
    public Task RoslynDiagnosticAdapter_AllSeverities()
    {
        // Create diagnostics with various severities
        var errorDesc = new DiagnosticDescriptor("ERR1", "Error", "Error {0}", "Test",
            DiagnosticSeverity.Error, true);
        var warnDesc = new DiagnosticDescriptor("WARN1", "Warning", "Warn {0}", "Test",
            DiagnosticSeverity.Warning, true);
        var infoDesc = new DiagnosticDescriptor("INFO1", "Info", "Info {0}", "Test",
            DiagnosticSeverity.Info, true);
        var hiddenDesc = new DiagnosticDescriptor("HIDE1", "Hidden", "Hidden {0}", "Test",
            DiagnosticSeverity.Hidden, true);

        var errorDiag = Diagnostic.Create(errorDesc, Location.None, "test");
        var warnDiag = Diagnostic.Create(warnDesc, Location.None, "test");
        var infoDiag = Diagnostic.Create(infoDesc, Location.None, "test");
        var hiddenDiag = Diagnostic.Create(hiddenDesc, Location.None, "test");

        var e = RoslynDiagnosticAdapter.ToWgDiagnosticV1(errorDiag);
        var w = RoslynDiagnosticAdapter.ToWgDiagnosticV1(warnDiag);
        var i = RoslynDiagnosticAdapter.ToWgDiagnosticV1(infoDiag);
        var h = RoslynDiagnosticAdapter.ToWgDiagnosticV1(hiddenDiag);

        return Given("diagnostics of all severity levels", () => (e, w, i, h))
            .Then("error maps to error", t => t.e.Severity == WgDiagnosticSeverity.Error)
            .And("warning maps to warning", t => t.w.Severity == WgDiagnosticSeverity.Warning)
            .And("info maps to note", t => t.i.Severity == WgDiagnosticSeverity.Note)
            .And("hidden maps to none", t => t.h.Severity == WgDiagnosticSeverity.None)
            .And("all have correct code", t =>
                t.e.Code == "ERR1" && t.w.Code == "WARN1" && t.i.Code == "INFO1" && t.h.Code == "HIDE1")
            .AssertPassed();
    }

    [Scenario("RoslynDiagnosticAdapter: diagnostic without file location")]
    [Fact]
    public Task RoslynDiagnosticAdapter_NoLocation()
    {
        var desc = new DiagnosticDescriptor("WG001", "Test", "Msg", "Test",
            DiagnosticSeverity.Warning, true);
        var diag = Diagnostic.Create(desc, Location.None);
        var wg = RoslynDiagnosticAdapter.ToWgDiagnosticV1(diag);

        return Given("diagnostic with no file location", () => wg)
            .Then("Location is null", d => d.Location is null)
            .And("HelpUri is null or empty", d => string.IsNullOrEmpty(d.HelpUri))
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DirectUsageAnalyzer: more node analysis paths
    // ═══════════════════════════════════════════════════════════════════

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source, params AdditionalText[] additionalTexts)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(
                typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location),
        };
        foreach (var dll in new[] { "netstandard.dll", "System.Collections.dll", "System.Linq.dll" })
        {
            var path = Path.Combine(runtimeDir, dll);
            if (File.Exists(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { syntaxTree }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var analyzer = new DirectUsageAnalyzer();
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer),
            new AnalyzerOptions(additionalTexts.ToImmutableArray()));
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private const string AcmeLibDef = """
        namespace Acme.Lib
        {
            public class FooService
            {
                public string DoWork(string input) => input;
                public int Count { get; set; }
            }
        }
        """;

    private static readonly string FooManifest = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib" },
          "types": [
            {
              "fullName": "Acme.Lib.FooService",
              "name": "FooService",
              "namespace": "Acme.Lib",
              "members": [
                { "name": "DoWork", "kind": "method", "returnType": "string" },
                { "name": "Count", "kind": "property", "returnType": "int" }
              ]
            }
          ]
        }
        """;

    [Scenario("Analyzer: local variable typed as wrapped type triggers WG2001")]
    [Fact]
    public Task Analyzer_LocalVariable()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    public void Test()
                    {
                        Acme.Lib.FooService svc = new Acme.Lib.FooService();
                    }
                }
            }
            """;

        return Given("local variable typed as wrapped type",
                () => RunAnalyzerAsync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest))
                    .GetAwaiter().GetResult())
            .Then("WG2001 is reported", diags => diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    [Scenario("Analyzer: property typed as wrapped type triggers WG2001")]
    [Fact]
    public Task Analyzer_PropertyType()
    {
        var source = AcmeLibDef + """
            namespace MyApp
            {
                public class Consumer
                {
                    public Acme.Lib.FooService Svc { get; set; }
                }
            }
            """;

        return Given("property typed as wrapped type",
                () => RunAnalyzerAsync(source,
                    new InMemoryAdditionalText("a.wrapgod.json", FooManifest))
                    .GetAwaiter().GetResult())
            .Then("WG2001 is reported", diags => diags.Any(d => d.Id == "WG2001"))
            .AssertPassed();
    }

    // ── Scenarios: WrapGodIncrementalGenerator additional paths ───────

    [Scenario("ParseManifest: manifest with no assembly property")]
    [Fact]
    public Task ParseManifest_NoAssembly()
        => Given("manifest without assembly property",
                () => WrapGodIncrementalGenerator.ParseManifest("""
                    {
                      "types": [
                        { "fullName": "A", "name": "A", "namespace": "", "members": [] }
                      ]
                    }
                    """))
            .Then("assembly name defaults to Unknown", plan =>
                plan != null && plan.AssemblyName == "Unknown")
            .AssertPassed();

    [Scenario("ParseManifest: manifest with no types property")]
    [Fact]
    public Task ParseManifest_NoTypes()
        => Given("manifest without types property",
                () => WrapGodIncrementalGenerator.ParseManifest("""
                    { "assembly": { "name": "Lib" } }
                    """))
            .Then("plan has zero types", plan =>
                plan != null && plan.Types.Count == 0)
            .AssertPassed();

    [Scenario("ParseConfig: config with no types property")]
    [Fact]
    public Task ParseConfig_NoTypes()
        => Given("config without types property",
                () => WrapGodIncrementalGenerator.ParseConfig("{}"))
            .Then("config has zero types", c => c != null && c.Types.Count == 0)
            .AssertPassed();

    [Scenario("ParseConfig: type with include as non-bool")]
    [Fact]
    public Task ParseConfig_NonBoolInclude()
        => Given("config type with non-bool include (defaults to true)",
                () => WrapGodIncrementalGenerator.ParseConfig("""
                    {
                      "types": [{
                        "sourceType": "A",
                        "include": "yes"
                      }]
                    }
                    """))
            .Then("type include defaults to true (non-bool ignored)", c =>
                c != null && c.Types[0].Include)
            .AssertPassed();

    [Scenario("ParseManifest: member with isStatic true")]
    [Fact]
    public Task ParseManifest_StaticMember()
        => Given("manifest with static member",
                () => WrapGodIncrementalGenerator.ParseManifest("""
                    {
                      "assembly": { "name": "Lib" },
                      "types": [{
                        "fullName": "Lib.A",
                        "name": "A",
                        "namespace": "Lib",
                        "members": [{
                          "name": "M",
                          "kind": "method",
                          "returnType": "void",
                          "parameters": [],
                          "genericParameters": [],
                          "isStatic": true
                        }]
                      }]
                    }
                    """))
            .Then("member isStatic is true", plan =>
                plan != null && plan.Types[0].Members[0].IsStatic)
            .AssertPassed();

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
