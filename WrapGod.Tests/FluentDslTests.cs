using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Fluent;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Fluent DSL configuration")]
public sealed class FluentDslTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static GenerationPlan BuildFullPlan() =>
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
            .Build();

    private static GenerationPlan BuildSingleWrappedType() =>
        WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WrapType("TestLib.Foo")
                .As("IFoo")
            .Build();

    private static GenerationPlan BuildRenamedMethod() =>
        WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WrapType("TestLib.Svc")
                .WrapMethod("DoWork").As("Execute")
            .Build();

    private static GenerationPlan BuildWrappedProperty() =>
        WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WrapType("TestLib.Svc")
                .WrapProperty("Timeout")
            .Build();

    private static GenerationPlan BuildExcludedMembers() =>
        WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WrapType("TestLib.Svc")
                .ExcludeMember("Dispose")
                .ExcludeMember("Finalize")
            .Build();

    private static GenerationPlan BuildWrapAllPublic() =>
        WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WrapType("TestLib.Logger")
                .WrapAllPublicMembers()
            .Build();

    private static GenerationPlan BuildTwoTypeMappings() =>
        WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .MapType("TestLib.Config", "MyApp.Config")
            .MapType("TestLib.Options", "MyApp.Options")
            .Build();

    private static GenerationPlan BuildTwoExclusionPatterns() =>
        WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .ExcludeType("TestLib.Internal*")
            .ExcludeType("TestLib.Debug*")
            .Build();

    private static GenerationPlan BuildStrictCompatibility() =>
        WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .WithCompatibilityMode("strict")
            .Build();

    private static GenerationPlan BuildDefaultCompatibility() =>
        WrapGodConfiguration.Create()
            .ForAssembly("TestLib")
            .Build();

    private static GenerationPlan BuildMixedConfig() =>
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
            .Build();

    // ── Scenarios ────────────────────────────────────────────────────

    [Scenario("Full fluent configuration produces a valid generation plan")]
    [Fact]
    public Task Build_ProducesValidGenerationPlan()
        => Given("a fully configured fluent plan", BuildFullPlan)
            .Then("the plan is not null", plan => plan is not null)
            .And("the assembly name is correct", plan => plan.AssemblyName == "Vendor.Lib")
            .And("there are two type directives", plan => plan.TypeDirectives.Count == 2)
            .And("there is one type mapping", plan => plan.TypeMappings.Count == 1)
            .And("there is one exclusion pattern", plan => plan.ExclusionPatterns.Count == 1)
            .AssertPassed();

    [Scenario("Type directives record source and target names")]
    [Fact]
    public Task TypeDirectives_RecordSourceAndTargetNames()
        => Given("a plan with a single wrapped type", BuildSingleWrappedType)
            .Then("there is exactly one directive", plan => plan.TypeDirectives.Count == 1)
            .And("the directive records the source type", plan =>
                plan.TypeDirectives[0].SourceType == "TestLib.Foo")
            .And("the directive records the target name", plan =>
                plan.TypeDirectives[0].TargetName == "IFoo")
            .AssertPassed();

    [Scenario("Method wrapping records rename")]
    [Fact]
    public Task MethodWrapping_RecordedWithRename()
        => Given("a plan with a renamed method", BuildRenamedMethod)
            .Then("there is exactly one type directive", plan => plan.TypeDirectives.Count == 1)
            .And("there is exactly one member directive", plan =>
                plan.TypeDirectives[0].MemberDirectives.Count == 1)
            .And("the member source name is correct", plan =>
                plan.TypeDirectives[0].MemberDirectives[0].SourceName == "DoWork")
            .And("the member target name is correct", plan =>
                plan.TypeDirectives[0].MemberDirectives[0].TargetName == "Execute")
            .And("the member kind is Method", plan =>
                plan.TypeDirectives[0].MemberDirectives[0].Kind == MemberDirectiveKind.Method)
            .AssertPassed();

    [Scenario("Property wrapping is recorded")]
    [Fact]
    public Task PropertyWrapping_Recorded()
        => Given("a plan with a wrapped property", BuildWrappedProperty)
            .Then("there is exactly one member directive", plan =>
                plan.TypeDirectives.Count == 1 && plan.TypeDirectives[0].MemberDirectives.Count == 1)
            .And("the member source name is Timeout", plan =>
                plan.TypeDirectives[0].MemberDirectives[0].SourceName == "Timeout")
            .And("the member target name is null", plan =>
                plan.TypeDirectives[0].MemberDirectives[0].TargetName is null)
            .And("the member kind is Property", plan =>
                plan.TypeDirectives[0].MemberDirectives[0].Kind == MemberDirectiveKind.Property)
            .AssertPassed();

    [Scenario("Excluded members are recorded")]
    [Fact]
    public Task ExcludeMember_Recorded()
        => Given("a plan with excluded members", BuildExcludedMembers)
            .Then("two members are excluded", plan =>
                plan.TypeDirectives.Count == 1 && plan.TypeDirectives[0].ExcludedMembers.Count == 2)
            .And("Dispose is in the exclusion list", plan =>
                plan.TypeDirectives[0].ExcludedMembers.Contains("Dispose"))
            .And("Finalize is in the exclusion list", plan =>
                plan.TypeDirectives[0].ExcludedMembers.Contains("Finalize"))
            .AssertPassed();

    [Scenario("WrapAllPublicMembers flag is set")]
    [Fact]
    public Task WrapAllPublicMembers_FlagSet()
        => Given("a plan with WrapAllPublicMembers enabled", BuildWrapAllPublic)
            .Then("the flag is true on the directive", plan =>
                plan.TypeDirectives.Count == 1 && plan.TypeDirectives[0].WrapAllPublicMembers)
            .AssertPassed();

    [Scenario("Type mappings are recorded")]
    [Fact]
    public Task TypeMappings_Recorded()
        => Given("a plan with two type mappings", BuildTwoTypeMappings)
            .Then("two mappings are recorded", plan => plan.TypeMappings.Count == 2)
            .And("first mapping source is correct", plan =>
                plan.TypeMappings[0].SourceType == "TestLib.Config")
            .And("first mapping destination is correct", plan =>
                plan.TypeMappings[0].DestinationType == "MyApp.Config")
            .And("second mapping source is correct", plan =>
                plan.TypeMappings[1].SourceType == "TestLib.Options")
            .And("second mapping destination is correct", plan =>
                plan.TypeMappings[1].DestinationType == "MyApp.Options")
            .AssertPassed();

    [Scenario("Exclusion patterns are recorded")]
    [Fact]
    public Task ExclusionPatterns_Recorded()
        => Given("a plan with two exclusion patterns", BuildTwoExclusionPatterns)
            .Then("two patterns are recorded", plan => plan.ExclusionPatterns.Count == 2)
            .And("Internal pattern is present", plan =>
                plan.ExclusionPatterns.Contains("TestLib.Internal*"))
            .And("Debug pattern is present", plan =>
                plan.ExclusionPatterns.Contains("TestLib.Debug*"))
            .AssertPassed();

    [Scenario("Compatibility mode is recorded")]
    [Fact]
    public Task CompatibilityMode_Recorded()
        => Given("a plan with strict compatibility mode", BuildStrictCompatibility)
            .Then("the compatibility mode is strict", plan =>
                plan.CompatibilityMode == "strict")
            .AssertPassed();

    [Scenario("Compatibility mode is null by default")]
    [Fact]
    public Task CompatibilityMode_NullByDefault()
        => Given("a plan with no compatibility mode set", BuildDefaultCompatibility)
            .Then("the compatibility mode is null", plan =>
                plan.CompatibilityMode is null)
            .AssertPassed();

    [Scenario("Multiple types with mixed configuration")]
    [Fact]
    public Task MultipleTypes_WithMixedConfig()
        => Given("a plan with two types having different configurations", BuildMixedConfig)
            .Then("there are two type directives", plan =>
                plan.TypeDirectives.Count == 2)
            .And("HTTP client source type is correct", plan =>
                plan.TypeDirectives[0].SourceType == "Vendor.Lib.HttpClient")
            .And("HTTP client target name is correct", plan =>
                plan.TypeDirectives[0].TargetName == "IHttpClient")
            .And("HTTP client has two member directives", plan =>
                plan.TypeDirectives[0].MemberDirectives.Count == 2)
            .And("HTTP client has one excluded member", plan =>
                plan.TypeDirectives[0].ExcludedMembers.Count == 1)
            .And("HTTP client does not wrap all public members", plan =>
                !plan.TypeDirectives[0].WrapAllPublicMembers)
            .And("Logger source type is correct", plan =>
                plan.TypeDirectives[1].SourceType == "Vendor.Lib.Logger")
            .And("Logger target name is correct", plan =>
                plan.TypeDirectives[1].TargetName == "ILogger")
            .And("Logger wraps all public members", plan =>
                plan.TypeDirectives[1].WrapAllPublicMembers)
            .And("Logger has no member directives", plan =>
                plan.TypeDirectives[1].MemberDirectives.Count == 0)
            .AssertPassed();
}
