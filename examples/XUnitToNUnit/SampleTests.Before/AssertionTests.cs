using Xunit;

namespace SampleTests;

/// <summary>
/// Assertion patterns covering the full xUnit Assert API surface.
/// </summary>
public class AssertionTests
{
    // Scenario 15: Assert.Equal / Assert.NotEqual
    [Fact]
    public void Equal_And_NotEqual()
    {
        Assert.Equal(42, 40 + 2);
        Assert.NotEqual(42, 43);
    }

    // Scenario 16: Assert.True / Assert.False
    [Fact]
    public void True_And_False()
    {
        Assert.True(1 < 2);
        Assert.False(1 > 2);
    }

    // Scenario 17: Assert.Null / Assert.NotNull
    [Fact]
    public void Null_And_NotNull()
    {
        string? nothing = null;
        string something = "hello";

        Assert.Null(nothing);
        Assert.NotNull(something);
    }

    // Scenario 18: Assert.Throws<T>
    [Fact]
    public void Throws_Exception()
    {
        var ex = Assert.Throws<DivideByZeroException>(() =>
        {
            int _ = 1 / int.Parse("0");
        });
        Assert.NotNull(ex);
    }

    // Scenario 19: Assert.ThrowsAsync<T>
    [Fact]
    public async Task ThrowsAsync_Exception()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("async failure");
        });
        Assert.Contains("async failure", ex.Message);
    }

    // Scenario 20: Assert.Contains / Assert.DoesNotContain (string)
    [Fact]
    public void String_Contains()
    {
        var message = "Hello, World!";
        Assert.Contains("World", message);
        Assert.DoesNotContain("Goodbye", message);
    }

    // Scenario 21: Assert.Contains / Assert.DoesNotContain (collection)
    [Fact]
    public void Collection_Contains()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        Assert.Contains(3, list);
        Assert.DoesNotContain(99, list);
    }

    // Scenario 22: Assert.Empty / Assert.NotEmpty
    [Fact]
    public void Empty_And_NotEmpty()
    {
        Assert.Empty(Array.Empty<int>());
        Assert.NotEmpty(new[] { 1, 2, 3 });
    }

    // Scenario 23: Assert.IsType<T> / Assert.IsAssignableFrom<T>
    [Fact]
    public void Type_Checks()
    {
        object value = "test string";
        Assert.IsType<string>(value);
        Assert.IsAssignableFrom<IComparable>(value);
    }

    // Scenario 24: Assert.InRange
    [Fact]
    public void InRange_Check()
    {
        var temperature = 72.5;
        Assert.InRange(temperature, 60.0, 80.0);
    }

    // Scenario 25: Assert.Collection
    [Fact]
    public void Collection_ElementInspection()
    {
        var items = new[] { "alpha", "beta", "gamma" };
        Assert.Collection(items,
            item => Assert.Equal("alpha", item),
            item => Assert.Equal("beta", item),
            item => Assert.Equal("gamma", item));
    }

    // Scenario 26: Assert.All
    [Fact]
    public void All_ItemsSatisfyCondition()
    {
        var numbers = new[] { 2, 4, 6, 8, 10 };
        Assert.All(numbers, n => Assert.True(n % 2 == 0));
    }

    // Scenario 27: Assert.Single
    [Fact]
    public void Single_Element()
    {
        var list = new List<string> { "only" };
        var item = Assert.Single(list);
        Assert.Equal("only", item);
    }

    // Scenario 28: Assert.StartsWith / Assert.EndsWith
    [Fact]
    public void String_StartsAndEndsWith()
    {
        var path = "/api/users/123";
        Assert.StartsWith("/api", path);
        Assert.EndsWith("123", path);
    }

    // Scenario 29: Assert.Matches (regex)
    [Fact]
    public void Regex_Matches()
    {
        var email = "user@example.com";
        Assert.Matches(@"^[\w.+-]+@[\w-]+\.[\w.]+$", email);
    }

    // Scenario 30: Assert.Equal on collections
    [Fact]
    public void Collection_Equality()
    {
        var expected = new[] { 1, 2, 3 };
        var actual = new List<int> { 1, 2, 3 };
        Assert.Equal(expected, actual);
    }
}
