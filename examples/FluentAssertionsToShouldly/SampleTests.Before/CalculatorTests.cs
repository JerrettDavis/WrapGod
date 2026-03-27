using FluentAssertions;
using Xunit;

namespace SampleTests.Before;

public class Calculator
{
    public int Add(int a, int b) => a + b;
    public int Subtract(int a, int b) => a - b;
    public double Divide(int a, int b) =>
        b == 0 ? throw new DivideByZeroException() : (double)a / b;
}

public class CalculatorTests
{
    private readonly Calculator _calculator = new();

    [Fact]
    public void Add_ShouldReturnSum()
    {
        var result = _calculator.Add(2, 3);

        result.Should().Be(5);
    }

    [Fact]
    public void Add_WithNegativeNumbers_ShouldReturnCorrectResult()
    {
        var result = _calculator.Add(-1, -2);

        result.Should().Be(-3);
        result.Should().BeNegative();
    }

    [Fact]
    public void Subtract_ShouldReturnDifference()
    {
        var result = _calculator.Subtract(10, 3);

        result.Should().Be(7);
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Divide_ByZero_ShouldThrow()
    {
        var act = () => _calculator.Divide(1, 0);

        act.Should().Throw<DivideByZeroException>();
    }
}
