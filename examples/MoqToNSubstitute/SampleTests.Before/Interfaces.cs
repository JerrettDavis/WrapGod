namespace SampleTests.Before;

public interface IEmailService
{
    bool SendEmail(string to, string subject, string body);
    Task<bool> SendEmailAsync(string to, string subject, string body);
}

public interface IUserRepository
{
    User? GetById(int id);
    IReadOnlyList<User> GetAll();
    void Save(User user);
}

public interface ILogger
{
    void Log(string message);
    void LogError(string message, Exception exception);
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class UserService(IUserRepository repository, IEmailService emailService, ILogger logger)
{
    public User? GetUser(int id)
    {
        logger.Log($"Getting user {id}");
        return repository.GetById(id);
    }

    public bool NotifyUser(int userId, string message)
    {
        var user = repository.GetById(userId);
        if (user is null)
        {
            logger.LogError("User not found", new InvalidOperationException($"User {userId} not found"));
            return false;
        }

        return emailService.SendEmail(user.Email, "Notification", message);
    }

    public void DeactivateUser(int userId)
    {
        var user = repository.GetById(userId);
        if (user is null) return;

        user.IsActive = false;
        repository.Save(user);
        logger.Log($"Deactivated user {userId}");
    }
}
