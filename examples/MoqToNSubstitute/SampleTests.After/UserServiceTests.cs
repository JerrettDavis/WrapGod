using NSubstitute;
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

    [Fact]
    public void GetUser_ShouldReturnUser_WhenFound()
    {
        var expected = new User { Id = 1, Name = "Alice" };
        _repository.GetById(1).Returns(expected);

        var result = _sut.GetUser(1);

        Assert.Equal(expected, result);
        _logger.Received(1).Log(Arg.Any<string>());
    }

    [Fact]
    public void GetUser_ShouldReturnNull_WhenNotFound()
    {
        _repository.GetById(Arg.Any<int>()).Returns((User?)null);

        var result = _sut.GetUser(99);

        Assert.Null(result);
    }

    [Fact]
    public void NotifyUser_ShouldSendEmail_WhenUserExists()
    {
        var user = new User { Id = 1, Email = "alice@example.com" };
        _repository.GetById(1).Returns(user);
        _emailService.SendEmail(user.Email, "Notification", "Hello").Returns(true);

        var result = _sut.NotifyUser(1, "Hello");

        Assert.True(result);
        _emailService.Received(1).SendEmail("alice@example.com", "Notification", "Hello");
    }

    [Fact]
    public void NotifyUser_ShouldReturnFalse_WhenUserNotFound()
    {
        _repository.GetById(Arg.Any<int>()).Returns((User?)null);

        var result = _sut.NotifyUser(99, "Hello");

        Assert.False(result);
        _logger.Received(1).LogError(Arg.Any<string>(), Arg.Any<Exception>());
        _emailService.DidNotReceive().SendEmail(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public void DeactivateUser_ShouldSaveAndLog()
    {
        var user = new User { Id = 1, IsActive = true };
        _repository.GetById(1).Returns(user);

        _sut.DeactivateUser(1);

        Assert.False(user.IsActive);
        _repository.Received(1).Save(Arg.Is<User>(u => !u.IsActive));
        _logger.Received(1).Log(Arg.Is<string>(s => s.Contains("Deactivated")));
    }

    [Fact]
    public void DeactivateUser_ShouldDoNothing_WhenUserNotFound()
    {
        _repository.GetById(Arg.Any<int>()).Returns((User?)null);

        _sut.DeactivateUser(99);

        _repository.DidNotReceive().Save(Arg.Any<User>());
    }
}
