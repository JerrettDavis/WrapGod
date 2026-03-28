using Moq;
using Xunit;

namespace SampleTests.Before;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _repositoryMock = new();
    private readonly Mock<IEmailService> _emailServiceMock = new();
    private readonly Mock<ILogger> _loggerMock = new();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _sut = new UserService(_repositoryMock.Object, _emailServiceMock.Object, _loggerMock.Object);
    }

    // ──────────────────────────────────────────────
    // 1. Basic Setup + Returns
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldReturnUser_WhenFound()
    {
        var expected = new User { Id = 1, Name = "Alice" };
        _repositoryMock.Setup(r => r.GetById(1)).Returns(expected);

        var result = _sut.GetUser(1);

        Assert.Equal(expected, result);
    }

    // ──────────────────────────────────────────────
    // 2. Returns null
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldReturnNull_WhenNotFound()
    {
        _repositoryMock.Setup(r => r.GetById(It.IsAny<int>())).Returns((User?)null);

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
        _repositoryMock.Setup(r => r.GetById(1)).Returns(user);
        _emailServiceMock.Setup(e => e.SendEmail(user.Email, "Notification", "Hello")).Returns(true);

        var result = _sut.NotifyUser(1, "Hello");

        Assert.True(result);
        _emailServiceMock.Verify(e => e.SendEmail(user.Email, "Notification", "Hello"), Times.Once);
    }

    // ──────────────────────────────────────────────
    // 4. Verify never called
    // ──────────────────────────────────────────────

    [Fact]
    public void NotifyUser_ShouldReturnFalse_WhenUserNotFound()
    {
        _repositoryMock.Setup(r => r.GetById(It.IsAny<int>())).Returns((User?)null);

        var result = _sut.NotifyUser(99, "Hello");

        Assert.False(result);
        _emailServiceMock.Verify(
            e => e.SendEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ──────────────────────────────────────────────
    // 5. Verify with argument predicate (It.Is<T>)
    // ──────────────────────────────────────────────

    [Fact]
    public void DeactivateUser_ShouldSaveUser_WithIsActiveFalse()
    {
        var user = new User { Id = 1, Name = "Alice", IsActive = true };
        _repositoryMock.Setup(r => r.GetById(1)).Returns(user);

        _sut.DeactivateUser(1);

        _repositoryMock.Verify(r => r.Save(It.Is<User>(u => !u.IsActive)), Times.Once);
    }

    // ──────────────────────────────────────────────
    // 6. Async method mocking
    // ──────────────────────────────────────────────

    [Fact]
    public async Task NotifyUserAsync_ShouldSendEmailAsync()
    {
        var user = new User { Id = 1, Email = "alice@example.com" };
        _repositoryMock.Setup(r => r.GetById(1)).Returns(user);
        _emailServiceMock
            .Setup(e => e.SendEmailAsync(user.Email, "Notification", "Hello"))
            .ReturnsAsync(true);

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
        _repositoryMock.Setup(r => r.SaveAsync(user)).Returns(Task.CompletedTask);

        await _sut.SaveUserAsync(user);

        _repositoryMock.Verify(r => r.SaveAsync(user), Times.Once);
    }

    // ──────────────────────────────────────────────
    // 8. Exception throwing setup
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldThrow_WhenRepositoryFails()
    {
        _repositoryMock.Setup(r => r.GetById(It.IsAny<int>()))
            .Throws(new InvalidOperationException("DB down"));

        Assert.Throws<InvalidOperationException>(() => _sut.GetUser(1));
    }

    // ──────────────────────────────────────────────
    // 9. Sequential returns (SetupSequence)
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldReturnDifferentResults_OnSequentialCalls()
    {
        var alice = new User { Id = 1, Name = "Alice" };
        var bob = new User { Id = 1, Name = "Bob" };
        _repositoryMock.SetupSequence(r => r.GetById(1))
            .Returns(alice)
            .Returns(bob)
            .Returns((User?)null);

        Assert.Equal(alice, _sut.GetUser(1));
        Assert.Equal(bob, _sut.GetUser(1));
        Assert.Null(_sut.GetUser(1));
    }

    // ──────────────────────────────────────────────
    // 10. Callback with arguments
    // ──────────────────────────────────────────────

    [Fact]
    public void DeactivateUser_ShouldLogMessage_WithCallback()
    {
        var user = new User { Id = 5, Name = "Charlie", IsActive = true };
        _repositoryMock.Setup(r => r.GetById(5)).Returns(user);

        var savedUsers = new List<User>();
        _repositoryMock.Setup(r => r.Save(It.IsAny<User>()))
            .Callback<User>(u => savedUsers.Add(u));

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
        _loggerMock.Setup(l => l.Prefix).Returns("[APP]");

        Assert.Equal("[APP]", _loggerMock.Object.Prefix);
    }

    // ──────────────────────────────────────────────
    // 12. Property set verification
    // ──────────────────────────────────────────────

    [Fact]
    public void Logger_Prefix_ShouldBeSettable()
    {
        _loggerMock.Object.Prefix = "[TEST]";

        _loggerMock.VerifySet(l => l.Prefix = "[TEST]", Times.Once);
    }

    // ──────────────────────────────────────────────
    // 13. Returns from function (computed return)
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldReturnComputedResult()
    {
        _repositoryMock.Setup(r => r.GetById(It.IsAny<int>()))
            .Returns((int id) => new User { Id = id, Name = $"User{id}" });

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
        _repositoryMock.Setup(r => r.GetById(It.IsAny<int>())).Returns(new User { Id = 1 });

        _sut.GetUser(1);
        _sut.GetUser(2);
        _sut.GetUser(3);

        _repositoryMock.Verify(r => r.GetById(It.IsAny<int>()), Times.Exactly(3));
    }

    // ──────────────────────────────────────────────
    // 15. Verify at least once
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_ShouldVerifyAtLeastOnce()
    {
        _repositoryMock.Setup(r => r.GetById(It.IsAny<int>())).Returns(new User { Id = 1 });

        _sut.GetUser(1);
        _sut.GetUser(1);

        _repositoryMock.Verify(r => r.GetById(It.IsAny<int>()), Times.AtLeastOnce);
    }

    // ──────────────────────────────────────────────
    // 16. Void method setup + verify
    // ──────────────────────────────────────────────

    [Fact]
    public void DeactivateUser_ShouldCallLogOnSuccess()
    {
        var user = new User { Id = 1, IsActive = true };
        _repositoryMock.Setup(r => r.GetById(1)).Returns(user);

        _sut.DeactivateUser(1);

        _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Deactivated"))), Times.Once);
    }

    // ──────────────────────────────────────────────
    // 17. It.IsInRange argument matching
    // ──────────────────────────────────────────────

    [Fact]
    public void GetUser_WithRangeArgument_ShouldMatch()
    {
        var user = new User { Id = 5, Name = "Ranged" };
        _repositoryMock
            .Setup(r => r.GetById(It.IsInRange(1, 10, Moq.Range.Inclusive)))
            .Returns(user);

        Assert.Equal(user, _sut.GetUser(5));
        Assert.Null(_sut.GetUser(11));
    }

    // ──────────────────────────────────────────────
    // 18. Generic interface mocking (ICache<T>)
    // ──────────────────────────────────────────────

    [Fact]
    public void Cache_ShouldReturnConfiguredValue()
    {
        var cacheMock = new Mock<ICache<User>>();
        var user = new User { Id = 1, Name = "CachedAlice" };
        cacheMock.Setup(c => c.Get("user:1")).Returns(user);

        var result = cacheMock.Object.Get("user:1");

        Assert.Equal(user, result);
        cacheMock.Verify(c => c.Get("user:1"), Times.Once);
    }

    // ──────────────────────────────────────────────
    // 19. Generic interface with overloaded method
    // ──────────────────────────────────────────────

    [Fact]
    public void Cache_SetWithExpiry_ShouldVerifyCorrectOverload()
    {
        var cacheMock = new Mock<ICache<string>>();

        cacheMock.Object.Set("key", "value", TimeSpan.FromMinutes(5));

        cacheMock.Verify(c => c.Set("key", "value", TimeSpan.FromMinutes(5)), Times.Once);
        cacheMock.Verify(c => c.Set("key", "value"), Times.Never);
    }

    // ──────────────────────────────────────────────
    // 20. MockBehavior.Strict
    // ──────────────────────────────────────────────

    [Fact]
    public void StrictMock_ShouldThrow_WhenUnexpectedCallMade()
    {
        var strictRepo = new Mock<IUserRepository>(MockBehavior.Strict);
        strictRepo.Setup(r => r.GetById(1)).Returns(new User { Id = 1 });

        // Calling the setup method works fine
        Assert.NotNull(strictRepo.Object.GetById(1));

        // Calling an unsetup method on strict mock throws
        Assert.Throws<MockException>(() => strictRepo.Object.GetById(999));
    }

    // ──────────────────────────────────────────────
    // 21. VerifyAll
    // ──────────────────────────────────────────────

    [Fact]
    public void VerifyAll_ShouldPass_WhenAllSetupsInvoked()
    {
        _repositoryMock.Setup(r => r.GetById(1)).Returns(new User { Id = 1 });

        _sut.GetUser(1);

        _repositoryMock.VerifyAll();
    }

    // ──────────────────────────────────────────────
    // 22. VerifyNoOtherCalls
    // ──────────────────────────────────────────────

    [Fact]
    public void VerifyNoOtherCalls_ShouldPass_WhenNoExtraCalls()
    {
        _repositoryMock.Setup(r => r.GetById(1)).Returns(new User { Id = 1 });

        _sut.GetUser(1);

        _repositoryMock.Verify(r => r.GetById(1), Times.Once);
        _loggerMock.Verify(l => l.Log(It.IsAny<string>()), Times.Once);
        _repositoryMock.VerifyNoOtherCalls();
    }

    // ──────────────────────────────────────────────
    // 23. Throws async exception
    // ──────────────────────────────────────────────

    [Fact]
    public async Task NotifyUserAsync_ShouldPropagateException()
    {
        var user = new User { Id = 1, Email = "alice@example.com" };
        _repositoryMock.Setup(r => r.GetById(1)).Returns(user);
        _emailServiceMock
            .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new TimeoutException("SMTP timeout"));

        await Assert.ThrowsAsync<TimeoutException>(() => _sut.NotifyUserAsync(1, "Hello"));
    }

    // ──────────────────────────────────────────────
    // 24. Event raising
    // ──────────────────────────────────────────────

    [Fact]
    public void NotificationService_ShouldRaiseEvent()
    {
        var notifMock = new Mock<INotificationService>();
        NotificationEventArgs? receivedArgs = null;
        notifMock.Object.NotificationSent += (_, args) => receivedArgs = args;

        notifMock.Raise(
            n => n.NotificationSent += null,
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
        _repositoryMock
            .Setup(r => r.TryGetByEmail("alice@example.com", out It.Ref<User?>.IsAny))
            .Returns(new TryGetByEmailDelegate((string email, out User? u) =>
            {
                u = user;
                return true;
            }));

        var found = _repositoryMock.Object.TryGetByEmail("alice@example.com", out var result);

        Assert.True(found);
        Assert.Equal(user, result);
    }

    private delegate bool TryGetByEmailDelegate(string email, out User? user);
}
