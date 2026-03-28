using NUnit.Framework;

namespace SampleTests;

/// <summary>
/// Basic test discovery attributes: [Test], [Ignore], [Category].
/// </summary>
[TestFixture]
public class BasicTests
{
    // Scenario 1: Simple [Test]
    [Test]
    public void Add_ReturnsSum()
    {
        var result = 2 + 3;
        Assert.That(result, Is.EqualTo(5));
    }

    // Scenario 2: [Ignore("reason")]
    [Test]
    [Ignore("Demonstrates skipped test migration")]
    public void Skipped_Test()
    {
        Assert.That(false, Is.True, "This test should never run");
    }

    // Scenario 3: [Category] for categorization
    [Test]
    [Category("Math")]
    public void Subtract_ReturnsDifference()
    {
        var result = 10 - 3;
        Assert.That(result, Is.EqualTo(7));
    }

    // Scenario 4: [Category] + [Property]
    [Test]
    [Category("Math")]
    [Property("Priority", "High")]
    public void Multiply_ReturnsProduct()
    {
        var result = 4 * 5;
        Assert.That(result, Is.EqualTo(20));
    }
}
