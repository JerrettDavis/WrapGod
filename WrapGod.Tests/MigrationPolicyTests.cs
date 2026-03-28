using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Manifest.Config;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Migration policy modes control code fix aggressiveness")]
public sealed class MigrationPolicyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Safe policy only allows safe fixes")]
    [Fact]
    public Task SafePolicyOnlySafe() =>
        Given("safe migration policy", () => MigrationPolicyMode.Safe)
        .Then("allows safe fixes", policy =>
            MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Safe))
        .And("rejects assisted fixes", policy =>
            !MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Assisted))
        .And("rejects risky fixes", policy =>
            !MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Risky))
        .AssertPassed();

    [Scenario("Assisted policy allows safe and assisted fixes")]
    [Fact]
    public Task AssistedPolicyAllowsSafeAndAssisted() =>
        Given("assisted migration policy", () => MigrationPolicyMode.Assisted)
        .Then("allows safe fixes", policy =>
            MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Safe))
        .And("allows assisted fixes", policy =>
            MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Assisted))
        .And("rejects risky fixes", policy =>
            !MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Risky))
        .AssertPassed();

    [Scenario("Aggressive policy allows all fixes")]
    [Fact]
    public Task AggressivePolicyAllowsAll() =>
        Given("aggressive migration policy", () => MigrationPolicyMode.Aggressive)
        .Then("allows safe fixes", policy =>
            MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Safe))
        .And("allows assisted fixes", policy =>
            MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Assisted))
        .And("allows risky fixes", policy =>
            MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Risky))
        .AssertPassed();

    [Scenario("Unknown policy defaults to safe behavior")]
    [Fact]
    public Task UnknownPolicyDefaultsToSafe() =>
        Given("an unknown policy value cast to enum", () => (MigrationPolicyMode)99)
        .Then("allows safe fixes", policy =>
            MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Safe))
        .And("rejects assisted fixes", policy =>
            !MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Assisted))
        .And("rejects risky fixes", policy =>
            !MigrationPolicyEvaluator.ShouldOfferFix(policy, FixRiskLevel.Risky))
        .AssertPassed();

    [Scenario("WrapGodConfig accepts migration policy string")]
    [Fact]
    public Task ConfigAcceptsMigrationPolicy() =>
        Given("a WrapGodConfig with migration policy set", () =>
        {
            var config = new WrapGodConfig { MigrationPolicy = "aggressive" };
            return config;
        })
        .Then("policy property is set", config =>
            config.MigrationPolicy == "aggressive")
        .AssertPassed();

    [Scenario("WrapGodConfig migration policy defaults to null")]
    [Fact]
    public Task ConfigPolicyDefaultsToNull() =>
        Given("a default WrapGodConfig", () => new WrapGodConfig())
        .Then("policy is null by default", config =>
            config.MigrationPolicy is null)
        .AssertPassed();

    [Scenario("FixRiskLevel enum values are ordered by risk")]
    [Fact]
    public Task FixRiskLevelOrdering() =>
        Given("fix risk level enum values", () => true)
        .Then("Safe < Assisted", _ => FixRiskLevel.Safe < FixRiskLevel.Assisted)
        .And("Assisted < Risky", _ => FixRiskLevel.Assisted < FixRiskLevel.Risky)
        .AssertPassed();
}
