using NUnit.Framework;

namespace SampleTests;

/// <summary>
/// Assertion patterns covering the full NUnit Assert.That API surface.
/// </summary>
[TestFixture]
public class AssertionTests
{
    // Scenario 15: Assert.That + Is.EqualTo / Is.Not.EqualTo
    [Test]
    public void Equal_And_NotEqual()
    {
        Assert.That(40 + 2, Is.EqualTo(42));
        Assert.That(43, Is.Not.EqualTo(42));
    }

    // Scenario 16: Assert.That + Is.True / Is.False
    [Test]
    public void True_And_False()
    {
        Assert.That(1 < 2, Is.True);
        Assert.That(1 > 2, Is.False);
    }

    // Scenario 17: Assert.That + Is.Null / Is.Not.Null
    [Test]
    public void Null_And_NotNull()
    {
        string? nothing = null;
        string something = "hello";

        Assert.That(nothing, Is.Null);
        Assert.That(something, Is.Not.Null);
    }

    // Scenario 18: Assert.Throws<T>
    [Test]
    public void Throws_Exception()
    {
        var ex = Assert.Throws<DivideByZeroException>(() =>
        {
            int _ = 1 / int.Parse("0");
        });
        Assert.That(ex, Is.Not.Null);
    }

    // Scenario 19: Assert.ThrowsAsync<T>
    [Test]
    public async Task ThrowsAsync_Exception()
    {
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await Task.Delay(1);
            throw new InvalidOperationException("async failure");
        });
        Assert.That(ex!.Message, Does.Contain("async failure"));
    }

    // Scenario 20: Assert.That + Does.Contain / Does.Not.Contain (string)
    [Test]
    public void String_Contains()
    {
        var message = "Hello, World!";
        Assert.That(message, Does.Contain("World"));
        Assert.That(message, Does.Not.Contain("Goodbye"));
    }

    // Scenario 21: Assert.That + Does.Contain / Does.Not.Contain (collection)
    [Test]
    public void Collection_Contains()
    {
        var list = new List<int> { 1, 2, 3, 4, 5 };
        Assert.That(list, Does.Contain(3));
        Assert.That(list, Does.Not.Contain(99));
    }

    // Scenario 22: Assert.That + Is.Empty / Is.Not.Empty
    [Test]
    public void Empty_And_NotEmpty()
    {
        Assert.That(Array.Empty<int>(), Is.Empty);
        Assert.That(new[] { 1, 2, 3 }, Is.Not.Empty);
    }

    // Scenario 23: Assert.That + Is.TypeOf<T> / Is.AssignableFrom<T>
    [Test]
    public void Type_Checks()
    {
        object value = "test string";
        Assert.That(value, Is.TypeOf<string>());
        Assert.That(value, Is.AssignableFrom<string>());
    }

    // Scenario 24: Assert.That + Is.InRange
    [Test]
    public void InRange_Check()
    {
        var temperature = 72.5;
        Assert.That(temperature, Is.InRange(60.0, 80.0));
    }

    // Scenario 25: Multiple assertions on collection elements
    [Test]
    public void Collection_ElementInspection()
    {
        var items = new[] { "alpha", "beta", "gamma" };
        Assert.That(items, Has.Length.EqualTo(3));
        Assert.That(items[0], Is.EqualTo("alpha"));
        Assert.That(items[1], Is.EqualTo("beta"));
        Assert.That(items[2], Is.EqualTo("gamma"));
    }

    // Scenario 26: Assert.That + Has.All
    [Test]
    public void All_ItemsSatisfyCondition()
    {
        var numbers = new[] { 2, 4, 6, 8, 10 };
        Assert.That(numbers, Has.All.Matches<int>(n => n % 2 == 0));
    }

    // Scenario 27: Assert.That + Has.Count.EqualTo(1)
    [Test]
    public void Single_Element()
    {
        var list = new List<string> { "only" };
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0], Is.EqualTo("only"));
    }

    // Scenario 28: Assert.That + Does.StartWith / Does.EndWith
    [Test]
    public void String_StartsAndEndsWith()
    {
        var path = "/api/users/123";
        Assert.That(path, Does.StartWith("/api"));
        Assert.That(path, Does.EndWith("123"));
    }

    // Scenario 29: Assert.That + Does.Match (regex)
    [Test]
    public void Regex_Matches()
    {
        var email = "user@example.com";
        Assert.That(email, Does.Match(@"^[\w.+-]+@[\w-]+\.[\w.]+$"));
    }

    // Scenario 30: Assert.That + Is.EqualTo on collections
    [Test]
    public void Collection_Equality()
    {
        var expected = new[] { 1, 2, 3 };
        var actual = new List<int> { 1, 2, 3 };
        Assert.That(actual, Is.EqualTo(expected));
    }
}
