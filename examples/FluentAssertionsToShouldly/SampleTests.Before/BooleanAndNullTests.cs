using FluentAssertions;
using Xunit;

namespace SampleTests.Before;

public class BooleanAndNullTests
{
    [Fact]
    public void Boolean_ShouldBeTrue()
    {
        var flag = true;

        flag.Should().BeTrue();
    }

    [Fact]
    public void Boolean_ShouldBeFalse()
    {
        var flag = false;

        flag.Should().BeFalse();
    }

    [Fact]
    public void Object_ShouldBeNull()
    {
        string? value = null;

        value.Should().BeNull();
    }

    [Fact]
    public void Object_ShouldNotBeNull()
    {
        var value = "something";

        value.Should().NotBeNull();
    }

    [Fact]
    public void Object_ShouldBeOfType()
    {
        object value = "hello";

        value.Should().BeOfType<string>();
    }

    [Fact]
    public void Object_ShouldBeAssignableTo()
    {
        object value = new List<int>();

        value.Should().BeAssignableTo<IEnumerable<int>>();
    }
}
