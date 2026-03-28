using Xunit;

namespace SampleTests;

/// <summary>
/// Basic test discovery attributes: [Fact], [Fact(Skip=...)], [Trait].
/// </summary>
public class BasicTests
{
    // Scenario 1: Simple [Fact] (was [Test])
    [Fact]
    public void Add_ReturnsSum()
    {
        var result = 2 + 3;
        Assert.Equal(5, result);
    }

    // Scenario 2: [Fact(Skip = "reason")] (was [Ignore])
    [Fact(Skip = "Demonstrates skipped test migration")]
    public void Skipped_Test()
    {
        Assert.True(false, "This test should never run");
    }

    // Scenario 3: [Trait] (was [Category])
    [Fact]
    [Trait("Category", "Math")]
    public void Subtract_ReturnsDifference()
    {
        var result = 10 - 3;
        Assert.Equal(7, result);
    }

    // Scenario 4: [Trait] for non-category properties (was [Category] + [Property])
    [Fact]
    [Trait("Category", "Math")]
    [Trait("Priority", "High")]
    public void Multiply_ReturnsProduct()
    {
        var result = 4 * 5;
        Assert.Equal(20, result);
    }
}
