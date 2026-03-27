using Shouldly;
using Xunit;

namespace SampleTests.After;

public class StringTests
{
    [Fact]
    public void String_ShouldContainSubstring()
    {
        var greeting = "Hello, World!";

        greeting.ShouldContain("World");
    }

    [Fact]
    public void String_ShouldStartWith()
    {
        var greeting = "Hello, World!";

        greeting.ShouldStartWith("Hello");
    }

    [Fact]
    public void String_ShouldEndWith()
    {
        var greeting = "Hello, World!";

        greeting.ShouldEndWith("World!");
    }

    [Fact]
    public void String_ShouldBeEmpty()
    {
        var empty = string.Empty;

        empty.ShouldBeEmpty();
    }

    [Fact]
    public void String_ShouldNotBeNullOrEmpty()
    {
        var value = "something";

        value.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void String_ShouldMatchPattern()
    {
        var email = "user@example.com";

        email.ShouldMatch(@"^[\w.+-]+@[\w-]+\.[\w.]+$");
    }
}
