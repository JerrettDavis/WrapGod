# xUnit to NUnit Migration Example

This example demonstrates how WrapGod can orchestrate migration from
[xUnit](https://xunit.net/) to [NUnit](https://nunit.org/) in a .NET test project.

## Structure

```
migration.wrapgod.json          xUnit API surface manifest
migration.wrapgod.config.json   xUnit -> NUnit mapping rules
mapping-matrix.md               Full bidirectional mapping matrix with safety ratings
SampleTests.Before/             xUnit test project (source)
SampleTests.After/              NUnit test project (target)
```

## How it works

1. **Read** `migration.wrapgod.json` to understand the xUnit API surface
2. **Apply** rules from `migration.wrapgod.config.json` to transform source code
3. **Compare** `SampleTests.Before/` and `SampleTests.After/` to verify the migration

## Scenarios covered (30+)

| Category | Scenarios |
|----------|-----------|
| **Attributes** | `[Fact]` -> `[Test]`, `[Theory]` -> `[TestCase]`, `[InlineData]` -> `[TestCase]`, `[MemberData]` -> `[TestCaseSource]`, `[ClassData]` -> `[TestCaseSource]`, `[Trait]` -> `[Category]`/`[Property]`, `[Fact(Skip)]` -> `[Ignore]` |
| **Lifecycle** | Constructor -> `[SetUp]`, `IDisposable` -> `[TearDown]`, `IClassFixture<T>` -> `[OneTimeSetUp]`/`[OneTimeTearDown]`, `ICollectionFixture<T>` -> `[SetUpFixture]`, `ITestOutputHelper` -> `TestContext.Out` |
| **Assertions** | `Assert.Equal` -> `Assert.That(Is.EqualTo)`, `Assert.True/False`, `Assert.Null/NotNull`, `Assert.Throws<T>`, `Assert.ThrowsAsync<T>`, `Assert.Contains`, `Assert.Empty`, `Assert.IsType<T>`, `Assert.InRange`, `Assert.Collection`, `Assert.All`, `Assert.Single`, `Assert.StartsWith/EndsWith`, `Assert.Matches` |

## Key differences

| Concept | xUnit | NUnit |
|---------|-------|-------|
| Test marker | `[Fact]` | `[Test]` |
| Parameterized | `[Theory]` + `[InlineData]` | `[TestCase]` (combined) |
| Assertions | Static methods with expected-first | Constraint model with `Assert.That` |
| Per-test setup | Constructor (new instance per test) | `[SetUp]` method (instance reused) |
| Per-test teardown | `IDisposable.Dispose()` | `[TearDown]` method |
| One-time setup | `IClassFixture<T>` (DI) | `[OneTimeSetUp]` (attribute) |
| Cross-class sharing | `ICollectionFixture<T>` | `[SetUpFixture]` (namespace) |
| Output | `ITestOutputHelper` (injected) | `TestContext.Out` (static) |
| Skip | `[Fact(Skip = "...")]` | `[Ignore("...")]` |
| Parallelism | On by default (per collection) | Off by default (`[Parallelizable]`) |

## Risky / unsupported patterns

| xUnit pattern | NUnit | Risk |
|---------------|-------|------|
| `Assert.Collection(coll, ...)` | No direct equivalent | Rewrite to indexed assertions |
| `Assert.All(coll, action)` | `Has.All.Matches<T>(pred)` | Action -> predicate rewrite |
| `ICollectionFixture<T>` | `[SetUpFixture]` | Architectural change |
| Parallel by collection | `[Parallelizable]` | Opposite defaults; review strategy |

## Validate

Compare `SampleTests.Before/` and `SampleTests.After/` side by side. Both
projects compile and run the same test logic with equivalent semantics.

```bash
dotnet test examples/XUnitToNUnit/SampleTests.Before/
dotnet test examples/XUnitToNUnit/SampleTests.After/
```
