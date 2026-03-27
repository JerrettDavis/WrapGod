using FluentAssertions;
using Xunit;

namespace SampleTests.Before;

public class CollectionTests
{
    [Fact]
    public void Collection_ShouldContainItem()
    {
        var numbers = new List<int> { 1, 2, 3, 4, 5 };

        numbers.Should().Contain(3);
    }

    [Fact]
    public void Collection_ShouldHaveCount()
    {
        var numbers = new List<int> { 1, 2, 3 };

        numbers.Should().HaveCount(3);
    }

    [Fact]
    public void Collection_ShouldBeEmpty()
    {
        var empty = new List<string>();

        empty.Should().BeEmpty();
    }

    [Fact]
    public void Collection_ShouldNotContainNulls()
    {
        var items = new List<string> { "a", "b", "c" };

        items.Should().NotContainNulls();
    }

    [Fact]
    public void Collection_ShouldBeInAscendingOrder()
    {
        var numbers = new List<int> { 1, 2, 3, 4, 5 };

        numbers.Should().BeInAscendingOrder();
    }

    [Fact]
    public void Collection_ShouldContainSingle()
    {
        var numbers = new List<int> { 42 };

        numbers.Should().ContainSingle();
    }
}
