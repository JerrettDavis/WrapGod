using NUnit.Framework;

namespace SampleTests;

/// <summary>
/// Parameterized test patterns: [TestCase], [TestCaseSource].
/// </summary>
[TestFixture]
public class ParameterizedTests
{
    // Scenario 5: [TestCase] with inline args
    [TestCase(1, 2, 3)]
    [TestCase(-1, -2, -3)]
    [TestCase(0, 0, 0)]
    [TestCase(100, 200, 300)]
    public void Add_WithTestCase_ReturnsExpected(int a, int b, int expected)
    {
        Assert.That(a + b, Is.EqualTo(expected));
    }

    // Scenario 6: [TestCase] with strings
    [TestCase("hello", "HELLO")]
    [TestCase("world", "WORLD")]
    [TestCase("", "")]
    public void ToUpper_ConvertsCorrectly(string input, string expected)
    {
        Assert.That(input.ToUpper(), Is.EqualTo(expected));
    }

    // Scenario 7: [TestCaseSource] with static method
    [TestCaseSource(nameof(GetDivisionData))]
    public void Divide_WithTestCaseSource_ReturnsExpected(double a, double b, double expected)
    {
        var result = a / b;
        Assert.That(result, Is.EqualTo(expected).Within(0.01));
    }

    public static IEnumerable<object[]> GetDivisionData()
    {
        yield return [10.0, 2.0, 5.0];
        yield return [9.0, 3.0, 3.0];
        yield return [7.0, 2.0, 3.5];
    }

    // Scenario 8: [TestCaseSource] with static property
    [TestCaseSource(nameof(SubtractionData))]
    public void Subtract_WithTestCaseSourceProperty_ReturnsExpected(int a, int b, int expected)
    {
        Assert.That(a - b, Is.EqualTo(expected));
    }

    public static IEnumerable<object[]> SubtractionData =>
    [
        [10, 3, 7],
        [0, 0, 0],
        [-5, -3, -2],
    ];

    // Scenario 9: [TestCaseSource] with class
    [TestCaseSource(typeof(ModuloTestData))]
    public void Modulo_WithTestCaseSourceClass_ReturnsExpected(int a, int b, int expected)
    {
        Assert.That(a % b, Is.EqualTo(expected));
    }
}

// Scenario 9 (cont): TestCaseSource class provider
public class ModuloTestData : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        yield return [10, 3, 1];
        yield return [20, 7, 6];
        yield return [15, 5, 0];
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
