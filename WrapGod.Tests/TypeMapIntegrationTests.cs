using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Abstractions.Config;
using WrapGod.TypeMap;
using WrapGod.TypeMap.Generation;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("TypeMap to Generator integration")]
public sealed class TypeMapIntegrationTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static TypeMappingPlan BuildPlanFromConfig()
    {
        var config = new WrapGodConfig();
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Client",
            Include = true,
            TargetName = "IClient",
        });
        config.Types[0].Members.Add(
            new MemberConfig { SourceMember = "GetUser", Include = true, TargetName = "FetchUser" });
        config.Types[0].Members.Add(
            new MemberConfig { SourceMember = "Timeout", Include = true });

        return TypeMappingPlanner.BuildPlan(config);
    }

    private static string EmitMapperFromPlan()
    {
        var plan = BuildPlanFromConfig();
        return TypeMapperEmitter.Emit(plan);
    }

    private static string FullFlowWithObjectAndEnum()
    {
        var config = new WrapGodConfig();

        // Object type mapping
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.Client",
            Include = true,
            TargetName = "IClient",
        });
        config.Types[0].Members.Add(
            new MemberConfig { SourceMember = "GetUser", Include = true, TargetName = "FetchUser" });
        config.Types[0].Members.Add(
            new MemberConfig { SourceMember = "Timeout", Include = true });

        // Enum type mapping
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.StatusCode",
            Include = true,
            TargetName = "StatusCode",
        });

        var overrides = new List<TypeMappingOverride>
        {
            new() { SourceType = "Vendor.StatusCode", Kind = TypeMappingKind.Enum },
        };

        var plan = TypeMappingPlanner.BuildPlan(config, overrides);
        return TypeMapperEmitter.Emit(plan);
    }

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("TypeMappingPlanner builds plan from WrapGodConfig with type mappings")]
    [Fact]
    public Task PlannerBuildsPlanFromConfig()
        => Given("a WrapGodConfig with an object type and two members", BuildPlanFromConfig)
            .Then("there is exactly one mapping", plan => plan.Mappings.Count == 1)
            .And("the mapping kind is ObjectMapping", plan =>
                plan.Mappings[0].Kind == TypeMappingKind.ObjectMapping)
            .And("the source type is Vendor.Client", plan =>
                plan.Mappings[0].SourceType == "Vendor.Client")
            .And("the destination type is IClient", plan =>
                plan.Mappings[0].DestinationType == "IClient")
            .And("there are two member mappings", plan =>
                plan.Mappings[0].MemberMappings.Count == 2)
            .And("the renamed member maps GetUser to FetchUser", plan =>
                plan.Mappings[0].MemberMappings[0].SourceMember == "GetUser"
                && plan.Mappings[0].MemberMappings[0].DestinationMember == "FetchUser")
            .AssertPassed();

    [Scenario("TypeMapperEmitter generates mapper source from plan with Map methods")]
    [Fact]
    public Task EmitterGeneratesMapperFromPlan()
        => Given("a plan built from config piped through the emitter", EmitMapperFromPlan)
            .Then("the output contains the mapper class", code =>
                code.Contains("public static class Vendor_ClientMapper"))
            .And("the output contains a Map method", code =>
                code.Contains("public static IClient Map(Vendor.Client source)"))
            .And("the output maps the renamed member", code =>
                code.Contains("FetchUser = source.GetUser"))
            .And("the output maps the identity member", code =>
                code.Contains("Timeout = source.Timeout"))
            .AssertPassed();

    [Scenario("Full flow from config with object and enum type mappings produces correct mapper code")]
    [Fact]
    public Task FullFlowProducesCorrectMapperCode()
        => Given("a config with an object and an enum type mapping", FullFlowWithObjectAndEnum)
            .Then("the output contains the object mapper class", code =>
                code.Contains("public static class Vendor_ClientMapper"))
            .And("the output contains the object Map method", code =>
                code.Contains("public static IClient Map(Vendor.Client source)"))
            .And("the output contains the enum mapper class", code =>
                code.Contains("public static class Vendor_StatusCodeMapper"))
            .And("the enum mapper uses a cast", code =>
                code.Contains("return (StatusCode)source;"))
            .And("the output includes the auto-generated header", code =>
                code.Contains("// <auto-generated />"))
            .And("the output includes the generated namespace", code =>
                code.Contains("namespace WrapGod.Generated.Mappers;"))
            .AssertPassed();
}
