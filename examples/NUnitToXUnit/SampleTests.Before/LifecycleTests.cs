using NUnit.Framework;

namespace SampleTests;

/// <summary>
/// Lifecycle patterns: [SetUp]/[TearDown], [OneTimeSetUp]/[OneTimeTearDown], TestContext.
/// </summary>

// Scenario 10 & 11: [SetUp] / [TearDown] per-test lifecycle
[TestFixture]
public class LifecycleTests
{
    private List<string> _log = null!;

    [SetUp]
    public void SetUp()
    {
        _log = [];
    }

    [Test]
    public void Test_One()
    {
        _log.Add("one");
        Assert.That(_log, Has.Count.EqualTo(1));
    }

    [Test]
    public void Test_Two()
    {
        _log.Add("two");
        Assert.That(_log, Has.Count.EqualTo(1));
    }

    [TearDown]
    public void TearDown()
    {
        _log.Clear();
    }
}

// Scenario 12: [OneTimeSetUp] / [OneTimeTearDown] per-class lifecycle
[TestFixture]
public class ClassFixtureTests
{
    private int _initCount;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _initCount = 1;
    }

    [Test]
    public void Fixture_IsSharedAcrossTests()
    {
        Assert.That(_initCount, Is.EqualTo(1));
    }

    [Test]
    public void Fixture_SameInstance()
    {
        Assert.That(_initCount, Is.GreaterThan(0));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // one-time cleanup
    }
}

// Scenario 13: TestContext.Out for output capture
[TestFixture]
public class OutputTests
{
    [Test]
    public void Test_WithOutput()
    {
        TestContext.Out.WriteLine("This message appears in test output");
        Assert.That(true, Is.True);
    }
}

// Scenario 14: [SetUpFixture] for cross-class shared state
[SetUpFixture]
public class DatabaseSetupFixture
{
    public static string ConnectionId { get; private set; } = string.Empty;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        ConnectionId = Guid.NewGuid().ToString();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        // cleanup shared resource
    }
}

[TestFixture]
public class CollectionFixtureTestsA
{
    [Test]
    public void HasConnection()
    {
        Assert.That(string.IsNullOrEmpty(DatabaseSetupFixture.ConnectionId), Is.False);
    }
}

[TestFixture]
public class CollectionFixtureTestsB
{
    [Test]
    public void SharesSameFixture()
    {
        Assert.That(string.IsNullOrEmpty(DatabaseSetupFixture.ConnectionId), Is.False);
    }
}
