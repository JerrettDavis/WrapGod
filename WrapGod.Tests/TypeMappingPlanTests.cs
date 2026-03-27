using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.TypeMap;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Type mapping core plan")]
public sealed class TypeMappingPlanTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // helpers
    private static TypeMappingPlan BuildFromJson()
    {
        const string json = """
        {
          "mappings": [
            {
              "sourceType": "Vendor.User",
              "destinationType": "Domain.User",
              "bidirectional": true,
              "members": [
                {
                  "sourceMember": "FirstName",
                  "destinationMember": "GivenName"
                }
              ]
            }
          ]
        }
        """;

        return TypeMappingPlanBuilder.FromJson(json);
    }

    private static TypeMappingPlan BuildFromAttributes() =>
        TypeMappingPlanBuilder.FromAssemblyAttributes(typeof(FakeMapHost).Assembly);

    [MapType("Vendor.User", "Domain.User", Bidirectional = true)]
    private sealed class FakeMapHost
    {
        [MapMember("FirstName", "GivenName")]
        public string FirstName { get; } = string.Empty;
    }

    [Scenario("JSON mapping configuration is parsed into a plan")]
    [Fact]
    public Task JsonMappingConfigurationIsParsedIntoPlan()
        => Given("a json mapping plan", BuildFromJson)
            .Then("it contains one mapping", p => p.Mappings.Count == 1)
            .And("it contains one member mapping", p => p.Mappings[0].Members.Count == 1)
            .AssertPassed();

    [Scenario("attribute mapping configuration is parsed into a plan")]
    [Fact]
    public Task AttributeMappingConfigurationIsParsedIntoPlan()
        => Given("an attributed assembly mapping plan", BuildFromAttributes)
            .Then("it includes the expected source type", p => p.Mappings.Any(m => m.SourceType == "Vendor.User"))
            .And("it includes the expected member map", p => p.Mappings.SelectMany(m => m.Members).Any(mm => mm.SourceMember == "FirstName"))
            .AssertPassed();
}
