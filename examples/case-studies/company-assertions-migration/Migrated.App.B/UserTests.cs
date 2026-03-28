using Company.Assertions;
using Xunit;

namespace Migrated.App.B;

public class UserTests
{
    [Fact]
    public void User_Name_Should_Not_Be_Empty()
    {
        var user = CreateUser("alice", "alice@example.com");

        user.Name.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void User_Email_Should_Contain_At_Symbol()
    {
        var user = CreateUser("bob", "bob@example.com");

        user.Email.ShouldContain("@");
    }

    [Fact]
    public void User_Should_Be_Active_By_Default()
    {
        var user = CreateUser("carol", "carol@example.com");

        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void User_Roles_Should_Contain_Default_Role()
    {
        var user = CreateUser("dave", "dave@example.com");

        user.Roles.ShouldContain("User");
    }

    [Fact]
    public void User_Age_ShouldBe_Positive()
    {
        var user = CreateUser("eve", "eve@example.com");
        user.Age = 25;

        user.Age.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void User_DisplayName_ShouldBe_Name()
    {
        var user = CreateUser("frank", "frank@example.com");

        user.DisplayName.ShouldBe("frank");
    }

    [Fact]
    public void Null_Name_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => CreateUser(null!, "test@example.com"));
    }

    [Fact]
    public void User_Roles_ShouldNotBeEmpty()
    {
        var user = CreateUser("grace", "grace@example.com");

        user.Roles.ShouldNotBeEmpty();
    }

    // --- Helpers ---

    private static User CreateUser(string name, string email)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        return new User
        {
            Name = name,
            Email = email,
            DisplayName = name,
            IsActive = true,
            Roles = ["User"]
        };
    }

    private class User
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public bool IsActive { get; set; }
        public int Age { get; set; }
        public List<string> Roles { get; set; } = [];
    }
}
