// VersionMatrixTests.cs
// Demonstrates assertions that differ across FluentAssertions v5, v6, and v8.
// This file is NOT compilable as-is — it shows version-conditional patterns
// that WrapGod's version-matrix strategies must handle.

using FluentAssertions;
using FluentAssertions.Execution;
using Xunit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WrapGod.Examples.VersionMatrix;

public class VersionMatrixTests
{
    // -----------------------------------------------------------------------
    // 1. Exception assertions: entry point changed between v5 and v6
    // -----------------------------------------------------------------------

    [Fact]
    public void ExceptionAssertion_V5Style()
    {
        // v5: Action has a Should() extension returning ActionAssertions
        Action act = () => throw new InvalidOperationException("boom");

        // This compiles in v5 only — removed in v6+
        // act.Should().Throw<InvalidOperationException>();

        // v6+ equivalent:
        act.Should().Throw<InvalidOperationException>();
        // Note: In v6+, Throw<T>() is on DelegateAssertions<TDelegate>,
        // accessed via the same Should() syntax but different internal type.
    }

    [Fact]
    public void ExceptionAssertion_V6PlusStyle()
    {
        // v6+: Use FluentActions.Invoking for subject-based exception testing
        var calculator = new Calculator();

        FluentActions.Invoking(() => calculator.Divide(1, 0))
            .Should().Throw<DivideByZeroException>();
    }

    // -----------------------------------------------------------------------
    // 2. ThrowExactly: available in v6+ only
    // -----------------------------------------------------------------------

    [Fact]
    public void ThrowExactly_V6Plus()
    {
        Action act = () => throw new ArgumentNullException("param");

        // v6+: ThrowExactly asserts the exact type, not subclasses
        act.Should().ThrowExactly<ArgumentNullException>();

        // v5 workaround:
        // act.Should().Throw<ArgumentNullException>()
        //     .And.GetType().Should().Be(typeof(ArgumentNullException));
    }

    // -----------------------------------------------------------------------
    // 3. BooleanAssertions.NotBe: added in v6
    // -----------------------------------------------------------------------

    [Fact]
    public void BooleanNotBe_V6Plus()
    {
        bool isEnabled = true;

        // v6+: NotBe is available
        isEnabled.Should().NotBe(false);

        // v5 workaround:
        // isEnabled.Should().Be(!false);  // or: isEnabled.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // 4. BeEquivalentTo: signature changed in v8 (added config parameter)
    // -----------------------------------------------------------------------

    [Fact]
    public void BeEquivalentTo_SignatureChange()
    {
        var actual = new { Name = "Alice", Age = 30 };
        var expected = new { Name = "Alice", Age = 30 };

        // All versions: basic call still works
        actual.Should().BeEquivalentTo(expected);

        // v8 only: inline equivalency configuration
        // actual.Should().BeEquivalentTo(expected, options => options
        //     .Excluding(o => o.Age));
    }

    // -----------------------------------------------------------------------
    // 5. Execute.Assertion vs AssertionChain: custom assertion infrastructure
    // -----------------------------------------------------------------------

    [Fact]
    public void CustomAssertion_VersionDifference()
    {
        // v5/v6: Custom assertions use Execute.Assertion
        // using (var scope = new AssertionScope())
        // {
        //     Execute.Assertion
        //         .ForCondition(true)
        //         .FailWith("Should not fail");
        // }

        // v8: Custom assertions use AssertionChain.GetOrCreate()
        // var chain = AssertionChain.GetOrCreate();
        // chain.ForCondition(true)
        //     .FailWith("Should not fail");
    }

    // -----------------------------------------------------------------------
    // 6. StringAssertions.NotBeNullOrWhiteSpace: added in v6
    // -----------------------------------------------------------------------

    [Fact]
    public void StringNotBeNullOrWhiteSpace_V6Plus()
    {
        string value = "hello";

        // v6+: Direct assertion
        value.Should().NotBeNullOrWhiteSpace();

        // v5 workaround:
        // value.Should().NotBeNullOrEmpty();
        // value.Trim().Should().NotBeEmpty();
    }

    // -----------------------------------------------------------------------
    // 7. NumericAssertions.BeCloseTo: added in v6
    // -----------------------------------------------------------------------

    [Fact]
    public void NumericBeCloseTo_V6Plus()
    {
        double result = 3.14159;

        // v6+: BeCloseTo with delta
        // result.Should().BeCloseTo(3.14, 0.01);

        // v5 workaround:
        // result.Should().BeInRange(3.13, 3.15);
    }

    // -----------------------------------------------------------------------
    // 8. Satisfy: v8-only nested assertion pattern
    // -----------------------------------------------------------------------

    [Fact]
    public void Satisfy_V8Only()
    {
        object value = "hello world";

        // v8 only: Satisfy enables nested assertions via inspector
        // value.Should().Satisfy<string>(s =>
        //     s.Should().StartWith("hello").And.EndWith("world"));

        // v5/v6 workaround:
        value.Should().BeOfType<string>()
            .Which.Should().StartWith("hello")
            .And.Subject.Should().EndWith("world");
    }

    // -----------------------------------------------------------------------
    // 9. Collection assertions: stable across versions (LCD-safe)
    // -----------------------------------------------------------------------

    [Fact]
    public void CollectionAssertions_StableAcrossVersions()
    {
        var items = new List<int> { 1, 2, 3 };

        // These work identically across v5, v6, and v8
        items.Should().HaveCount(3);
        items.Should().Contain(2);
        items.Should().BeInAscendingOrder();
        items.Should().NotContainNulls();
        items.Should().ContainSingle(x => x == 2);
    }

    // -----------------------------------------------------------------------
    // 10. Global equivalency configuration: renamed in v8
    // -----------------------------------------------------------------------

    // v5/v6:
    // AssertionOptions.EquivalencyPlan.Using<T>(new CustomStep());

    // v8:
    // AssertionConfiguration.Current.Equivalency.Modify(opts =>
    //     opts.Using(new CustomStep()));

    // -----------------------------------------------------------------------
    // Helper class for exception tests
    // -----------------------------------------------------------------------

    private class Calculator
    {
        public int Add(int a, int b) => a + b;
        public double Divide(int a, int b)
        {
            if (b == 0) throw new DivideByZeroException();
            return (double)a / b;
        }
    }
}
