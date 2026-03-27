using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.TypeMap;
using WrapGod.TypeMap.Generation;
using Xunit.Abstractions;
using TypeMapping = WrapGod.TypeMap.TypeMapping;

namespace WrapGod.Tests;

[Feature("Type mapper code generation")]
public sealed class TypeMapperEmitterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // -- Helpers --------------------------------------------------------

    private static string EmitObjectMapping()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.Client",
                    DestinationType = "IClient",
                    Kind = TypeMappingKind.ObjectMapping,
                    MemberMappings = new List<MemberMapping>
                    {
                        new() { SourceMember = "GetUser", DestinationMember = "FetchUser" },
                        new() { SourceMember = "Timeout", DestinationMember = "Timeout" },
                    },
                },
            },
        };

        return TypeMapperEmitter.Emit(plan);
    }

    private static string EmitEnumMapping()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.StatusCode",
                    DestinationType = "StatusCode",
                    Kind = TypeMappingKind.Enum,
                },
            },
        };

        return TypeMapperEmitter.Emit(plan);
    }

    private static string EmitCollectionMapping()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.ItemList",
                    DestinationType = "IReadOnlyList<Item>",
                    Kind = TypeMappingKind.Collection,
                },
            },
        };

        return TypeMapperEmitter.Emit(plan);
    }

    private static string EmitConverterHookMapping()
    {
        var plan = new TypeMappingPlan
        {
            Mappings = new List<TypeMapping>
            {
                new()
                {
                    SourceType = "Vendor.DateTime",
                    DestinationType = "DateTimeOffset",
                    Kind = TypeMappingKind.Custom,
                    Converter = new ConverterRef
                    {
                        TypeName = "MyApp.Converters.DateTimeConverter",
                        MethodName = "ToOffset",
                    },
                },
            },
        };

        return TypeMapperEmitter.Emit(plan);
    }

    // -- Scenarios ------------------------------------------------------

    [Scenario("Object mapping generates property copy code")]
    [Fact]
    public Task ObjectMappingCodeGen()
        => Given("a plan with an object mapping and two members", EmitObjectMapping)
            .Then("the output contains the mapper class", code =>
                code.Contains("public static class Vendor_ClientMapper"))
            .And("the output contains the Map method signature", code =>
                code.Contains("public static IClient Map(Vendor.Client source)"))
            .And("the output copies the first member with rename", code =>
                code.Contains("FetchUser = source.GetUser"))
            .And("the output copies the second member by name", code =>
                code.Contains("Timeout = source.Timeout"))
            .And("the output creates a new destination instance", code =>
                code.Contains("return new IClient"))
            .AssertPassed();

    [Scenario("Enum mapping generates cast")]
    [Fact]
    public Task EnumMappingCodeGen()
        => Given("a plan with an enum mapping", EmitEnumMapping)
            .Then("the output contains the mapper class", code =>
                code.Contains("public static class Vendor_StatusCodeMapper"))
            .And("the output contains a cast expression", code =>
                code.Contains("return (StatusCode)source;"))
            .AssertPassed();

    [Scenario("Collection mapping generates Select call")]
    [Fact]
    public Task CollectionMappingCodeGen()
        => Given("a plan with a collection mapping", EmitCollectionMapping)
            .Then("the output contains the mapper class", code =>
                code.Contains("public static class Vendor_ItemListMapper"))
            .And("the output contains a Select projection", code =>
                code.Contains(".Select("))
            .And("the output calls ToList()", code =>
                code.Contains(".ToList()"))
            .AssertPassed();

    [Scenario("Converter hook is included in generated code")]
    [Fact]
    public Task ConverterHookCodeGen()
        => Given("a plan with a custom converter mapping", EmitConverterHookMapping)
            .Then("the output contains the mapper class", code =>
                code.Contains("public static class Vendor_DateTimeMapper"))
            .And("the output delegates to the converter", code =>
                code.Contains("MyApp.Converters.DateTimeConverter.ToOffset(source)"))
            .AssertPassed();
}
