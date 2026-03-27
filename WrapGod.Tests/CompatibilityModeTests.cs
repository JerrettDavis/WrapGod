using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Generator;
using Xunit.Abstractions;
using GenerationPlan = WrapGod.Generator.GenerationPlan;
using TypePlan = WrapGod.Generator.TypePlan;
using MemberPlan = WrapGod.Generator.MemberPlan;
using ParameterPlan = WrapGod.Generator.ParameterPlan;

namespace WrapGod.Tests;

[Feature("Compatibility generation modes: LCD, targeted, adaptive")]
public sealed class CompatibilityModeTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ── Helpers ──────────────────────────────────────────────────────

    private static readonly string[] Versions = ["1.0", "2.0", "3.0"];

    /// <summary>
    /// Creates a plan with three members:
    /// - CommonMethod: introduced in 1.0, never removed (present in all)
    /// - NewMethod: introduced in 2.0, never removed (present in 2.0+)
    /// - DeprecatedMethod: introduced in 1.0, removed in 3.0 (present in 1.0 and 2.0)
    /// </summary>
    private static GenerationPlan CreateTestPlan()
    {
        var members = new List<MemberPlan>
        {
            new MemberPlan(
                name: "CommonMethod",
                kind: "method",
                returnType: "void",
                parameters: Array.Empty<ParameterPlan>(),
                hasGetter: false,
                hasSetter: false,
                introducedIn: "1.0",
                removedIn: null),
            new MemberPlan(
                name: "NewMethod",
                kind: "method",
                returnType: "string",
                parameters: Array.Empty<ParameterPlan>(),
                hasGetter: false,
                hasSetter: false,
                introducedIn: "2.0",
                removedIn: null),
            new MemberPlan(
                name: "DeprecatedMethod",
                kind: "method",
                returnType: "int",
                parameters: Array.Empty<ParameterPlan>(),
                hasGetter: false,
                hasSetter: false,
                introducedIn: "1.0",
                removedIn: "3.0"),
        };

        var type = new TypePlan(
            "MyNamespace.MyClass", "MyClass", "MyNamespace", members,
            introducedIn: "1.0", removedIn: null);

        return new GenerationPlan("TestAssembly", new[] { type });
    }

    /// <summary>
    /// Creates a plan with two types:
    /// - StableType: present in all versions
    /// - NewType: introduced in 2.0 only
    /// </summary>
    private static GenerationPlan CreateMultiTypePlan()
    {
        var stableMembers = new List<MemberPlan>
        {
            new MemberPlan("Run", "method", "void", Array.Empty<ParameterPlan>(),
                false, false, introducedIn: "1.0", removedIn: null),
        };
        var newMembers = new List<MemberPlan>
        {
            new MemberPlan("Execute", "method", "void", Array.Empty<ParameterPlan>(),
                false, false, introducedIn: "2.0", removedIn: null),
        };

        var stableType = new TypePlan(
            "MyNamespace.StableType", "StableType", "MyNamespace", stableMembers,
            introducedIn: "1.0", removedIn: null);
        var newType = new TypePlan(
            "MyNamespace.NewType", "NewType", "MyNamespace", newMembers,
            introducedIn: "2.0", removedIn: null);

        return new GenerationPlan("TestAssembly", new[] { stableType, newType });
    }

    private static GenerationPlan ApplyLcd()
    {
        return CompatibilityFilter.Apply(CreateTestPlan(), CompatibilityMode.Lcd, Versions);
    }

    private static GenerationPlan ApplyTargetedV2()
    {
        return CompatibilityFilter.Apply(CreateTestPlan(), CompatibilityMode.Targeted, Versions, "2.0");
    }

    private static GenerationPlan ApplyTargetedV1()
    {
        return CompatibilityFilter.Apply(CreateTestPlan(), CompatibilityMode.Targeted, Versions, "1.0");
    }

    private static GenerationPlan ApplyAdaptive()
    {
        return CompatibilityFilter.Apply(CreateTestPlan(), CompatibilityMode.Adaptive, Versions);
    }

    private static GenerationPlan ApplyLcdMultiType()
    {
        return CompatibilityFilter.Apply(CreateMultiTypePlan(), CompatibilityMode.Lcd, Versions);
    }

    // ── LCD Scenarios ────────────────────────────────────────────────

    [Scenario("LCD mode filters to only members present in all versions")]
    [Fact]
    public Task Lcd_FiltersToCommonMembers()
        => Given("a plan with common, new, and deprecated members", ApplyLcd)
            .Then("only one member remains", plan =>
                plan.Types.Count == 1 && plan.Types[0].Members.Count == 1)
            .And("the remaining member is CommonMethod", plan =>
                plan.Types[0].Members[0].Name == "CommonMethod")
            .AssertPassed();

    [Scenario("LCD mode filters out types not present in all versions")]
    [Fact]
    public Task Lcd_FiltersOutNewTypes()
        => Given("a plan with a stable type and a type introduced in 2.0", ApplyLcdMultiType)
            .Then("only the stable type remains", plan =>
                plan.Types.Count == 1 && plan.Types[0].Name == "StableType")
            .AssertPassed();

    // ── Targeted Scenarios ───────────────────────────────────────────

    [Scenario("Targeted mode keeps only members present in the target version")]
    [Fact]
    public Task Targeted_KeepsTargetVersionMembers()
        => Given("a plan filtered for version 2.0", ApplyTargetedV2)
            .Then("all three members remain (all present at 2.0)", plan =>
                plan.Types[0].Members.Count == 3)
            .And("CommonMethod is included", plan =>
                plan.Types[0].Members.Any(m => m.Name == "CommonMethod"))
            .And("NewMethod is included", plan =>
                plan.Types[0].Members.Any(m => m.Name == "NewMethod"))
            .And("DeprecatedMethod is included (not yet removed at 2.0)", plan =>
                plan.Types[0].Members.Any(m => m.Name == "DeprecatedMethod"))
            .AssertPassed();

    [Scenario("Targeted mode for v1 excludes members not yet introduced")]
    [Fact]
    public Task Targeted_V1_ExcludesNewMembers()
        => Given("a plan filtered for version 1.0", ApplyTargetedV1)
            .Then("two members remain (CommonMethod and DeprecatedMethod)", plan =>
                plan.Types[0].Members.Count == 2)
            .And("NewMethod is excluded", plan =>
                !plan.Types[0].Members.Any(m => m.Name == "NewMethod"))
            .AssertPassed();

    [Scenario("Targeted mode throws when no target version is specified")]
    [Fact]
    public Task Targeted_ThrowsWithoutTargetVersion()
        => Given("a plan and targeted mode with no target version", CreateTestPlan)
            .Then("an ArgumentException is thrown", plan =>
            {
                try
                {
                    CompatibilityFilter.Apply(plan, CompatibilityMode.Targeted, Versions);
                    return false;
                }
                catch (ArgumentException)
                {
                    return true;
                }
            })
            .AssertPassed();

    // ── Adaptive Scenarios ───────────────────────────────────────────

    [Scenario("Adaptive mode includes all members with version metadata")]
    [Fact]
    public Task Adaptive_IncludesAllMembers()
        => Given("a plan filtered in adaptive mode", ApplyAdaptive)
            .Then("all three members remain", plan =>
                plan.Types[0].Members.Count == 3)
            .And("NewMethod retains its IntroducedIn metadata", plan =>
                plan.Types[0].Members.Any(m =>
                    m.Name == "NewMethod" && m.IntroducedIn == "2.0"))
            .And("DeprecatedMethod retains its RemovedIn metadata", plan =>
                plan.Types[0].Members.Any(m =>
                    m.Name == "DeprecatedMethod" && m.RemovedIn == "3.0"))
            .AssertPassed();

    [Scenario("Adaptive mode emits runtime version guards in facade source")]
    [Fact]
    public Task Adaptive_EmitsVersionGuards()
        => Given("a type plan with version-specific members", () =>
            {
                var members = new List<MemberPlan>
                {
                    new MemberPlan("NewMethod", "method", "string",
                        Array.Empty<ParameterPlan>(), false, false,
                        introducedIn: "2.0", removedIn: null),
                };
                return new TypePlan("MyNamespace.MyClass", "MyClass", "MyNamespace", members,
                    introducedIn: "1.0", removedIn: null);
            })
            .Then("the facade source contains a version availability check", type =>
            {
                bool previousMode = SourceEmitter.AdaptiveMode;
                try
                {
                    SourceEmitter.AdaptiveMode = true;
                    string source = SourceEmitter.EmitFacade(type);
                    return source.Contains("WrapGodVersionHelper.IsMemberAvailable")
                        && source.Contains("PlatformNotSupportedException")
                        && source.Contains("Available since 2.0");
                }
                finally
                {
                    SourceEmitter.AdaptiveMode = previousMode;
                }
            })
            .AssertPassed();

    [Scenario("Non-adaptive mode does not emit version guards")]
    [Fact]
    public Task NonAdaptive_NoVersionGuards()
        => Given("a type plan with version-specific members", () =>
            {
                var members = new List<MemberPlan>
                {
                    new MemberPlan("NewMethod", "method", "string",
                        Array.Empty<ParameterPlan>(), false, false,
                        introducedIn: "2.0", removedIn: null),
                };
                return new TypePlan("MyNamespace.MyClass", "MyClass", "MyNamespace", members,
                    introducedIn: "1.0", removedIn: null);
            })
            .Then("the facade source does not contain version guards", type =>
            {
                bool previousMode = SourceEmitter.AdaptiveMode;
                try
                {
                    SourceEmitter.AdaptiveMode = false;
                    string source = SourceEmitter.EmitFacade(type);
                    return !source.Contains("WrapGodVersionHelper.IsMemberAvailable")
                        && !source.Contains("PlatformNotSupportedException");
                }
                finally
                {
                    SourceEmitter.AdaptiveMode = previousMode;
                }
            })
            .AssertPassed();
}
