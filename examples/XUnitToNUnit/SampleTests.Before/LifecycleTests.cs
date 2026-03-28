using Xunit;
using Xunit.Abstractions;

namespace SampleTests;

/// <summary>
/// Lifecycle patterns: constructor/IDisposable, IClassFixture, ITestOutputHelper.
/// </summary>

// Scenario 10 & 11: Constructor = per-test setup, IDisposable = per-test teardown
public class LifecycleTests : IDisposable
{
    private readonly List<string> _log;

    public LifecycleTests()
    {
        // Runs before every test (like [SetUp])
        _log = [];
    }

    [Fact]
    public void Test_One()
    {
        _log.Add("one");
        Assert.Single(_log);
    }

    [Fact]
    public void Test_Two()
    {
        _log.Add("two");
        Assert.Single(_log);
    }

    public void Dispose()
    {
        // Runs after every test (like [TearDown])
        _log.Clear();
    }
}

// Scenario 12: IClassFixture<T> = one-time setup/teardown per class
public class SharedResourceFixture : IDisposable
{
    public int InitCount { get; }
    public SharedResourceFixture() => InitCount = 1;
    public void Dispose() { /* one-time cleanup */ }
}

public class ClassFixtureTests : IClassFixture<SharedResourceFixture>
{
    private readonly SharedResourceFixture _fixture;

    public ClassFixtureTests(SharedResourceFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Fixture_IsSharedAcrossTests()
    {
        Assert.Equal(1, _fixture.InitCount);
    }

    [Fact]
    public void Fixture_SameInstance()
    {
        Assert.NotNull(_fixture);
    }
}

// Scenario 13: ITestOutputHelper for capturing test output
public class OutputTests
{
    private readonly ITestOutputHelper _output;

    public OutputTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Test_WithOutput()
    {
        _output.WriteLine("This message appears in test output");
        Assert.True(true);
    }
}

// Scenario 14: Collection fixture for cross-class shared state
[CollectionDefinition("DatabaseCollection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>;

public class DatabaseFixture : IDisposable
{
    public string ConnectionId { get; } = Guid.NewGuid().ToString();
    public void Dispose() { /* cleanup shared resource */ }
}

[Collection("DatabaseCollection")]
public class CollectionFixtureTestsA
{
    private readonly DatabaseFixture _db;

    public CollectionFixtureTestsA(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public void HasConnection()
    {
        Assert.False(string.IsNullOrEmpty(_db.ConnectionId));
    }
}

[Collection("DatabaseCollection")]
public class CollectionFixtureTestsB
{
    private readonly DatabaseFixture _db;

    public CollectionFixtureTestsB(DatabaseFixture db)
    {
        _db = db;
    }

    [Fact]
    public void SharesSameFixture()
    {
        Assert.False(string.IsNullOrEmpty(_db.ConnectionId));
    }
}
