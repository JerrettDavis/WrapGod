using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Manifest.Config;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Analyzer migration policy coverage")]
public sealed class AnalyzerPolicyCoverageTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Safe policy allows safe fixes only")]
    [Fact]
    public Task SafePolicyAllowsSafeFixes()
        => Given("Safe migration policy", () => MigrationPolicyMode.Safe)
            .Then("safe fix is allowed", policy =>
                MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Safe))
            .And("assisted fix is blocked", policy =>
                !MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Assisted))
            .And("risky fix is blocked", policy =>
                !MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Risky))
            .AssertPassed();

    [Scenario("Assisted policy allows safe and assisted fixes")]
    [Fact]
    public Task AssistedPolicyAllowsSafeAndAssisted()
        => Given("Assisted migration policy", () => MigrationPolicyMode.Assisted)
            .Then("safe fix is allowed", policy =>
                MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Safe))
            .And("assisted fix is allowed", policy =>
                MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Assisted))
            .And("risky fix is blocked", policy =>
                !MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Risky))
            .AssertPassed();

    [Scenario("Aggressive policy allows all fixes")]
    [Fact]
    public Task AggressivePolicyAllowsAll()
        => Given("Aggressive migration policy", () => MigrationPolicyMode.Aggressive)
            .Then("safe fix is allowed", policy =>
                MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Safe))
            .And("assisted fix is allowed", policy =>
                MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Assisted))
            .And("risky fix is allowed", policy =>
                MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Risky))
            .AssertPassed();

    [Scenario("Default policy fallback is Safe")]
    [Fact]
    public Task DefaultPolicyIsSafe()
        => Given("default enum value", () => default(MigrationPolicyMode))
            .Then("defaults to Safe behavior", policy =>
                MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Safe) &&
                !MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Risky))
            .AssertPassed();

    [Scenario("FixRiskLevel enum values are ordered")]
    [Fact]
    public Task FixRiskLevelOrdering()
        => Given("risk level values", () => true)
            .Then("Safe < Assisted < Risky", _ =>
                (int)FixRiskLevel.Safe < (int)FixRiskLevel.Assisted &&
                (int)FixRiskLevel.Assisted < (int)FixRiskLevel.Risky)
            .AssertPassed();
}
