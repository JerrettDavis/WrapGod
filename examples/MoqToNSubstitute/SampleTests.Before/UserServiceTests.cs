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
        _sut = new UserService(
            _repositoryMock.Object,
            _emailServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public void GetUser_ShouldReturnUser_WhenFound()
    {
        var expected = new User { Id = 1, Name = "Alice" };
        _repositoryMock
            .Setup(r => r.GetById(1))
            .Returns(expected);

        var result = _sut.GetUser(1);

        Assert.Equal(expected, result);
        _loggerMock.Verify(l => l.Log(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void GetUser_ShouldReturnNull_WhenNotFound()
    {
        _repositoryMock
            .Setup(r => r.GetById(It.IsAny<int>()))
            .Returns((User?)null);

        var result = _sut.GetUser(99);

        Assert.Null(result);
    }

    [Fact]
    public void NotifyUser_ShouldSendEmail_WhenUserExists()
    {
        var user = new User { Id = 1, Email = "alice@example.com" };
        _repositoryMock.Setup(r => r.GetById(1)).Returns(user);
        _emailServiceMock
            .Setup(e => e.SendEmail(user.Email, "Notification", "Hello"))
            .Returns(true);

        var result = _sut.NotifyUser(1, "Hello");

        Assert.True(result);
        _emailServiceMock.Verify(
            e => e.SendEmail("alice@example.com", "Notification", "Hello"),
            Times.Once);
    }

    [Fact]
    public void NotifyUser_ShouldReturnFalse_WhenUserNotFound()
    {
        _repositoryMock
            .Setup(r => r.GetById(It.IsAny<int>()))
            .Returns((User?)null);

        var result = _sut.NotifyUser(99, "Hello");

        Assert.False(result);
        _loggerMock.Verify(
            l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()),
            Times.Once);
        _emailServiceMock.Verify(
            e => e.SendEmail(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void DeactivateUser_ShouldSaveAndLog()
    {
        var user = new User { Id = 1, IsActive = true };
        _repositoryMock.Setup(r => r.GetById(1)).Returns(user);

        _sut.DeactivateUser(1);

        Assert.False(user.IsActive);
        _repositoryMock.Verify(r => r.Save(It.Is<User>(u => !u.IsActive)), Times.Once);
        _loggerMock.Verify(l => l.Log(It.Is<string>(s => s.Contains("Deactivated"))), Times.Once);
    }

    [Fact]
    public void DeactivateUser_ShouldDoNothing_WhenUserNotFound()
    {
        _repositoryMock
            .Setup(r => r.GetById(It.IsAny<int>()))
            .Returns((User?)null);

        _sut.DeactivateUser(99);

        _repositoryMock.Verify(r => r.Save(It.IsAny<User>()), Times.Never);
    }
}
