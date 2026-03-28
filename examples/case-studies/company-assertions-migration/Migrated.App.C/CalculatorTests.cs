using Company.Assertions;
using Xunit;

namespace Migrated.App.C;

public class CalculatorTests
{
    [Fact]
    public void Add_Two_Numbers()
    {
        var result = Calculator.Add(2, 3);

        result.ShouldBe(5);
    }

    [Fact]
    public void Subtract_Two_Numbers()
    {
        var result = Calculator.Subtract(10, 4);

        result.ShouldBe(6);
    }

    [Fact]
    public void Multiply_Returns_Positive_For_Same_Signs()
    {
        var result = Calculator.Multiply(-3, -4);

        result.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Divide_Returns_Expected_Result()
    {
        var result = Calculator.Divide(10, 2);

        result.ShouldBe(5.0);
    }

    [Fact]
    public void Divide_By_Zero_Throws()
    {
        Should.Throw<DivideByZeroException>(() => Calculator.Divide(10, 0));
    }

    [Fact]
    public void Sum_Of_Empty_List_Is_Zero()
    {
        var numbers = Array.Empty<int>();

        Calculator.Sum(numbers).ShouldBe(0);
    }

    [Fact]
    public void Sum_Of_List()
    {
        var numbers = new[] { 1, 2, 3, 4, 5 };

        Calculator.Sum(numbers).ShouldBe(15);
    }

    [Fact]
    public void IsEven_Returns_True_For_Even()
    {
        Calculator.IsEven(4).ShouldBeTrue();
    }

    [Fact]
    public void IsEven_Returns_False_For_Odd()
    {
        Calculator.IsEven(7).ShouldBeFalse();
    }

    [Fact]
    public void Factorial_Of_Zero_Is_One()
    {
        Calculator.Factorial(0).ShouldBe(1);
    }

    private static class Calculator
    {
        public static int Add(int a, int b) => a + b;
        public static int Subtract(int a, int b) => a - b;
        public static int Multiply(int a, int b) => a * b;

        public static double Divide(int a, int b) =>
            b == 0 ? throw new DivideByZeroException() : (double)a / b;

        public static int Sum(int[] numbers) => numbers.Sum();
        public static bool IsEven(int n) => n % 2 == 0;

        public static long Factorial(int n)
        {
            if (n < 0) throw new ArgumentException("Negative input.");
            long result = 1;
            for (var i = 2; i <= n; i++) result *= i;
            return result;
        }
    }
}
