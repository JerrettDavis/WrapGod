// VersionMatrixTests.cs
// Demonstrates mocking patterns that differ across Moq v4.10, v4.16, and v4.20.
// This file is illustrative -- it does not compile standalone (no .csproj).

using Moq;
using System;
using System.Threading.Tasks;

namespace WrapGod.Examples.Moq.VersionMatrix;

/// <summary>
/// Service interfaces used across all version examples.
/// </summary>
public interface IUserRepository
{
    User GetById(int id);
    Task<User> GetByIdAsync(int id);
    void Save(User user);
    void Delete(int id);
    event EventHandler<UserSavedEventArgs> UserSaved;
    event Func<UserSavedEventArgs, Task> UserSavedAsync;
}

public abstract class BaseRepository
{
    protected abstract string GetConnectionString();
    protected abstract int MaxRetries { get; }
    protected abstract void LogQuery(string sql, params object[] args);
}

public record User(int Id, string Name, string Email);
public record UserSavedEventArgs(User User) : EventArgs;

// =============================================================================
// PATTERN 1: Callback arity
// v4.10: Callback supports up to 4 generic parameters
// v4.16: Callback supports up to 16 generic parameters
// =============================================================================

public class CallbackArityTests
{
    // Works on ALL versions (v4.10+)
    public void Callback_WithFourParams_WorksOnAllVersions()
    {
        var mock = new Mock<IFourParamService>();
        var captured = new List<string>();

        mock.Setup(x => x.Process(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<bool>(),
                It.IsAny<DateTime>()))
            .Callback<string, int, bool, DateTime>((name, count, flag, date) =>
            {
                captured.Add($"{name}-{count}-{flag}-{date:yyyy}");
            });
    }

    // REQUIRES v4.16+ -- Callback with 8 parameters
    public void Callback_WithEightParams_RequiresV416()
    {
        var mock = new Mock<IEightParamService>();
        var captured = new List<string>();

        mock.Setup(x => x.Process(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Callback<string, string, int, int, bool, bool, DateTime, DateTime>(
                (a, b, c, d, e, f, g, h) =>
                {
                    captured.Add($"{a}-{b}-{c}-{d}");
                });
    }
}

public interface IFourParamService
{
    void Process(string name, int count, bool flag, DateTime date);
}

public interface IEightParamService
{
    void Process(string a, string b, int c, int d, bool e, bool f, DateTime g, DateTime h);
}

// =============================================================================
// PATTERN 2: Protected member mocking
// v4.10: String-based only -- Protected().Setup("MethodName", args)
// v4.15+: Type-safe via Protected().As<TAnalog>().Setup(x => x.Method())
// v4.18+: Analog supports SetupGet/SetupSet for protected properties
// =============================================================================

public class ProtectedMemberTests
{
    // v4.10 pattern: string-based, no compile-time safety
    public void ProtectedSetup_StringBased_V410()
    {
        var mock = new Mock<BaseRepository>();

        mock.Protected()
            .Setup<string>("GetConnectionString")
            .Returns("Server=test;Database=test");

        mock.Protected()
            .Setup("LogQuery", ItExpr.IsAny<string>(), ItExpr.IsAny<object[]>());
    }

    // v4.15+ pattern: type-safe via analog interface
    public interface IBaseRepositoryAnalog
    {
        string GetConnectionString();
        int MaxRetries { get; }
        void LogQuery(string sql, params object[] args);
    }

    public void ProtectedSetup_TypeSafe_V415()
    {
        var mock = new Mock<BaseRepository>();

        mock.Protected().As<IBaseRepositoryAnalog>()
            .Setup(x => x.GetConnectionString())
            .Returns("Server=test;Database=test");

        mock.Protected().As<IBaseRepositoryAnalog>()
            .Setup(x => x.LogQuery(It.IsAny<string>(), It.IsAny<object[]>()));
    }

    // v4.18+ pattern: type-safe protected property mocking
    public void ProtectedPropertySetup_V418()
    {
        var mock = new Mock<BaseRepository>();

        mock.Protected().As<IBaseRepositoryAnalog>()
            .SetupGet(x => x.MaxRetries)
            .Returns(3);

        mock.Protected().As<IBaseRepositoryAnalog>()
            .VerifyGet(x => x.MaxRetries, Times.Once());
    }
}

// =============================================================================
// PATTERN 3: Sequential setup variations
// v4.10: SetupSequence for return values only
// v4.12+: SetupSequence for void methods (ISetupSequentialAction)
// v4.12+: ReturnsAsync / ThrowsAsync in sequential chains
// v4.18+: CallBase() in sequential chains
// =============================================================================

public class SequentialSetupTests
{
    // v4.10: return-value sequences
    public void SetupSequence_Returns_V410()
    {
        var mock = new Mock<IUserRepository>();

        mock.SetupSequence(x => x.GetById(It.IsAny<int>()))
            .Returns(new User(1, "First", "first@test.com"))
            .Returns(new User(2, "Second", "second@test.com"))
            .Throws(new InvalidOperationException("No more users"));
    }

    // v4.12+: void method sequences
    public void SetupSequence_VoidMethod_V412()
    {
        var mock = new Mock<IUserRepository>();

        mock.SetupSequence(x => x.Save(It.IsAny<User>()))
            .Pass()                    // First call succeeds
            .Pass()                    // Second call succeeds
            .Throws(new Exception());  // Third call fails
    }

    // v4.12+: async sequential returns
    public void SetupSequence_Async_V412()
    {
        var mock = new Mock<IUserRepository>();

        mock.SetupSequence(x => x.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new User(1, "First", "first@test.com"))
            .ReturnsAsync(new User(2, "Second", "second@test.com"))
            .ThrowsAsync(new InvalidOperationException());
    }

    // v4.18+: CallBase in sequence (for partial mocks)
    public void SetupSequence_CallBase_V418()
    {
        var mock = new Mock<BaseRepository>() { CallBase = true };

        // First call delegates to real implementation, second throws
        mock.Protected().As<ProtectedMemberTests.IBaseRepositoryAnalog>()
            .SetupSequence(x => x.GetConnectionString())
            .CallBase()
            .Throws(new InvalidOperationException("Connection lost"));
    }
}

// =============================================================================
// PATTERN 4: Event subscription mocking
// v4.10: Can raise events but cannot mock add/remove handlers
// v4.13+: SetupAdd / SetupRemove for event subscription verification
// v4.20+: RaiseAsync for async event delegates
// =============================================================================

public class EventMockingTests
{
    // v4.10: raising synchronous events
    public void RaiseEvent_Sync_V410()
    {
        var mock = new Mock<IUserRepository>();
        var eventRaised = false;

        mock.Object.UserSaved += (sender, args) => eventRaised = true;
        mock.Raise(x => x.UserSaved += null,
            new UserSavedEventArgs(new User(1, "Test", "test@test.com")));

        // Assert eventRaised == true
    }

    // v4.13+: verifying event subscription
    public void VerifyEventSubscription_V413()
    {
        var mock = new Mock<IUserRepository>();

        mock.Object.UserSaved += (sender, args) => { };

        mock.VerifyAdd(x => x.UserSaved += It.IsAny<EventHandler<UserSavedEventArgs>>(),
            Times.Once());
    }

    // v4.20+: raising async events
    public async Task RaiseEvent_Async_V420()
    {
        var mock = new Mock<IUserRepository>();
        var eventRaised = false;

        mock.Object.UserSavedAsync += async (args) =>
        {
            await Task.Delay(1);
            eventRaised = true;
        };

        await mock.RaiseAsync(x => x.UserSavedAsync += null,
            new UserSavedEventArgs(new User(1, "Test", "test@test.com")));

        // Assert eventRaised == true (properly awaited)
    }
}

// =============================================================================
// PATTERN 5: Verifiable with Times (v4.20 only)
// v4.10-4.19: Times specified at Verify() call site
// v4.20+: Times can be specified at Setup with Verifiable(Times)
// =============================================================================

public class VerifiableTimesTests
{
    // v4.10+ pattern: specify Times at verify
    public void Verify_TimesAtCallSite_V410()
    {
        var mock = new Mock<IUserRepository>();
        mock.Setup(x => x.GetById(It.IsAny<int>()))
            .Returns(new User(1, "Test", "test@test.com"))
            .Verifiable();  // No times here

        mock.Object.GetById(1);

        mock.Verify(x => x.GetById(It.IsAny<int>()), Times.Once());  // Times at verify
    }

    // v4.20+ pattern: specify Times at setup (declarative)
    public void Verify_TimesAtSetup_V420()
    {
        var mock = new Mock<IUserRepository>();
        mock.Setup(x => x.GetById(It.IsAny<int>()))
            .Returns(new User(1, "Test", "test@test.com"))
            .Verifiable(Times.Once());  // Times at setup -- v4.20+

        mock.Object.GetById(1);

        mock.Verify();  // No need to restate the expression or times
    }
}

// =============================================================================
// PATTERN 6: Mock introspection (v4.17+)
// Examine recorded invocations and registered setups at runtime.
// =============================================================================

public class IntrospectionTests
{
    // v4.17+: access invocation history
    public void Invocations_Introspection_V417()
    {
        var mock = new Mock<IUserRepository>();

        mock.Object.GetById(1);
        mock.Object.GetById(2);
        mock.Object.Save(new User(3, "Test", "test@test.com"));

        // Assert mock.Invocations.Count == 3
        // Can iterate: foreach (var inv in mock.Invocations) { ... }

        mock.Invocations.Clear();  // Reset for next test phase
        // Assert mock.Invocations.Count == 0
    }
}
