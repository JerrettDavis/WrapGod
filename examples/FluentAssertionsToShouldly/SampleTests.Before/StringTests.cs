using FluentAssertions;
using Xunit;

namespace SampleTests.Before;

public class StringTests
{
    [Fact]
    public void String_ShouldContainSubstring()
    {
        var greeting = "Hello, World!";

        greeting.Should().Contain("World");
    }

    [Fact]
    public void String_ShouldStartWith()
    {
        var greeting = "Hello, World!";

        greeting.Should().StartWith("Hello");
    }

    [Fact]
    public void String_ShouldEndWith()
    {
        var greeting = "Hello, World!";

        greeting.Should().EndWith("World!");
    }

    [Fact]
    public void String_ShouldBeEmpty()
    {
        var empty = string.Empty;

        empty.Should().BeEmpty();
    }

    [Fact]
    public void String_ShouldNotBeNullOrEmpty()
    {
        var value = "something";

        value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void String_ShouldMatchPattern()
    {
        var email = "user@example.com";

        email.Should().MatchRegex(@"^[\w.+-]+@[\w-]+\.[\w.]+$");
    }
}
