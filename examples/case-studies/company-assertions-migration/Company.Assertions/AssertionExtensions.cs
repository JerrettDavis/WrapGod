// Company.Assertions wraps Shouldly. All consuming projects reference
// Company.Assertions instead of Shouldly directly, centralizing the
// assertion dependency across the enterprise.
//
// Consumers add:
//   using Shouldly;                -- for standard Shouldly assertions
//   using Company.Assertions;     -- for company-specific extensions (optional)
//
// The transitive dependency on Shouldly flows through the project reference,
// so consumers never need a direct PackageReference to Shouldly.

using Shouldly;

namespace Company.Assertions;

/// <summary>
/// Company-specific assertion extensions built on top of Shouldly.
/// Add domain-specific assertion helpers here as needed.
/// </summary>
public static class AssertionExtensions
{
    /// <summary>
    /// Verifies that the value is between <paramref name="low"/> and
    /// <paramref name="high"/> (inclusive). A common domain assertion that
    /// Shouldly doesn't provide out of the box.
    /// </summary>
    public static void ShouldBeInRange<T>(this T actual, T low, T high)
        where T : IComparable<T>
    {
        actual.ShouldBeGreaterThanOrEqualTo(low);
        actual.ShouldBeLessThanOrEqualTo(high);
    }
}
