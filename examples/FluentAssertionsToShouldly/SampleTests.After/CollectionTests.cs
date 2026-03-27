using Shouldly;
using Xunit;

namespace SampleTests.After;

public class CollectionTests
{
    [Fact]
    public void Collection_ShouldContainItem()
    {
        var numbers = new List<int> { 1, 2, 3, 4, 5 };

        numbers.ShouldContain(3);
    }

    [Fact]
    public void Collection_ShouldHaveCount()
    {
        var numbers = new List<int> { 1, 2, 3 };

        numbers.Count.ShouldBe(3);
    }

    [Fact]
    public void Collection_ShouldBeEmpty()
    {
        var empty = new List<string>();

        empty.ShouldBeEmpty();
    }

    [Fact]
    public void Collection_ShouldNotContainNulls()
    {
        var items = new List<string> { "a", "b", "c" };

        items.ShouldAllBe(item => item != null);
    }

    [Fact]
    public void Collection_ShouldBeInAscendingOrder()
    {
        var numbers = new List<int> { 1, 2, 3, 4, 5 };

        numbers.ShouldBe(numbers.OrderBy(x => x));
    }

    [Fact]
    public void Collection_ShouldContainSingle()
    {
        var numbers = new List<int> { 42 };

        numbers.ShouldHaveSingleItem();
    }
}
