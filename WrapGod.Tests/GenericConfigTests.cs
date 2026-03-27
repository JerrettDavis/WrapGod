using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Abstractions.Config;
using WrapGod.Fluent;
using WrapGod.Manifest.Config;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Generic configuration support")]
public sealed class GenericConfigTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // -- JSON config helpers ------------------------------------------------

    private static readonly string GenericJsonConfig = """
        {
          "types": [
            {
              "sourceType": "IRepository<>",
              "include": true,
              "targetName": "IWrappedRepository"
            },
            {
              "sourceType": "Dictionary<,>",
              "include": true,
              "targetName": "IWrappedDictionary"
            },
            {
              "sourceType": "Vendor.ConcreteService",
              "include": true,
              "targetName": "IConcreteService"
            }
          ]
        }
        """;

    private static WrapGodConfig LoadGenericJsonConfig() =>
        JsonConfigLoader.LoadFromJson(GenericJsonConfig);

    // -- Fluent DSL helpers -------------------------------------------------

    private static GenerationPlan BuildGenericFluentPlan() =>
        WrapGodConfiguration.Create()
            .ForAssembly("Vendor.Lib")
            .WrapType("IRepository<>")
                .As("IWrappedRepository")
            .WrapType("Dictionary<,>")
                .As("IWrappedDictionary")
            .WrapType("Vendor.ConcreteService")
                .As("IConcreteService")
            .Build();

    // -- Merge engine helpers -----------------------------------------------

    private static ConfigMergeResult MergeGenericAndConcreteRules()
    {
        var jsonConfig = new WrapGodConfig();
        jsonConfig.Types.Add(new TypeConfig
        {
            SourceType = "IRepository<>",
            IsGenericPattern = true,
            GenericArity = 1,
            Include = true,
            TargetName = "JsonRepo",
        });
        jsonConfig.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.ConcreteService",
            Include = true,
            TargetName = "JsonConcrete",
        });

        var attrConfig = new WrapGodConfig();
        attrConfig.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.ConcreteService",
            Include = false,
            TargetName = "AttrConcrete",
        });

        return ConfigMergeEngine.Merge(jsonConfig, attrConfig, new ConfigMergeOptions
        {
            HigherPrecedence = ConfigSource.Attributes,
        });
    }

    private static ConfigMergeResult MergeGenericPatternConflict()
    {
        var jsonConfig = new WrapGodConfig();
        jsonConfig.Types.Add(new TypeConfig
        {
            SourceType = "IRepository<>",
            IsGenericPattern = true,
            GenericArity = 1,
            Include = true,
            TargetName = "JsonRepo",
        });

        var attrConfig = new WrapGodConfig();
        attrConfig.Types.Add(new TypeConfig
        {
            SourceType = "IRepository<>",
            IsGenericPattern = true,
            GenericArity = 1,
            Include = false,
            TargetName = "AttrRepo",
        });

        return ConfigMergeEngine.Merge(jsonConfig, attrConfig, new ConfigMergeOptions
        {
            HigherPrecedence = ConfigSource.Attributes,
        });
    }

    // -- Scenarios: JSON config with generic type rules ---------------------

    [Scenario("JSON config with open generic type rules loads correctly")]
    [Fact]
    public Task JsonConfig_GenericTypeRules_LoadCorrectly()
        => Given("a JSON config with generic type patterns", LoadGenericJsonConfig)
            .Then("three type rules are parsed", config => config.Types.Count == 3)
            .And("IRepository<> is marked as a generic pattern", config =>
                config.Types[0].IsGenericPattern)
            .And("IRepository<> has arity 1", config =>
                config.Types[0].GenericArity == 1)
            .And("Dictionary<,> is marked as a generic pattern", config =>
                config.Types[1].IsGenericPattern)
            .And("Dictionary<,> has arity 2", config =>
                config.Types[1].GenericArity == 2)
            .And("ConcreteService is NOT a generic pattern", config =>
                !config.Types[2].IsGenericPattern)
            .And("ConcreteService has arity 0", config =>
                config.Types[2].GenericArity == 0)
            .AssertPassed();

    // -- Scenarios: Fluent config with generic patterns ---------------------

    [Scenario("Fluent config with generic patterns builds correct plan")]
    [Fact]
    public Task FluentConfig_GenericPatterns_BuildCorrectPlan()
        => Given("a fluent config with generic type patterns", BuildGenericFluentPlan)
            .Then("three type directives are created", plan =>
                plan.TypeDirectives.Count == 3)
            .And("IRepository<> directive is a generic pattern", plan =>
                plan.TypeDirectives[0].IsGenericPattern)
            .And("IRepository<> directive has arity 1", plan =>
                plan.TypeDirectives[0].GenericArity == 1)
            .And("IRepository<> directive has correct target name", plan =>
                plan.TypeDirectives[0].TargetName == "IWrappedRepository")
            .And("Dictionary<,> directive is a generic pattern", plan =>
                plan.TypeDirectives[1].IsGenericPattern)
            .And("Dictionary<,> directive has arity 2", plan =>
                plan.TypeDirectives[1].GenericArity == 2)
            .And("ConcreteService directive is NOT a generic pattern", plan =>
                !plan.TypeDirectives[2].IsGenericPattern)
            .AssertPassed();

    // -- Scenarios: Merge engine with generic + concrete rules ---------------

    [Scenario("Merge engine handles generic and concrete rules together")]
    [Fact]
    public Task MergeEngine_GenericAndConcreteRules_HandledCorrectly()
        => Given("JSON config with generic + concrete and attribute config with concrete",
                MergeGenericAndConcreteRules)
            .Then("merged config has two type rules", result =>
                result.Config.Types.Count == 2)
            .And("the generic pattern rule is preserved", result =>
                result.Config.Types.Any(t => t.IsGenericPattern && t.SourceType == "IRepository<>"))
            .And("the concrete rule uses attribute precedence for target name", result =>
                result.Config.Types.First(t => t.SourceType == "Vendor.ConcreteService")
                    .TargetName == "AttrConcrete")
            .And("diagnostics are emitted for the concrete conflict", result =>
                result.Diagnostics.Count > 0)
            .AssertPassed();

    [Scenario("Merge engine resolves conflicting generic pattern rules by precedence")]
    [Fact]
    public Task MergeEngine_GenericPatternConflict_ResolvedByPrecedence()
        => Given("conflicting JSON and attribute configs for a generic pattern",
                MergeGenericPatternConflict)
            .Then("merged config has one type rule", result =>
                result.Config.Types.Count == 1)
            .And("attribute config wins for include", result =>
                result.Config.Types[0].Include == false)
            .And("attribute config wins for target name", result =>
                result.Config.Types[0].TargetName == "AttrRepo")
            .And("the generic pattern metadata is preserved", result =>
                result.Config.Types[0].IsGenericPattern)
            .And("diagnostics are emitted for the conflict", result =>
                result.Diagnostics.Count > 0)
            .AssertPassed();
}
