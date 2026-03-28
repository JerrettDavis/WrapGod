# xUnit / NUnit Bidirectional Mapping Matrix

Comprehensive mapping of xUnit and NUnit APIs with safety classifications.

## Safety Legend

| Classification | Meaning |
|----------------|---------|
| **safe** | Direct 1:1 mapping, automated migration possible |
| **guided** | Structural rewrite needed, tool can suggest but human review required |
| **manual** | No direct equivalent, requires architectural changes |

## Attributes

| # | xUnit | NUnit | Direction | Safety | Notes |
|---|-------|-------|-----------|--------|-------|
| 1 | `[Fact]` | `[Test]` | Both | safe | |
| 2 | `[Fact(Skip = "reason")]` | `[Test] [Ignore("reason")]` | Both | safe | xUnit embeds in attribute; NUnit uses separate attribute |
| 3 | `[Theory]` | _(implicit from data attributes)_ | x->N | safe | NUnit does not need a separate marker |
| 4 | `[InlineData(args)]` | `[TestCase(args)]` | Both | safe | |
| 5 | `[MemberData(nameof(M))]` | `[TestCaseSource(nameof(M))]` | Both | safe | |
| 6 | `[ClassData(typeof(C))]` | `[TestCaseSource(typeof(C))]` | Both | safe | |
| 7 | `[Trait("Category", "v")]` | `[Category("v")]` | Both | safe | |
| 8 | `[Trait("k", "v")]` | `[Property("k", "v")]` | Both | safe | Non-category metadata |
| 9 | _(no class attribute)_ | `[TestFixture]` | Both | safe | NUnit requires it; xUnit discovers implicitly |

## Lifecycle

| # | xUnit | NUnit | Direction | Safety | Notes |
|---|-------|-------|-----------|--------|-------|
| 10 | Constructor | `[SetUp]` method | Both | guided | xUnit creates new instance per test; NUnit reuses instance |
| 11 | `IDisposable.Dispose()` | `[TearDown]` method | Both | guided | Structural change: interface vs attribute |
| 12 | `IClassFixture<T>` | `[OneTimeSetUp]` / `[OneTimeTearDown]` | Both | guided | DI-based vs attribute-based lifecycle |
| 13 | `ITestOutputHelper` | `TestContext.Out` | Both | safe | DI-injected vs static access |
| 14 | `ICollectionFixture<T>` + `[Collection]` | `[SetUpFixture]` at namespace | Both | manual | Fundamentally different sharing models |

## Assertions

| # | xUnit | NUnit | Direction | Safety | Notes |
|---|-------|-------|-----------|--------|-------|
| 15 | `Assert.Equal(exp, act)` | `Assert.That(act, Is.EqualTo(exp))` | Both | safe | Parameter order differs |
| 16 | `Assert.NotEqual(exp, act)` | `Assert.That(act, Is.Not.EqualTo(exp))` | Both | safe | |
| 17 | `Assert.True(cond)` | `Assert.That(cond, Is.True)` | Both | safe | |
| 18 | `Assert.False(cond)` | `Assert.That(cond, Is.False)` | Both | safe | |
| 19 | `Assert.Null(val)` | `Assert.That(val, Is.Null)` | Both | safe | |
| 20 | `Assert.NotNull(val)` | `Assert.That(val, Is.Not.Null)` | Both | safe | |
| 21 | `Assert.Throws<T>(action)` | `Assert.Throws<T>(action)` | Both | safe | Same API, different namespace |
| 22 | `await Assert.ThrowsAsync<T>(f)` | `Assert.ThrowsAsync<T>(f)` | Both | safe | xUnit awaits; NUnit does not |
| 23 | `Assert.Contains(sub, str)` | `Assert.That(str, Does.Contain(sub))` | Both | safe | |
| 24 | `Assert.DoesNotContain(sub, str)` | `Assert.That(str, Does.Not.Contain(sub))` | Both | safe | |
| 25 | `Assert.Contains(item, coll)` | `Assert.That(coll, Does.Contain(item))` | Both | safe | |
| 26 | `Assert.DoesNotContain(item, coll)` | `Assert.That(coll, Does.Not.Contain(item))` | Both | safe | |
| 27 | `Assert.Empty(coll)` | `Assert.That(coll, Is.Empty)` | Both | safe | |
| 28 | `Assert.NotEmpty(coll)` | `Assert.That(coll, Is.Not.Empty)` | Both | safe | |
| 29 | `Assert.IsType<T>(val)` | `Assert.That(val, Is.TypeOf<T>())` | Both | safe | |
| 30 | `Assert.IsAssignableFrom<T>(v)` | `Assert.That(v, Is.AssignableFrom<T>())` | Both | safe | |
| 31 | `Assert.InRange(act, lo, hi)` | `Assert.That(act, Is.InRange(lo, hi))` | Both | safe | |
| 32 | `Assert.Collection(coll, ...)` | Index-based `Assert.That` calls | x->N | guided | No direct NUnit equivalent |
| 33 | `Assert.All(coll, action)` | `Assert.That(coll, Has.All.Matches<T>(p))` | Both | guided | Action vs predicate rewrite |
| 34 | `Assert.Single(coll)` | `Assert.That(coll, Has.Count.EqualTo(1))` | Both | safe | xUnit returns element; NUnit only asserts |
| 35 | `Assert.StartsWith(exp, act)` | `Assert.That(act, Does.StartWith(exp))` | Both | safe | |
| 36 | `Assert.EndsWith(exp, act)` | `Assert.That(act, Does.EndWith(exp))` | Both | safe | |
| 37 | `Assert.Matches(pattern, act)` | `Assert.That(act, Does.Match(pattern))` | Both | safe | |
| 38 | `Assert.Equal(exp, act)` (collection) | `Assert.That(act, Is.EqualTo(exp))` | Both | safe | Sequence equality |

## Runner / Configuration

| # | Topic | xUnit | NUnit | Safety | Notes |
|---|-------|-------|-------|--------|-------|
| 39 | Test parallelism | Parallel by collection (default on) | `[Parallelizable]` attribute (default off) | manual | Opposite defaults; review parallelism strategy |
| 40 | Test ordering | `[TestCaseOrderer]` custom | `[Order(n)]` attribute | guided | Different mechanisms |
| 41 | Output capture | `ITestOutputHelper` (DI) | `TestContext.Out` (static) | safe | |

## Summary

| Safety | Count | Percentage |
|--------|-------|------------|
| safe | 32 | 78% |
| guided | 7 | 17% |
| manual | 2 | 5% |
| **Total** | **41** | |
