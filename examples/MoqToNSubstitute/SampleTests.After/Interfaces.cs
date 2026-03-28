namespace SampleTests.After;

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
    Task SaveAsync(User user);
    bool TryGetByEmail(string email, out User? user);
}

public interface ILogger
{
    void Log(string message);
    void LogError(string message, Exception exception);
    string Prefix { get; set; }
}

public interface ICache<TValue>
{
    TValue? Get(string key);
    void Set(string key, TValue value);
    void Set(string key, TValue value, TimeSpan expiry);
}

public interface INotificationService
{
    event EventHandler<NotificationEventArgs> NotificationSent;
    void Send(string userId, string message);
}

public class NotificationEventArgs(string userId, string message) : EventArgs
{
    public string UserId { get; } = userId;
    public string Message { get; } = message;
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

    public async Task<bool> NotifyUserAsync(int userId, string message)
    {
        var user = repository.GetById(userId);
        if (user is null) return false;

        return await emailService.SendEmailAsync(user.Email, "Notification", message);
    }

    public async Task SaveUserAsync(User user)
    {
        await repository.SaveAsync(user);
        logger.Log($"Saved user {user.Id}");
    }
}
