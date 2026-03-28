using System.Collections;
using Xunit;

namespace SampleTests;

/// <summary>
/// Parameterized test patterns: [Theory], [InlineData], [MemberData], [ClassData].
/// </summary>
public class ParameterizedTests
{
    // Scenario 5: [Theory] + [InlineData] (was [TestCase])
    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(-1, -2, -3)]
    [InlineData(0, 0, 0)]
    [InlineData(100, 200, 300)]
    public void Add_WithInlineData_ReturnsExpected(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }

    // Scenario 6: [Theory] + [InlineData] with strings (was [TestCase])
    [Theory]
    [InlineData("hello", "HELLO")]
    [InlineData("world", "WORLD")]
    [InlineData("", "")]
    public void ToUpper_ConvertsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, input.ToUpper());
    }

    // Scenario 7: [Theory] + [MemberData] (was [TestCaseSource])
    [Theory]
    [MemberData(nameof(GetDivisionData))]
    public void Divide_WithMemberData_ReturnsExpected(double a, double b, double expected)
    {
        var result = a / b;
        Assert.Equal(expected, result, precision: 2);
    }

    public static IEnumerable<object[]> GetDivisionData()
    {
        yield return [10.0, 2.0, 5.0];
        yield return [9.0, 3.0, 3.0];
        yield return [7.0, 2.0, 3.5];
    }

    // Scenario 8: [Theory] + [MemberData] with property (was [TestCaseSource] property)
    [Theory]
    [MemberData(nameof(SubtractionData))]
    public void Subtract_WithMemberDataProperty_ReturnsExpected(int a, int b, int expected)
    {
        Assert.Equal(expected, a - b);
    }

    public static IEnumerable<object[]> SubtractionData =>
    [
        [10, 3, 7],
        [0, 0, 0],
        [-5, -3, -2],
    ];

    // Scenario 9: [Theory] + [ClassData] (was [TestCaseSource(typeof(...))])
    [Theory]
    [ClassData(typeof(ModuloTestData))]
    public void Modulo_WithClassData_ReturnsExpected(int a, int b, int expected)
    {
        Assert.Equal(expected, a % b);
    }
}

// Scenario 9 (cont): ClassData provider
public class ModuloTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return [10, 3, 1];
        yield return [20, 7, 6];
        yield return [15, 5, 0];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
