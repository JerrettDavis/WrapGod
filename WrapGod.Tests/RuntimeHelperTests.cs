using System;
using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Runtime;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("WrapGodVersionHelper: runtime availability checks for adaptive mode")]
public sealed class RuntimeHelperTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static Version ConfigureVersion(string version)
    {
        WrapGodVersionHelper.Reset();
        WrapGodVersionHelper.CurrentVersion = Version.Parse(version);
        return WrapGodVersionHelper.CurrentVersion;
    }

    // ── Availability checks ─────────────────────────────────────────

    [Scenario("Known type+member returns true when version is in range")]
    [Fact]
    public Task KnownMember_ReturnsTrue()
        => Given("the current version is 2.0.0", () => ConfigureVersion("2.0.0"))
            .Then("a member introduced in 1.0.0 with no removal is available", _ =>
                WrapGodVersionHelper.IsMemberAvailable("1.0.0", null))
            .AssertPassed();

    [Scenario("Nonexistent member returns false when version is below introduction")]
    [Fact]
    public Task NonexistentMember_ReturnsFalse()
        => Given("the current version is 1.0.0", () => ConfigureVersion("1.0.0"))
            .Then("a member introduced in 2.0.0 is not available", _ =>
                !WrapGodVersionHelper.IsMemberAvailable("2.0.0", null))
            .AssertPassed();

    [Scenario("Removed member returns false when version is at or past removal")]
    [Fact]
    public Task RemovedMember_ReturnsFalse()
        => Given("the current version is 3.0.0", () => ConfigureVersion("3.0.0"))
            .Then("a member introduced in 1.0.0 and removed in 3.0.0 is not available", _ =>
                !WrapGodVersionHelper.IsMemberAvailable("1.0.0", "3.0.0"))
            .AssertPassed();

    [Scenario("Member is available just before removal version")]
    [Fact]
    public Task MemberBeforeRemoval_ReturnsTrue()
        => Given("the current version is 2.9.0", () => ConfigureVersion("2.9.0"))
            .Then("a member introduced in 1.0.0 and removed in 3.0.0 is still available", _ =>
                WrapGodVersionHelper.IsMemberAvailable("1.0.0", "3.0.0"))
            .AssertPassed();

    // ── Caching ─────────────────────────────────────────────────────

    [Scenario("Result is cached across repeated lookups")]
    [Fact]
    public Task Result_IsCached()
        => Given("the current version is 2.0.0", () => ConfigureVersion("2.0.0"))
            .Then("two identical calls return the same result without error", _ =>
            {
                bool first = WrapGodVersionHelper.IsMemberAvailable("1.0.0", null);
                bool second = WrapGodVersionHelper.IsMemberAvailable("1.0.0", null);
                return first == second && first;
            })
            .AssertPassed();

    // ── Edge cases ──────────────────────────────────────────────────

    [Scenario("Throws when CurrentVersion has not been configured")]
    [Fact]
    public Task ThrowsWhenNotConfigured()
        => Given("the helper has been reset", () =>
            {
                WrapGodVersionHelper.Reset();
                return "reset";
            })
            .Then("calling IsMemberAvailable throws InvalidOperationException", _ =>
            {
                try
                {
                    WrapGodVersionHelper.IsMemberAvailable("1.0", null);
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            })
            .AssertPassed();

    [Scenario("Short version strings (e.g. '2.0') are handled correctly")]
    [Fact]
    public Task ShortVersionStrings_WorkCorrectly()
        => Given("the current version is 2.0", () => ConfigureVersion("2.0"))
            .Then("a member introduced in '2.0' is available", _ =>
                WrapGodVersionHelper.IsMemberAvailable("2.0", null))
            .And("a member introduced in '3.0' is not available", _ =>
                !WrapGodVersionHelper.IsMemberAvailable("3.0", null))
            .AssertPassed();
}
