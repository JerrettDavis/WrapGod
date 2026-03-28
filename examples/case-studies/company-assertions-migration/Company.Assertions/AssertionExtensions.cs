// ──────────────────────────────────────────────────────────────────
// Company.Assertions — WrapGod-generated facade over Shouldly
// ──────────────────────────────────────────────────────────────────
// This file represents what WrapGod's generator would produce:
// extension methods in the Company.Assertions namespace that fully
// wrap the Shouldly public API surface. Consumer code uses
// "using Company.Assertions;" exclusively — never "using Shouldly;".
//
// The enterprise owns this namespace. If the backing library changes
// (e.g., Shouldly → a custom engine), only this file regenerates.
// ──────────────────────────────────────────────────────────────────

namespace Company.Assertions;

/// <summary>
/// Generated assertion facade — delegates to Shouldly.
/// </summary>
public static class AssertionExtensions
{
    // ── Equality ─────────────────────────────────────────────────

    public static void ShouldBe<T>(this T actual, T expected)
        => Shouldly.ShouldBeTestExtensions.ShouldBe(actual, expected);

    public static void ShouldBe(this double actual, double expected)
        => Shouldly.ShouldBeTestExtensions.ShouldBe(actual, expected);

    // ── Null checks ──────────────────────────────────────────────

    public static T ShouldNotBeNull<T>(this T? actual) where T : class
        => Shouldly.ShouldBeNullExtensions.ShouldNotBeNull(actual);

    public static void ShouldBeNull<T>(this T? actual) where T : class
        => Shouldly.ShouldBeNullExtensions.ShouldBeNull(actual);

    // ── String checks ────────────────────────────────────────────

    public static void ShouldNotBeNullOrEmpty(this string? actual)
        => Shouldly.ShouldBeStringTestExtensions.ShouldNotBeNullOrEmpty(actual);

    public static void ShouldStartWith(this string? actual, string expected)
        => Shouldly.ShouldBeStringTestExtensions.ShouldStartWith(actual, expected);

    public static void ShouldEndWith(this string? actual, string expected)
        => Shouldly.ShouldBeStringTestExtensions.ShouldEndWith(actual, expected);

    public static void ShouldContain(this string? actual, string expected)
        => Shouldly.ShouldBeStringTestExtensions.ShouldContain(actual, expected);

    // ── Boolean checks ───────────────────────────────────────────

    public static void ShouldBeTrue(this bool actual)
        => Shouldly.ShouldBeBooleanExtensions.ShouldBeTrue(actual);

    public static void ShouldBeFalse(this bool actual)
        => Shouldly.ShouldBeBooleanExtensions.ShouldBeFalse(actual);

    // ── Comparison ───────────────────────────────────────────────

    public static void ShouldBeGreaterThan<T>(this T actual, T expected)
        where T : IComparable<T>
        => Shouldly.ShouldBeTestExtensions.ShouldBeGreaterThan(actual, expected);

    public static void ShouldBeGreaterThanOrEqualTo<T>(this T actual, T expected)
        where T : IComparable<T>
        => Shouldly.ShouldBeTestExtensions.ShouldBeGreaterThanOrEqualTo(actual, expected);

    public static void ShouldBeLessThanOrEqualTo<T>(this T actual, T expected)
        where T : IComparable<T>
        => Shouldly.ShouldBeTestExtensions.ShouldBeLessThanOrEqualTo(actual, expected);

    // ── Collection ───────────────────────────────────────────────

    public static void ShouldContain<T>(this IEnumerable<T> actual, T expected)
        => Shouldly.ShouldBeEnumerableTestExtensions.ShouldContain(actual, expected);

    public static void ShouldBeEmpty<T>(this IEnumerable<T> actual)
        => Shouldly.ShouldBeEnumerableTestExtensions.ShouldBeEmpty(actual);

    public static void ShouldNotBeEmpty<T>(this IEnumerable<T> actual)
        => Shouldly.ShouldBeEnumerableTestExtensions.ShouldNotBeEmpty(actual);

    // ── Company-specific extensions ──────────────────────────────

    /// <summary>
    /// Verifies that the value is between <paramref name="low"/> and
    /// <paramref name="high"/> (inclusive). A company-specific assertion
    /// Shouldly does not provide out of the box.
    /// </summary>
    public static void ShouldBeInRange<T>(this T actual, T low, T high)
        where T : IComparable<T>
    {
        actual.ShouldBeGreaterThanOrEqualTo(low);
        actual.ShouldBeLessThanOrEqualTo(high);
    }
}

/// <summary>
/// Generated static assertion facade — wraps Shouldly.Should.
/// </summary>
public static class Should
{
    public static TException Throw<TException>(Action actual) where TException : Exception
        => Shouldly.Should.Throw<TException>(actual);

    public static Task<TException> ThrowAsync<TException>(Func<Task> actual) where TException : Exception
        => Shouldly.Should.ThrowAsync<TException>(actual);
}
