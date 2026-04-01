using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Generator;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Generator coverage: untested paths")]
public sealed class GeneratorCoverageTests(ITestOutputHelper output) : TinyBddXunitBase(output)
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

    private static readonly string ManifestNoTypes = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Empty.Lib", "version": "1.0.0" },
          "types": []
        }
        """;

    private static readonly string ManifestTypeNoMembers = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Marker",
              "name": "Marker",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": []
            }
          ]
        }
        """;

    private static readonly string ManifestPropertyOnly = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Settings",
              "name": "Settings",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "Host",
                  "kind": "property",
                  "returnType": "string",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false,
                  "hasGetter": true,
                  "hasSetter": true
                },
                {
                  "name": "Port",
                  "kind": "property",
                  "returnType": "int",
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

    private static readonly string ManifestStaticOnly = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Utils",
              "name": "Utils",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "Format",
                  "kind": "method",
                  "returnType": "string",
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

    private static readonly string ManifestGenericMultiConstraint = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Store",
              "name": "Store",
              "namespace": "Acme.Lib",
              "kind": "class",
              "genericParameters": [
                { "name": "TKey", "constraints": ["struct", "System.IComparable<TKey>"] },
                { "name": "TValue", "constraints": ["class", "new()"] }
              ],
              "members": [
                {
                  "name": "Get",
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

    private static readonly string ManifestTwoTypes = """
        {
          "schemaVersion": "1.0",
          "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
          "types": [
            {
              "fullName": "Acme.Lib.Alpha",
              "name": "Alpha",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "Run",
                  "kind": "method",
                  "returnType": "void",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false,
                  "hasGetter": false,
                  "hasSetter": false
                }
              ]
            },
            {
              "fullName": "Acme.Lib.Beta",
              "name": "Beta",
              "namespace": "Acme.Lib",
              "kind": "class",
              "members": [
                {
                  "name": "Execute",
                  "kind": "method",
                  "returnType": "int",
                  "parameters": [],
                  "genericParameters": [],
                  "isStatic": false,
                  "hasGetter": false,
                  "hasSetter": false
                },
                {
                  "name": "Name",
                  "kind": "property",
                  "returnType": "string",
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

    private static readonly string ConfigExcludeAll = """
        {
          "types": [
            { "sourceType": "Acme.Lib.Alpha", "include": false },
            { "sourceType": "Acme.Lib.Beta", "include": false }
          ]
        }
        """;

    private static readonly string ConfigRenameAll = """
        {
          "types": [
            {
              "sourceType": "Acme.Lib.Alpha",
              "include": true,
              "targetName": "RenamedAlpha",
              "members": [
                { "sourceMember": "Run", "include": true, "targetName": "Go" }
              ]
            },
            {
              "sourceType": "Acme.Lib.Beta",
              "include": true,
              "targetName": "RenamedBeta",
              "members": [
                { "sourceMember": "Execute", "include": true, "targetName": "DoIt" },
                { "sourceMember": "Name", "include": true, "targetName": "Label" }
              ]
            }
          ]
        }
        """;

    // ── Scenarios: Empty and minimal manifests ───────────────────────

    [Scenario("Empty manifest (no types) produces no generated output")]
    [Fact]
    public Task EmptyManifest_NoOutput()
        => Given("a generator run with a manifest containing zero types",
                () => RunGenerator(ManifestNoTypes))
            .Then("no source files are emitted", result =>
                AllGeneratedHintNames(result).Count == 0)
            .AssertPassed();

    [Scenario("Type with no members produces interface and facade with no body members")]
    [Fact]
    public Task TypeWithNoMembers_EmptyInterfaceAndFacade()
        => Given("a manifest with a type that has zero members",
                () => RunGenerator(ManifestTypeNoMembers))
            .Then("an interface is emitted", result =>
                AllGeneratedHintNames(result).Contains("IWrappedMarker.g.cs"))
            .And("a facade is emitted", result =>
                AllGeneratedHintNames(result).Contains("MarkerFacade.g.cs"))
            .And("the interface body has no method signatures", result =>
                !GetSource(result, "IWrappedMarker.g.cs").Contains('(')
                || !GetSource(result, "IWrappedMarker.g.cs").Split('\n')
                    .Any(l => l.TrimStart().StartsWith("    ", StringComparison.Ordinal) && l.Contains('(')))
            .AssertPassed();

    // ── Scenario: Property-only type ─────────────────────────────────

    [Scenario("Property-only type emits get/set in interface and facade")]
    [Fact]
    public Task PropertyOnlyType()
        => Given("a manifest with only properties (no methods)",
                () => RunGenerator(ManifestPropertyOnly))
            .Then("the interface contains Host property with get/set", result =>
                GetSource(result, "IWrappedSettings.g.cs")
                    .Contains("string Host { get; set; }", StringComparison.Ordinal))
            .And("the interface contains Port as read-only", result =>
                GetSource(result, "IWrappedSettings.g.cs")
                    .Contains("int Port { get; }", StringComparison.Ordinal))
            .And("the facade delegates Host getter", result =>
                GetSource(result, "SettingsFacade.g.cs")
                    .Contains("get => _inner.Host;", StringComparison.Ordinal))
            .And("the facade delegates Host setter", result =>
                GetSource(result, "SettingsFacade.g.cs")
                    .Contains("set => _inner.Host = value;", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenario: Static members skipped completely ──────────────────

    [Scenario("All-static type produces interface and facade with no members")]
    [Fact]
    public Task StaticOnlyType_NoMembersEmitted()
        => Given("a manifest where all members are static",
                () => RunGenerator(ManifestStaticOnly))
            .Then("an interface is emitted", result =>
                AllGeneratedHintNames(result).Contains("IWrappedUtils.g.cs"))
            .And("the interface does not contain the static method", result =>
                !GetSource(result, "IWrappedUtils.g.cs")
                    .Contains("Format", StringComparison.Ordinal))
            .And("the facade does not contain the static method", result =>
                !GetSource(result, "UtilsFacade.g.cs")
                    .Contains("Format", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenario: Adaptive mode emission with version guards ─────────

    [Scenario("Adaptive mode emits version-guarded facade members")]
    [Fact]
    public Task AdaptiveMode_VersionGuards()
    {
        // Build the type plan directly with version metadata on members,
        // since ParseManifest does not extract introducedIn/removedIn.
        var members = new List<MemberPlan>
        {
            new("DoWork", "method", "string",
                new List<ParameterPlan> { new("input", "string") },
                hasGetter: false, hasSetter: false, isStatic: false,
                genericParameters: new List<GenericTypeParameterPlan>(),
                introducedIn: "2.0"),
            new("DoVoidWork", "method", "void",
                new List<ParameterPlan>(),
                hasGetter: false, hasSetter: false, isStatic: false,
                genericParameters: new List<GenericTypeParameterPlan>(),
                introducedIn: "2.0", removedIn: "4.0"),
            new("Enabled", "property", "bool",
                new List<ParameterPlan>(),
                hasGetter: true, hasSetter: true, isStatic: false,
                genericParameters: new List<GenericTypeParameterPlan>(),
                introducedIn: "3.0"),
            new("ReadOnlyFlag", "property", "bool",
                new List<ParameterPlan>(),
                hasGetter: true, hasSetter: false, isStatic: false,
                genericParameters: new List<GenericTypeParameterPlan>(),
                introducedIn: "3.0", removedIn: "5.0"),
        };

        var type = new TypePlan("Acme.Lib.Service", "Service", "Acme.Lib", members);

        bool previousMode = SourceEmitter.AdaptiveMode;
        SourceEmitter.AdaptiveMode = true;
        try
        {
            var facadeSource = SourceEmitter.EmitFacade(type);

            return Given("adaptive facade source", () => facadeSource)
                .Then("the facade contains a version guard for DoWork", source =>
                    source.Contains("WrapGodVersionHelper.IsMemberAvailable", StringComparison.Ordinal))
                .And("the guard references introduced version 2.0", source =>
                    source.Contains("\"2.0\"", StringComparison.Ordinal))
                .And("the guard for DoVoidWork references removed version 4.0", source =>
                    source.Contains("\"4.0\"", StringComparison.Ordinal))
                .And("the property getter has a version guard", source =>
                    source.Contains("get") && source.Contains("IsMemberAvailable"))
                .And("the property setter has a version guard", source =>
                    source.Contains("set") && source.Contains("IsMemberAvailable"))
                .And("the void method does not have a return keyword before _inner call", source =>
                    source.Contains("_inner.DoVoidWork()") && !source.Contains("return _inner.DoVoidWork"))
                .And("the non-void method has a return keyword before _inner call", source =>
                    source.Contains("return _inner.DoWork"))
                .And("the availability comment includes removed version", source =>
                    source.Contains("removed in 4.0", StringComparison.Ordinal))
                .AssertPassed();
        }
        finally
        {
            SourceEmitter.AdaptiveMode = previousMode;
        }
    }

    // ── Scenario: Config excludes ALL types ──────────────────────────

    [Scenario("Config that excludes all types produces no output")]
    [Fact]
    public Task ConfigExcludesAllTypes_NoOutput()
        => Given("a manifest with two types and a config excluding both",
                () => RunGenerator(ManifestTwoTypes, ConfigExcludeAll))
            .Then("no source files are emitted", result =>
                AllGeneratedHintNames(result).Count == 0)
            .AssertPassed();

    // ── Scenario: Config renames ALL types and members ───────────────

    [Scenario("Config renames all types and members")]
    [Fact]
    public Task ConfigRenamesAllTypesAndMembers()
        => Given("a manifest with two types and a config renaming everything",
                () => RunGenerator(ManifestTwoTypes, ConfigRenameAll))
            .Then("the interface uses renamed type: IWrappedRenamedAlpha", result =>
                AllGeneratedHintNames(result).Contains("IWrappedRenamedAlpha.g.cs"))
            .And("the facade uses renamed type: RenamedAlphaFacade", result =>
                AllGeneratedHintNames(result).Contains("RenamedAlphaFacade.g.cs"))
            .And("the renamed interface for Beta exists", result =>
                AllGeneratedHintNames(result).Contains("IWrappedRenamedBeta.g.cs"))
            .And("the renamed Alpha interface contains renamed member Go", result =>
                GetSource(result, "IWrappedRenamedAlpha.g.cs")
                    .Contains("Go(", StringComparison.Ordinal))
            .And("the renamed Beta interface contains renamed member DoIt", result =>
                GetSource(result, "IWrappedRenamedBeta.g.cs")
                    .Contains("DoIt(", StringComparison.Ordinal))
            .And("the renamed Beta interface contains renamed property Label", result =>
                GetSource(result, "IWrappedRenamedBeta.g.cs")
                    .Contains("Label", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenario: Generic type with multiple constraints ─────────────

    [Scenario("Generic type with multiple constraints emits where clauses")]
    [Fact]
    public Task GenericTypeWithMultipleConstraints()
        => Given("a manifest with a generic type having two constrained type parameters",
                () => RunGenerator(ManifestGenericMultiConstraint))
            .Then("the interface emits generic parameters", result =>
                GetSource(result, "IWrappedStore.g.cs")
                    .Contains("<TKey, TValue>", StringComparison.Ordinal))
            .And("the interface emits where clause for TKey", result =>
                GetSource(result, "IWrappedStore.g.cs")
                    .Contains("where TKey : struct, System.IComparable<TKey>", StringComparison.Ordinal))
            .And("the interface emits where clause for TValue", result =>
                GetSource(result, "IWrappedStore.g.cs")
                    .Contains("where TValue : class, new()", StringComparison.Ordinal))
            .And("the facade emits generic suffix on class declaration", result =>
                GetSource(result, "StoreFacade.g.cs")
                    .Contains("StoreFacade<TKey, TValue>", StringComparison.Ordinal))
            .And("the facade emits constraint clauses", result =>
                GetSource(result, "StoreFacade.g.cs")
                    .Contains("where TKey : struct", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenario: Constructor emission ───────────────────────────────

    [Scenario("Facade constructor guards against null")]
    [Fact]
    public Task ConstructorEmission_NullGuard()
        => Given("a manifest with a simple type",
                () => RunGenerator(ManifestTypeNoMembers))
            .Then("the facade has a constructor with null guard", result =>
                GetSource(result, "MarkerFacade.g.cs")
                    .Contains("throw new System.ArgumentNullException(nameof(inner))",
                        StringComparison.Ordinal))
            .And("the constructor parameter is the original type", result =>
                GetSource(result, "MarkerFacade.g.cs")
                    .Contains("Acme.Lib.Marker inner", StringComparison.Ordinal))
            .AssertPassed();

    // ── Scenario: ParseManifest with invalid JSON returns null ───────

    [Scenario("ParseManifest with garbage JSON returns null")]
    [Fact]
    public Task ParseManifest_InvalidJson_ReturnsNull()
        => Given("garbage JSON input",
                () => WrapGodIncrementalGenerator.ParseManifest("not json at all"))
            .Then("the result is null", plan => plan == null)
            .AssertPassed();

    // ── Scenario: ParseConfig with invalid JSON returns null ─────────

    [Scenario("ParseConfig with garbage JSON returns null")]
    [Fact]
    public Task ParseConfig_InvalidJson_ReturnsNull()
        => Given("garbage JSON config input",
                () => WrapGodIncrementalGenerator.ParseConfig("{{{broken"))
            .Then("the result is null", config => config == null)
            .AssertPassed();

    // ── Scenario: ParseManifest with missing type name skips type ────

    [Scenario("ParseManifest skips types with missing name")]
    [Fact]
    public Task ParseManifest_MissingName_SkipsType()
        => Given("a manifest where a type has no name field",
                () => WrapGodIncrementalGenerator.ParseManifest("""
                    {
                      "assembly": { "name": "Lib" },
                      "types": [
                        { "fullName": "Lib.Foo", "namespace": "Lib", "members": [] }
                      ]
                    }
                    """))
            .Then("the plan has zero types (unnamed type skipped)", plan =>
                plan != null && plan.Types.Count == 0)
            .AssertPassed();

    // ── Scenario: ApplyConfig with no configs is identity ────────────

    [Scenario("ApplyConfig with empty configs returns plans unchanged")]
    [Fact]
    public Task ApplyConfig_NoConfigs_Identity()
    {
        var plan = WrapGodIncrementalGenerator.ParseManifest(ManifestTwoTypes)!;
        var plans = ImmutableArray.Create(plan);
        var noConfigs = ImmutableArray<ConfigPlan>.Empty;

        return Given("plans with no configs",
                () => WrapGodIncrementalGenerator.ApplyConfig(plans, noConfigs))
            .Then("the result equals the input", result =>
                result.Length == 1 && result[0].Types.Count == 2)
            .AssertPassed();
    }

    // ── Scenario: ParseManifest with member missing name skips member ─

    [Scenario("ParseManifest skips members with missing name")]
    [Fact]
    public Task ParseManifest_MissingMemberName_SkipsMember()
        => Given("a manifest with a member that has no name",
                () => WrapGodIncrementalGenerator.ParseManifest("""
                    {
                      "assembly": { "name": "Lib" },
                      "types": [
                        {
                          "fullName": "Lib.Foo",
                          "name": "Foo",
                          "namespace": "Lib",
                          "members": [
                            { "kind": "method", "returnType": "void", "parameters": [] },
                            { "name": "Valid", "kind": "method", "returnType": "void", "parameters": [], "genericParameters": [] }
                          ]
                        }
                      ]
                    }
                    """))
            .Then("the type has only the valid member", plan =>
                plan != null && plan.Types[0].Members.Count == 1
                && plan.Types[0].Members[0].Name == "Valid")
            .AssertPassed();

    // ── Scenario: Generics empty constraints list ────────────────────

    [Scenario("Generic type parameter with empty constraints emits no where clause")]
    [Fact]
    public Task GenericTypeParam_NoConstraints_NoWhereClause()
    {
        var manifest = """
            {
              "assembly": { "name": "Lib" },
              "types": [
                {
                  "fullName": "Lib.Box",
                  "name": "Box",
                  "namespace": "Lib",
                  "genericParameters": [
                    { "name": "T", "constraints": [] }
                  ],
                  "members": []
                }
              ]
            }
            """;
        return Given("a manifest with unconstrained generic parameter",
                () => RunGenerator(manifest))
            .Then("the interface does not contain a where clause", result =>
                !GetSource(result, "IWrappedBox.g.cs")
                    .Contains("where", StringComparison.Ordinal))
            .And("the interface contains the generic parameter", result =>
                GetSource(result, "IWrappedBox.g.cs")
                    .Contains("<T>", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Config member exclusion ────────────────────────────

    [Scenario("Config excludes specific members while keeping the type")]
    [Fact]
    public Task ConfigExcludesMember()
    {
        var config = """
            {
              "types": [
                {
                  "sourceType": "Acme.Lib.Beta",
                  "include": true,
                  "members": [
                    { "sourceMember": "Execute", "include": false }
                  ]
                }
              ]
            }
            """;
        return Given("a manifest with two members and a config excluding one",
                () => RunGenerator(ManifestTwoTypes, config))
            .Then("the Beta interface does not contain excluded method Execute", result =>
                !GetSource(result, "IWrappedBeta.g.cs")
                    .Contains("Execute", StringComparison.Ordinal))
            .And("the Beta interface still contains the property Name", result =>
                GetSource(result, "IWrappedBeta.g.cs")
                    .Contains("Name", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Params modifier ────────────────────────────────────

    [Scenario("Params parameter modifier is emitted in interface")]
    [Fact]
    public Task ParamsModifierEmitted()
    {
        var manifest = """
            {
              "assembly": { "name": "Lib" },
              "types": [
                {
                  "fullName": "Lib.Logger",
                  "name": "Logger",
                  "namespace": "Lib",
                  "members": [
                    {
                      "name": "Log",
                      "kind": "method",
                      "returnType": "void",
                      "parameters": [
                        { "name": "args", "type": "object[]", "isParams": true }
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
        return Given("a manifest with a params parameter",
                () => RunGenerator(manifest))
            .Then("the interface contains params modifier", result =>
                GetSource(result, "IWrappedLogger.g.cs")
                    .Contains("params object[] args", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: Ref parameter forwarded ────────────────────────────

    [Scenario("Ref parameter forwarded in facade")]
    [Fact]
    public Task RefParameterForwardedInFacade()
    {
        var manifest = """
            {
              "assembly": { "name": "Lib" },
              "types": [
                {
                  "fullName": "Lib.Swapper",
                  "name": "Swapper",
                  "namespace": "Lib",
                  "members": [
                    {
                      "name": "Swap",
                      "kind": "method",
                      "returnType": "void",
                      "parameters": [
                        { "name": "a", "type": "int", "isRef": true },
                        { "name": "b", "type": "int", "isRef": true }
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
        return Given("a manifest with ref parameters",
                () => RunGenerator(manifest))
            .Then("the facade forwards with ref keyword", result =>
                GetSource(result, "SwapperFacade.g.cs")
                    .Contains("_inner.Swap(ref a, ref b)", StringComparison.Ordinal))
            .AssertPassed();
    }

    // ── Scenario: GenerationPlan equality ────────────────────────────

    [Scenario("GenerationPlan equality and hash code")]
    [Fact]
    public Task GenerationPlanEquality()
    {
        var plan1 = WrapGodIncrementalGenerator.ParseManifest(ManifestTwoTypes)!;
        var plan2 = WrapGodIncrementalGenerator.ParseManifest(ManifestTwoTypes)!;
        var plan3 = WrapGodIncrementalGenerator.ParseManifest(ManifestTypeNoMembers)!;

        return Given("two identical plans and one different",
                () => (plan1, plan2, plan3))
            .Then("identical plans are equal", tuple => tuple.plan1.Equals(tuple.plan2))
            .And("different plans are not equal", tuple => !tuple.plan1.Equals(tuple.plan3))
            .And("identical plans have same hash code", tuple =>
                tuple.plan1.GetHashCode() == tuple.plan2.GetHashCode())
            .And("Equals(null) returns false", tuple => !tuple.plan1.Equals((WrapGod.Generator.GenerationPlan?)null))
            .And("Equals(object) works", tuple => tuple.plan1.Equals((object)tuple.plan2))
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
