# Moq to NSubstitute Migration Example

This example demonstrates how WrapGod orchestrates migration from
[Moq](https://github.com/devlooped/moq) to [NSubstitute](https://nsubstitute.github.io/)
in a .NET test project.

## Directory layout

```
MoqToNSubstitute/
  SampleTests.Before/     -- test project using Moq (25 tests)
  SampleTests.After/      -- same tests rewritten with NSubstitute (25 tests)
  migration.wrapgod.json  -- manifest describing the Moq API surface
  migration.wrapgod.config.json -- mapping rules (22 mappings)
  mapping-matrix.md       -- comprehensive bidirectional pattern matrix (30 rows)
```

## Scenarios covered (25 bidirectional)

| # | Pattern | Safety |
|---|---------|--------|
| 1 | Basic setup + returns | safe |
| 2 | Returns null | safe |
| 3 | Verify called once | safe |
| 4 | Verify never called | safe |
| 5 | Argument predicate (It.Is / Arg.Is) | safe |
| 6 | Async method mocking (ReturnsAsync) | safe |
| 7 | Async void method (Task-returning) | safe |
| 8 | Exception throwing (Throws) | safe |
| 9 | Sequential returns (SetupSequence) | safe |
| 10 | Callback with arguments | review |
| 11 | Property get mocking | safe |
| 12 | Property set verification | safe |
| 13 | Returns from function (computed) | review |
| 14 | Verify exactly N times | safe |
| 15 | Verify at least once | safe |
| 16 | Void method setup + verify | safe |
| 17 | Argument range matching | safe |
| 18 | Generic interface mocking | safe |
| 19 | Overloaded method verification | safe |
| 20 | MockBehavior.Strict | review |
| 21 | VerifyAll | manual |
| 22 | VerifyNoOtherCalls | manual |
| 23 | Async exception throwing | safe |
| 24 | Event raising | safe |
| 25 | Out parameter handling | review |

## Key differences

### Structural

| Aspect | Moq | NSubstitute |
|--------|-----|-------------|
| Creation | `new Mock<T>()` wraps T | `Substitute.For<T>()` returns T directly |
| Access | `mock.Object` | _(substitute is T)_ |
| Setup | `mock.Setup(x => x.M())` | `sub.M()` directly |
| Verify | `mock.Verify(x => x.M(), Times.Once)` | `sub.Received(1).M()` |

### Risky / unsupported patterns

| Moq pattern | NSubstitute | Notes |
|-------------|-------------|-------|
| `MockBehavior.Strict` | No equivalent | Tests may pass silently |
| `VerifyAll()` | Manual per-call Received | Must enumerate each expectation |
| `VerifyNoOtherCalls()` | No equivalent | Cannot detect unexpected calls |
| `Callback(action)` | `When().Do()` / Returns lambda | Structural rewrite required |

## Running the examples

```bash
# Build and run the "before" tests (Moq)
dotnet test examples/MoqToNSubstitute/SampleTests.Before/

# Build and run the "after" tests (NSubstitute)
dotnet test examples/MoqToNSubstitute/SampleTests.After/
```

Both projects should produce 25 passing tests with equivalent verification semantics.
