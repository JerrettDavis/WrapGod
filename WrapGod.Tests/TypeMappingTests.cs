using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Abstractions.Config;
using WrapGod.TypeMap;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Type mapping core")]
public sealed class TypeMappingTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static TypeMappingPlan BuildObjectMappingPlan()
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

    private static TypeMappingPlan BuildEnumMappingPlan()
    {
        var config = new WrapGodConfig();
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

        return TypeMappingPlanner.BuildPlan(config, overrides);
    }

    private static TypeMappingPlan BuildCollectionMappingPlan()
    {
        var config = new WrapGodConfig();
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.ItemList",
            Include = true,
            TargetName = "IReadOnlyList<Item>",
        });

        var overrides = new List<TypeMappingOverride>
        {
            new() { SourceType = "Vendor.ItemList", Kind = TypeMappingKind.Collection },
        };

        return TypeMappingPlanner.BuildPlan(config, overrides);
    }

    private static TypeMappingPlan BuildNullableMappingPlan()
    {
        var config = new WrapGodConfig();
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.NullableInt",
            Include = true,
            TargetName = "int?",
        });

        var overrides = new List<TypeMappingOverride>
        {
            new() { SourceType = "Vendor.NullableInt", Kind = TypeMappingKind.Nullable },
        };

        return TypeMappingPlanner.BuildPlan(config, overrides);
    }

    private static TypeMappingPlan BuildConverterHookPlan()
    {
        var config = new WrapGodConfig();
        config.Types.Add(new TypeConfig
        {
            SourceType = "Vendor.DateTime",
            Include = true,
            TargetName = "DateTimeOffset",
        });

        var overrides = new List<TypeMappingOverride>
        {
            new()
            {
                SourceType = "Vendor.DateTime",
                Kind = TypeMappingKind.Custom,
                Converter = new ConverterRef
                {
                    TypeName = "MyApp.Converters.DateTimeConverter",
                    MethodName = "ToOffset",
                },
            },
        };

        return TypeMappingPlanner.BuildPlan(config, overrides);
    }

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("Object mapping plan records source, destination, and members")]
    [Fact]
    public Task ObjectMappingPlanCreation()
        => Given("a config with an object type and two members", BuildObjectMappingPlan)
            .Then("there is exactly one mapping", plan => plan.Mappings.Count == 1)
            .And("the mapping kind is ObjectMapping", plan =>
                plan.Mappings[0].Kind == TypeMappingKind.ObjectMapping)
            .And("the source type is correct", plan =>
                plan.Mappings[0].SourceType == "Vendor.Client")
            .And("the destination type is correct", plan =>
                plan.Mappings[0].DestinationType == "IClient")
            .And("there are two member mappings", plan =>
                plan.Mappings[0].MemberMappings.Count == 2)
            .And("the first member is renamed", plan =>
                plan.Mappings[0].MemberMappings[0].SourceMember == "GetUser"
                && plan.Mappings[0].MemberMappings[0].DestinationMember == "FetchUser")
            .And("the second member keeps its name", plan =>
                plan.Mappings[0].MemberMappings[1].SourceMember == "Timeout"
                && plan.Mappings[0].MemberMappings[1].DestinationMember == "Timeout")
            .And("the mapping is findable by source type", plan =>
                plan.FindBySourceType("Vendor.Client") is not null)
            .AssertPassed();

    [Scenario("Enum mapping plan uses Enum kind")]
    [Fact]
    public Task EnumMappingPlan()
        => Given("a config with an enum type override", BuildEnumMappingPlan)
            .Then("there is exactly one mapping", plan => plan.Mappings.Count == 1)
            .And("the mapping kind is Enum", plan =>
                plan.Mappings[0].Kind == TypeMappingKind.Enum)
            .And("the source type is correct", plan =>
                plan.Mappings[0].SourceType == "Vendor.StatusCode")
            .And("the destination type is correct", plan =>
                plan.Mappings[0].DestinationType == "StatusCode")
            .AssertPassed();

    [Scenario("Collection mapping plan uses Collection kind")]
    [Fact]
    public Task CollectionMappingPlan()
        => Given("a config with a collection type override", BuildCollectionMappingPlan)
            .Then("there is exactly one mapping", plan => plan.Mappings.Count == 1)
            .And("the mapping kind is Collection", plan =>
                plan.Mappings[0].Kind == TypeMappingKind.Collection)
            .And("the destination type is correct", plan =>
                plan.Mappings[0].DestinationType == "IReadOnlyList<Item>")
            .AssertPassed();

    [Scenario("Nullable mapping plan uses Nullable kind")]
    [Fact]
    public Task NullableMappingPlan()
        => Given("a config with a nullable type override", BuildNullableMappingPlan)
            .Then("there is exactly one mapping", plan => plan.Mappings.Count == 1)
            .And("the mapping kind is Nullable", plan =>
                plan.Mappings[0].Kind == TypeMappingKind.Nullable)
            .And("the destination type is correct", plan =>
                plan.Mappings[0].DestinationType == "int?")
            .AssertPassed();

    [Scenario("Converter hook is registered and accessible on the mapping")]
    [Fact]
    public Task ConverterHookRegistration()
        => Given("a config with a custom converter override", BuildConverterHookPlan)
            .Then("there is exactly one mapping", plan => plan.Mappings.Count == 1)
            .And("the mapping kind is Custom", plan =>
                plan.Mappings[0].Kind == TypeMappingKind.Custom)
            .And("the converter type name is correct", plan =>
                plan.Mappings[0].Converter is { TypeName: "MyApp.Converters.DateTimeConverter" })
            .And("the converter method name is correct", plan =>
                plan.Mappings[0].Converter is { MethodName: "ToOffset" })
            .AssertPassed();
}
