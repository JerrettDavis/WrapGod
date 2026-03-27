# FluentAssertions to Shouldly Migration Example

This example demonstrates how WrapGod can orchestrate a migration from
[FluentAssertions](https://fluentassertions.com/) to
[Shouldly](https://docs.shouldly.org/) in a .NET test project.

## Directory layout

```
FluentAssertionsToShouldly/
  SampleTests.Before/     -- test project using FluentAssertions
  SampleTests.After/      -- same tests rewritten with Shouldly
  migration.wrapgod.json  -- manifest describing the FA API surface
  migration.wrapgod.config.json -- mapping rules from FA to Shouldly
```

## What WrapGod does at each step

### 1. Extract the source API surface

The `migration.wrapgod.json` manifest captures the FluentAssertions types and
members that appear in the codebase. In a real workflow this would be produced
by the WrapGod extractor (`AssemblyExtractor`) scanning the FluentAssertions
assembly.

### 2. Define the mapping configuration

`migration.wrapgod.config.json` pairs each FluentAssertions call pattern with
its Shouldly equivalent. Each mapping carries a **safety** rating:

| Safety | Meaning |
|--------|---------|
| `safe` | Direct 1:1 replacement -- can be applied automatically |
| `guided` | Structural rewrite needed -- WrapGod flags it for review |
| `manual` | No safe equivalent exists -- developer must intervene |

### 3. Analyze and transform

WrapGod analyzers scan call sites, match them against the mapping config, and
produce diagnostics. Safe mappings get automatic code fixes; guided mappings
surface as warnings with suggested rewrites.

### 4. Key migration patterns

| FluentAssertions | Shouldly | Notes |
|-----------------|----------|-------|
| `x.Should().Be(y)` | `x.ShouldBe(y)` | Direct |
| `x.Should().BeTrue()` | `x.ShouldBeTrue()` | Direct |
| `x.Should().BeFalse()` | `x.ShouldBeFalse()` | Direct |
| `x.Should().BeNull()` | `x.ShouldBeNull()` | Direct |
| `x.Should().NotBeNull()` | `x.ShouldNotBeNull()` | Direct |
| `x.Should().BeOfType<T>()` | `x.ShouldBeOfType<T>()` | Direct |
| `x.Should().Contain(y)` | `x.ShouldContain(y)` | Works for strings and collections |
| `x.Should().StartWith(y)` | `x.ShouldStartWith(y)` | Direct |
| `x.Should().EndWith(y)` | `x.ShouldEndWith(y)` | Direct |
| `x.Should().BeEmpty()` | `x.ShouldBeEmpty()` | Direct |
| `x.Should().MatchRegex(p)` | `x.ShouldMatch(p)` | Direct |
| `x.Should().BeGreaterThan(y)` | `x.ShouldBeGreaterThan(y)` | Direct |
| `x.Should().BeNegative()` | `x.ShouldBeLessThan(0)` | No direct equivalent |
| `x.Should().HaveCount(n)` | `x.Count.ShouldBe(n)` | Restructured |
| `x.Should().NotContainNulls()` | `x.ShouldAllBe(i => i != null)` | Predicate approach |
| `x.Should().ContainSingle()` | `x.ShouldHaveSingleItem()` | Direct |
| `act.Should().Throw<T>()` | `Should.Throw<T>(act)` | Static method, structural change |

### 5. Validate

Compare `SampleTests.Before/` and `SampleTests.After/` side by side to verify
the migration produced semantically identical tests. Both projects compile and
their tests exercise the same logic.

## Running the examples

```bash
# Build and run the "before" tests
dotnet test examples/FluentAssertionsToShouldly/SampleTests.Before/

# Build and run the "after" tests
dotnet test examples/FluentAssertionsToShouldly/SampleTests.After/
```
