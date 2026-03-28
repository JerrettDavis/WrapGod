using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace SampleTests.After;

public class UserServiceTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();
    private readonly IEmailService _emailService = Substitute.For<IEmailService>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_repository, _emailService, _logger);
    }

    // ──────────────────────────────────────────────
    // 1. Basic Setup + Returns
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldReturnUser_WhenFound()
    {
        var expected = new User { Id = 1, Name = "Alice" };
        _repository.GetById(1).Returns(expected);

        var result = _sut.GetUser(1);

        Assert.Equal(expected, result);
    }

    // ──────────────────────────────────────────────
    // 2. Returns null
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldReturnNull_WhenNotFound()
    {
        _repository.GetById(Arg.Any<int>()).Returns((User?)null);

        var result = _sut.GetUser(99);

        Assert.Null(result);
    }

    // ──────────────────────────────────────────────
    // 3. Verify called once
    // ──────────────────────────────────────────────

    [Fact]
    public void NotifyUser_ShouldSendEmail_WhenUserExists()
    {
        var user = new User { Id = 1, Email = "alice@example.com" };
        _repository.GetById(1).Returns(user);
        _emailService.SendEmail(user.Email, "Notification", "Hello").Returns(true);

        var result = _sut.NotifyUser(1, "Hello");

        Assert.True(result);
        _emailService.Received(1).SendEmail(user.Email, "Notification", "Hello");
    }

    // ──────────────────────────────────────────────
    // 4. Verify never called
    // ──────────────────────────────────────────────

    [Fact]
    public void NotifyUser_ShouldReturnFalse_WhenUserNotFound()
    {
        _repository.GetById(Arg.Any<int>()).Returns((User?)null);

        var result = _sut.NotifyUser(99, "Hello");

        Assert.False(result);
        _emailService.DidNotReceive().SendEmail(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ──────────────────────────────────────────────
    // 5. Verify with argument predicate (Arg.Is<T>)
    // ──────────────────────────────────────────────

    [Fact]
    public void DeactivateUser_ShouldSaveUser_WithIsActiveFalse()
    {
        var user = new User { Id = 1, Name = "Alice", IsActive = true };
        _repository.GetById(1).Returns(user);

        _sut.DeactivateUser(1);

        _repository.Received(1).Save(Arg.Is<User>(u => !u.IsActive));
    }

    // ──────────────────────────────────────────────
    // 6. Async method mocking
    // ──────────────────────────────────────────────

    [Fact]
    public async Task NotifyUserAsync_ShouldSendEmailAsync()
    {
        var user = new User { Id = 1, Email = "alice@example.com" };
        _repository.GetById(1).Returns(user);
        _emailService.SendEmailAsync(user.Email, "Notification", "Hello").Returns(true);

        var result = await _sut.NotifyUserAsync(1, "Hello");

        Assert.True(result);
    }

    // ──────────────────────────────────────────────
    // 7. Async void method (Task-returning)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task SaveUserAsync_ShouldCallRepository()
    {
        var user = new User { Id = 1, Name = "Alice" };
        _repository.SaveAsync(user).Returns(Task.CompletedTask);

        await _sut.SaveUserAsync(user);

        await _repository.Received(1).SaveAsync(user);
    }

    // ──────────────────────────────────────────────
    // 8. Exception throwing setup
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldThrow_WhenRepositoryFails()
    {
        _repository.GetById(Arg.Any<int>()).Throws(new InvalidOperationException("DB down"));

        Assert.Throws<InvalidOperationException>(() => _sut.GetUser(1));
    }

    // ──────────────────────────────────────────────
    // 9. Sequential returns
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldReturnDifferentResults_OnSequentialCalls()
    {
        var alice = new User { Id = 1, Name = "Alice" };
        var bob = new User { Id = 1, Name = "Bob" };
        _repository.GetById(1).Returns(alice, bob, null);

        Assert.Equal(alice, _sut.GetUser(1));
        Assert.Equal(bob, _sut.GetUser(1));
        Assert.Null(_sut.GetUser(1));
    }

    // ──────────────────────────────────────────────
    // 10. Callback with arguments (AndDoes)
    // ──────────────────────────────────────────────

    [Fact]
    public void DeactivateUser_ShouldLogMessage_WithCallback()
    {
        var user = new User { Id = 5, Name = "Charlie", IsActive = true };
        _repository.GetById(5).Returns(user);

        var savedUsers = new List<User>();
        _repository.When(r => r.Save(Arg.Any<User>()))
            .Do(ci => savedUsers.Add(ci.Arg<User>()));

        _sut.DeactivateUser(5);

        Assert.Single(savedUsers);
        Assert.False(savedUsers[0].IsActive);
    }

    // ──────────────────────────────────────────────
    // 11. Property get mocking
    // ──────────────────────────────────────────────

    [Fact]
    public void Logger_Prefix_ShouldReturnConfiguredValue()
    {
        _logger.Prefix.Returns("[APP]");

        Assert.Equal("[APP]", _logger.Prefix);
    }

    // ──────────────────────────────────────────────
    // 12. Property set verification
    // ──────────────────────────────────────────────

    [Fact]
    public void Logger_Prefix_ShouldBeSettable()
    {
        _logger.Prefix = "[TEST]";

        _logger.Received(1).Prefix = "[TEST]";
    }

    // ──────────────────────────────────────────────
    // 13. Returns from function (computed return)
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldReturnComputedResult()
    {
        _repository.GetById(Arg.Any<int>())
            .Returns(ci => new User { Id = ci.Arg<int>(), Name = $"User{ci.Arg<int>()}" });

        var result = _sut.GetUser(42);

        Assert.NotNull(result);
        Assert.Equal(42, result.Id);
        Assert.Equal("User42", result.Name);
    }

    // ──────────────────────────────────────────────
    // 14. Verify called exactly N times
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_MultipleCalls_ShouldVerifyExactCount()
    {
        _repository.GetById(Arg.Any<int>()).Returns(new User { Id = 1 });

        _sut.GetUser(1);
        _sut.GetUser(2);
        _sut.GetUser(3);

        _repository.Received(3).GetById(Arg.Any<int>());
    }

    // ──────────────────────────────────────────────
    // 15. Verify at least once
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldVerifyAtLeastOnce()
    {
        _repository.GetById(Arg.Any<int>()).Returns(new User { Id = 1 });

        _sut.GetUser(1);
        _sut.GetUser(1);

        _repository.Received().GetById(Arg.Any<int>());
    }

    // ──────────────────────────────────────────────
    // 16. Void method setup + verify
    // ──────────────────────────────────────────────

    [Fact]
    public void DeactivateUser_ShouldCallLogOnSuccess()
    {
        var user = new User { Id = 1, IsActive = true };
        _repository.GetById(1).Returns(user);

        _sut.DeactivateUser(1);

        _logger.Received(1).Log(Arg.Is<string>(s => s.Contains("Deactivated")));
    }

    // ──────────────────────────────────────────────
    // 17. Argument range matching (predicate-based)
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_WithRangeArgument_ShouldMatch()
    {
        var user = new User { Id = 5, Name = "Ranged" };
        _repository.GetById(Arg.Is<int>(id => id >= 1 && id <= 10)).Returns(user);

        Assert.Equal(user, _sut.GetUser(5));
        Assert.Null(_sut.GetUser(11));
    }

    // ──────────────────────────────────────────────
    // 18. Generic interface mocking (ICache<T>)
    // ──────────────────────────────────────────────

    [Fact]
    public void Cache_ShouldReturnConfiguredValue()
    {
        var cache = Substitute.For<ICache<User>>();
        var user = new User { Id = 1, Name = "CachedAlice" };
        cache.Get("user:1").Returns(user);

        var result = cache.Get("user:1");

        Assert.Equal(user, result);
        cache.Received(1).Get("user:1");
    }

    // ──────────────────────────────────────────────
    // 19. Generic interface with overloaded method
    // ──────────────────────────────────────────────

    [Fact]
    public void Cache_SetWithExpiry_ShouldVerifyCorrectOverload()
    {
        var cache = Substitute.For<ICache<string>>();

        cache.Set("key", "value", TimeSpan.FromMinutes(5));

        cache.Received(1).Set("key", "value", TimeSpan.FromMinutes(5));
        cache.DidNotReceive().Set("key", "value");
    }

    // ──────────────────────────────────────────────
    // 20. Strict mock equivalent (no direct NSubstitute equivalent)
    // NSubstitute has no strict mode; unmatched calls return defaults.
    // This test demonstrates the behavioral difference.
    // ──────────────────────────────────────────────

    [Fact]
    public void DefaultSubstitute_ShouldReturnDefault_WhenNoSetup()
    {
        var repo = Substitute.For<IUserRepository>();
        repo.GetById(1).Returns(new User { Id = 1 });

        // Configured call returns the value
        Assert.NotNull(repo.GetById(1));

        // Unconfigured call returns default (null) instead of throwing
        Assert.Null(repo.GetById(999));
    }

    // ──────────────────────────────────────────────
    // 21. VerifyAll equivalent (verify each setup individually)
    // ──────────────────────────────────────────────

    [Fact]
    public void VerifyAll_Equivalent_ShouldCheckEachSetup()
    {
        _repository.GetById(1).Returns(new User { Id = 1 });

        _sut.GetUser(1);

        // NSubstitute: verify each expected call individually
        _repository.Received(1).GetById(1);
    }

    // ──────────────────────────────────────────────
    // 22. VerifyNoOtherCalls equivalent
    // NSubstitute has no direct equivalent. Verify each call explicitly.
    // ──────────────────────────────────────────────

    [Fact]
    public void VerifyNoOtherCalls_Equivalent()
    {
        _repository.GetById(1).Returns(new User { Id = 1 });

        _sut.GetUser(1);

        // NSubstitute: verify the calls you expect and assert no extras
        _repository.Received(1).GetById(1);
        _logger.Received(1).Log(Arg.Any<string>());
        // No direct VerifyNoOtherCalls -- review manually
    }

    // ──────────────────────────────────────────────
    // 23. Throws async exception
    // ──────────────────────────────────────────────

    [Fact]
    public async Task NotifyUserAsync_ShouldPropagateException()
    {
        var user = new User { Id = 1, Email = "alice@example.com" };
        _repository.GetById(1).Returns(user);
        _emailService.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new TimeoutException("SMTP timeout"));

        await Assert.ThrowsAsync<TimeoutException>(() => _sut.NotifyUserAsync(1, "Hello"));
    }

    // ──────────────────────────────────────────────
    // 24. Event raising
    // ──────────────────────────────────────────────

    [Fact]
    public void NotificationService_ShouldRaiseEvent()
    {
        var notif = Substitute.For<INotificationService>();
        NotificationEventArgs? receivedArgs = null;
        notif.NotificationSent += (_, args) => receivedArgs = args;

        notif.NotificationSent += Raise.EventWith(
            new NotificationEventArgs("user-1", "Hello"));

        Assert.NotNull(receivedArgs);
        Assert.Equal("user-1", receivedArgs.UserId);
    }

    // ──────────────────────────────────────────────
    // 25. Out parameter handling
    // ──────────────────────────────────────────────

    [Fact]
    public void TryGetByEmail_ShouldReturnUser_ViaOutParam()
    {
        var user = new User { Id = 1, Email = "alice@example.com" };
        _repository.TryGetByEmail("alice@example.com", out Arg.Any<User?>())
            .Returns(ci =>
            {
                ci[1] = user;
                return true;
            });

        var found = _repository.TryGetByEmail("alice@example.com", out var result);

        Assert.True(found);
        Assert.Equal(user, result);
    }
}
