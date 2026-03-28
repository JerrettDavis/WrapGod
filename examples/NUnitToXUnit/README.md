# NUnit to xUnit Migration Example

This example demonstrates how WrapGod can orchestrate migration from
[NUnit](https://nunit.org/) to [xUnit](https://xunit.net/) in a .NET test project.

## Structure

```
migration.wrapgod.json          NUnit API surface manifest
migration.wrapgod.config.json   NUnit -> xUnit mapping rules
SampleTests.Before/             NUnit test project (source)
SampleTests.After/              xUnit test project (target)
```

## How it works

1. **Read** `migration.wrapgod.json` to understand the NUnit API surface
2. **Apply** rules from `migration.wrapgod.config.json` to transform source code
3. **Compare** `SampleTests.Before/` and `SampleTests.After/` to verify the migration

## Scenarios covered (30+)

| Category | Scenarios |
|----------|-----------|
| **Attributes** | `[Test]` -> `[Fact]`, `[TestCase]` -> `[Theory]`+`[InlineData]`, `[TestCaseSource]` -> `[MemberData]`/`[ClassData]`, `[Category]` -> `[Trait]`, `[Property]` -> `[Trait]`, `[Ignore]` -> `[Fact(Skip)]` |
| **Lifecycle** | `[SetUp]` -> Constructor, `[TearDown]` -> `IDisposable`, `[OneTimeSetUp]`/`[OneTimeTearDown]` -> `IClassFixture<T>`, `[SetUpFixture]` -> `ICollectionFixture<T>`, `TestContext.Out` -> `ITestOutputHelper` |
| **Assertions** | `Assert.That(Is.EqualTo)` -> `Assert.Equal`, `Is.True/False`, `Is.Null/Not.Null`, `Assert.Throws<T>`, `Assert.ThrowsAsync<T>`, `Does.Contain`, `Is.Empty`, `Is.TypeOf<T>`, `Is.InRange`, `Has.All.Matches`, `Has.Count.EqualTo(1)`, `Does.StartWith/EndWith`, `Does.Match` |

## Key differences

| Concept | NUnit | xUnit |
|---------|-------|-------|
| Test marker | `[Test]` | `[Fact]` |
| Parameterized | `[TestCase]` (combined) | `[Theory]` + `[InlineData]` |
| Assertions | Constraint model with `Assert.That` | Static methods with expected-first |
| Per-test setup | `[SetUp]` method (instance reused) | Constructor (new instance per test) |
| Per-test teardown | `[TearDown]` method | `IDisposable.Dispose()` |
| One-time setup | `[OneTimeSetUp]` (attribute) | `IClassFixture<T>` (DI) |
| Cross-class sharing | `[SetUpFixture]` (namespace) | `ICollectionFixture<T>` |
| Output | `TestContext.Out` (static) | `ITestOutputHelper` (injected) |
| Skip | `[Ignore("...")]` | `[Fact(Skip = "...")]` |
| Parallelism | Off by default (`[Parallelizable]`) | On by default (per collection) |

## Risky / unsupported patterns

| NUnit pattern | xUnit | Risk |
|---------------|-------|------|
| `Has.All.Matches<T>(pred)` | `Assert.All(coll, action)` | Predicate -> action rewrite |
| `[SetUpFixture]` | `ICollectionFixture<T>` | Architectural change |
| `[Parallelizable]` | Collection-based parallelism | Opposite defaults; review strategy |

## Validate

Compare `SampleTests.Before/` and `SampleTests.After/` side by side. Both
projects compile and run the same test logic with equivalent semantics.

```bash
dotnet test examples/NUnitToXUnit/SampleTests.Before/
dotnet test examples/NUnitToXUnit/SampleTests.After/
```
