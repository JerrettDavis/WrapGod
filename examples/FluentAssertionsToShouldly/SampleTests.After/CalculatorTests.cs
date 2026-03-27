using Shouldly;
using Xunit;

namespace SampleTests.After;

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

        result.ShouldBe(5);
    }

    [Fact]
    public void Add_WithNegativeNumbers_ShouldReturnCorrectResult()
    {
        var result = _calculator.Add(-1, -2);

        result.ShouldBe(-3);
        result.ShouldBeLessThan(0);
    }

    [Fact]
    public void Subtract_ShouldReturnDifference()
    {
        var result = _calculator.Subtract(10, 3);

        result.ShouldBe(7);
        result.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Divide_ByZero_ShouldThrow()
    {
        Should.Throw<DivideByZeroException>(() => _calculator.Divide(1, 0));
    }
}
