using Shouldly;
using Xunit;

namespace SampleTests.After;

public class BooleanAndNullTests
{
    [Fact]
    public void Boolean_ShouldBeTrue()
    {
        var flag = true;

        flag.ShouldBeTrue();
    }

    [Fact]
    public void Boolean_ShouldBeFalse()
    {
        var flag = false;

        flag.ShouldBeFalse();
    }

    [Fact]
    public void Object_ShouldBeNull()
    {
        string? value = null;

        value.ShouldBeNull();
    }

    [Fact]
    public void Object_ShouldNotBeNull()
    {
        var value = "something";

        value.ShouldNotBeNull();
    }

    [Fact]
    public void Object_ShouldBeOfType()
    {
        object value = "hello";

        value.ShouldBeOfType<string>();
    }

    [Fact]
    public void Object_ShouldBeAssignableTo()
    {
        object value = new List<int>();

        value.ShouldBeAssignableTo<IEnumerable<int>>();
    }
}
