using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Fluent;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Fluent DSL configuration")]
public partial class FluentDslTests : TinyBddXunitBase
{
    public FluentDslTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Full fluent configuration produces a valid generation plan")]
    [Fact]
    public async Task Build_ProducesValidGenerationPlan()
    {
        await Flow.Given("a fully configured fluent plan", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("Vendor.Lib")
                    .WrapType("Vendor.Lib.HttpClient")
                        .As("IHttpClient")
                        .WrapMethod("SendAsync").As("SendRequestAsync")
                        .WrapProperty("Timeout")
                        .ExcludeMember("Dispose")
                    .WrapType("Vendor.Lib.Logger")
                        .As("ILogger")
                        .WrapAllPublicMembers()
                    .MapType("Vendor.Lib.Config", "MyApp.Config")
                    .ExcludeType("Vendor.Lib.Internal*")
                    .Build())
            .Then("the plan is not null", plan => Assert.NotNull(plan))
            .And("the assembly name is correct", plan => Assert.Equal("Vendor.Lib", plan.AssemblyName))
            .And("there are two type directives", plan => Assert.Equal(2, plan.TypeDirectives.Count))
            .And("there is one type mapping", plan => Assert.Single(plan.TypeMappings))
            .And("there is one exclusion pattern", plan => Assert.Single(plan.ExclusionPatterns))
            .AssertPassed();
    }

    [Scenario("Type directives record source and target names")]
    [Fact]
    public async Task TypeDirectives_RecordSourceAndTargetNames()
    {
        await Flow.Given("a plan with a single wrapped type", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("TestLib")
                    .WrapType("TestLib.Foo")
                        .As("IFoo")
                    .Build())
            .Then("the directive records the source type", plan =>
            {
                var directive = Assert.Single(plan.TypeDirectives);
                Assert.Equal("TestLib.Foo", directive.SourceType);
                Assert.Equal("IFoo", directive.TargetName);
            })
            .AssertPassed();
    }

    [Scenario("Method wrapping records rename")]
    [Fact]
    public async Task MethodWrapping_RecordedWithRename()
    {
        await Flow.Given("a plan with a renamed method", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("TestLib")
                    .WrapType("TestLib.Svc")
                        .WrapMethod("DoWork").As("Execute")
                    .Build())
            .Then("the member directive records source, target, and kind", plan =>
            {
                var directive = Assert.Single(plan.TypeDirectives);
                var member = Assert.Single(directive.MemberDirectives);
                Assert.Equal("DoWork", member.SourceName);
                Assert.Equal("Execute", member.TargetName);
                Assert.Equal(MemberDirectiveKind.Method, member.Kind);
            })
            .AssertPassed();
    }

    [Scenario("Property wrapping is recorded")]
    [Fact]
    public async Task PropertyWrapping_Recorded()
    {
        await Flow.Given("a plan with a wrapped property", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("TestLib")
                    .WrapType("TestLib.Svc")
                        .WrapProperty("Timeout")
                    .Build())
            .Then("the member directive records source name and property kind", plan =>
            {
                var directive = Assert.Single(plan.TypeDirectives);
                var member = Assert.Single(directive.MemberDirectives);
                Assert.Equal("Timeout", member.SourceName);
                Assert.Null(member.TargetName);
                Assert.Equal(MemberDirectiveKind.Property, member.Kind);
            })
            .AssertPassed();
    }

    [Scenario("Excluded members are recorded")]
    [Fact]
    public async Task ExcludeMember_Recorded()
    {
        await Flow.Given("a plan with excluded members", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("TestLib")
                    .WrapType("TestLib.Svc")
                        .ExcludeMember("Dispose")
                        .ExcludeMember("Finalize")
                    .Build())
            .Then("two members are excluded", plan =>
            {
                var directive = Assert.Single(plan.TypeDirectives);
                Assert.Equal(2, directive.ExcludedMembers.Count);
                Assert.Contains("Dispose", directive.ExcludedMembers);
                Assert.Contains("Finalize", directive.ExcludedMembers);
            })
            .AssertPassed();
    }

    [Scenario("WrapAllPublicMembers flag is set")]
    [Fact]
    public async Task WrapAllPublicMembers_FlagSet()
    {
        await Flow.Given("a plan with WrapAllPublicMembers enabled", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("TestLib")
                    .WrapType("TestLib.Logger")
                        .WrapAllPublicMembers()
                    .Build())
            .Then("the flag is true on the directive", plan =>
            {
                var directive = Assert.Single(plan.TypeDirectives);
                Assert.True(directive.WrapAllPublicMembers);
            })
            .AssertPassed();
    }

    [Scenario("Type mappings are recorded")]
    [Fact]
    public async Task TypeMappings_Recorded()
    {
        await Flow.Given("a plan with two type mappings", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("TestLib")
                    .MapType("TestLib.Config", "MyApp.Config")
                    .MapType("TestLib.Options", "MyApp.Options")
                    .Build())
            .Then("both mappings are recorded with correct source and destination", plan =>
            {
                Assert.Equal(2, plan.TypeMappings.Count);
                Assert.Equal("TestLib.Config", plan.TypeMappings[0].SourceType);
                Assert.Equal("MyApp.Config", plan.TypeMappings[0].DestinationType);
                Assert.Equal("TestLib.Options", plan.TypeMappings[1].SourceType);
                Assert.Equal("MyApp.Options", plan.TypeMappings[1].DestinationType);
            })
            .AssertPassed();
    }

    [Scenario("Exclusion patterns are recorded")]
    [Fact]
    public async Task ExclusionPatterns_Recorded()
    {
        await Flow.Given("a plan with two exclusion patterns", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("TestLib")
                    .ExcludeType("TestLib.Internal*")
                    .ExcludeType("TestLib.Debug*")
                    .Build())
            .Then("both patterns are recorded", plan =>
            {
                Assert.Equal(2, plan.ExclusionPatterns.Count);
                Assert.Contains("TestLib.Internal*", plan.ExclusionPatterns);
                Assert.Contains("TestLib.Debug*", plan.ExclusionPatterns);
            })
            .AssertPassed();
    }

    [Scenario("Compatibility mode is recorded")]
    [Fact]
    public async Task CompatibilityMode_Recorded()
    {
        await Flow.Given("a plan with strict compatibility mode", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("TestLib")
                    .WithCompatibilityMode("strict")
                    .Build())
            .Then("the compatibility mode is strict", plan =>
                Assert.Equal("strict", plan.CompatibilityMode))
            .AssertPassed();
    }

    [Scenario("Compatibility mode is null by default")]
    [Fact]
    public async Task CompatibilityMode_NullByDefault()
    {
        await Flow.Given("a plan with no compatibility mode set", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("TestLib")
                    .Build())
            .Then("the compatibility mode is null", plan =>
                Assert.Null(plan.CompatibilityMode))
            .AssertPassed();
    }

    [Scenario("Multiple types with mixed configuration")]
    [Fact]
    public async Task MultipleTypes_WithMixedConfig()
    {
        await Flow.Given("a plan with two types having different configurations", () =>
                WrapGodConfiguration.Create()
                    .ForAssembly("Vendor.Lib")
                    .WrapType("Vendor.Lib.HttpClient")
                        .As("IHttpClient")
                        .WrapMethod("SendAsync").As("SendRequestAsync")
                        .WrapProperty("Timeout")
                        .ExcludeMember("Dispose")
                    .WrapType("Vendor.Lib.Logger")
                        .As("ILogger")
                        .WrapAllPublicMembers()
                    .Build())
            .Then("there are two type directives", plan =>
                Assert.Equal(2, plan.TypeDirectives.Count))
            .And("the HTTP client directive is correct", plan =>
            {
                var http = plan.TypeDirectives[0];
                Assert.Equal("Vendor.Lib.HttpClient", http.SourceType);
                Assert.Equal("IHttpClient", http.TargetName);
                Assert.Equal(2, http.MemberDirectives.Count);
                Assert.Single(http.ExcludedMembers);
                Assert.False(http.WrapAllPublicMembers);
            })
            .And("the Logger directive is correct", plan =>
            {
                var logger = plan.TypeDirectives[1];
                Assert.Equal("Vendor.Lib.Logger", logger.SourceType);
                Assert.Equal("ILogger", logger.TargetName);
                Assert.True(logger.WrapAllPublicMembers);
                Assert.Empty(logger.MemberDirectives);
            })
            .AssertPassed();
    }
}
