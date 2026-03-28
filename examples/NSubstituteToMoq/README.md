# NSubstitute to Moq Reverse Migration Example

This example demonstrates the **reverse** direction of the
[Moq to NSubstitute](../MoqToNSubstitute/) migration -- moving from
NSubstitute back to Moq using WrapGod.

## Why reverse migrations matter

Migrations are not always symmetric. Moving from NSubstitute to Moq introduces
complexity that the forward direction avoids:

- **Wrapper indirection**: NSubstitute returns the interface directly;
  Moq wraps it in `Mock<T>`, requiring `.Object` access at every injection
  point.
- **Lambda setup expressions**: NSubstitute calls methods directly;
  Moq requires `Setup(x => x.Method(...))` lambda wrappers.
- **One-way patterns**: `Arg.Do<T>()` and `ReceivedWithAnyArgs()` have no
  clean Moq equivalent.

## Directory layout

```
NSubstituteToMoq/
  SampleTests.Before/     -- test project using NSubstitute (25 tests)
  SampleTests.After/      -- same tests rewritten with Moq (25 tests)
  migration.wrapgod.json  -- manifest describing the NSubstitute API surface
  migration.wrapgod.config.json -- mapping config with asymmetry notes (19 mappings)
```

## Scenarios covered (25 bidirectional)

| # | Pattern | Safety |
|---|---------|--------|
| 1 | Basic setup + returns | safe |
| 2 | Verify called once (Received(1)) | safe |
| 3 | Verify never called (DidNotReceive) | safe |
| 4 | Argument predicate (Arg.Is) | safe |
| 5 | Argument matcher any (Arg.Any) | safe |
| 6 | Multiple verify calls | safe |
| 7 | Async method mocking | safe |
| 8 | Async returns false | safe |
| 9 | Exception throwing | safe |
| 10 | Async exception throwing | safe |
| 11 | Sequential returns | safe |
| 12 | Callback (When/Do -> Setup/Callback) | review |
| 13 | Returns from function (computed) | review |
| 14 | Property get mocking | safe |
| 15 | Property set verification | safe |
| 16 | Verify exact count | safe |
| 17 | Verify at least once | safe |
| 18 | ReceivedWithAnyArgs | review |
| 19 | Generic interface mocking | safe |
| 20 | Argument range matching (predicate) | safe |
| 21 | Event raising | safe |
| 22 | Out parameter handling | review |
| 23 | Arg.Do inline capture | manual |
| 24 | Default substitute returns defaults | safe |
| 25 | Audit failure path verification | safe |

## Key asymmetries vs forward migration

| Aspect | Forward (Moq -> NSub) | Reverse (NSub -> Moq) |
|--------|----------------------|----------------------|
| Creation | Remove wrapper: `Mock<T>` -> direct T | Add wrapper: direct T -> `Mock<T>` + `.Object` |
| Setup | Unwrap lambda: `Setup(x => ...)` -> direct call | Wrap in lambda: direct call -> `Setup(x => ...)` |
| Verify | Simplify: `Verify(..., Times.Once)` -> `Received(1)` | Add ceremony: `Received(1)` -> `Verify(..., Times.Once)` |
| Arg capture | Safe: `Callback` -> `Arg.Do` (simpler) | Risky: `Arg.Do` -> `Callback` (structural) |
| Sequential | Chain -> params: `SetupSequence` -> `Returns(a, b)` | Params -> chain: `Returns(a, b)` -> `SetupSequence` |

## Risky rewrites

The config includes an `asymmetries` section documenting patterns where the
reverse migration requires manual intervention:

1. **Substitute-is-T** (high risk) -- Every variable holding a substitute must
   become `Mock<T>` with `.Object` used at injection points.
2. **Arg.Do inline capture** (medium risk) -- Must be restructured as
   `Setup().Callback()`.
3. **When().Do()** (medium risk) -- Void method callbacks require different
   syntax in Moq.
4. **ReceivedWithAnyArgs** (low risk) -- Must enumerate each parameter with
   `It.IsAny<T>()`.
5. **Sequential returns** (low risk) -- Params array becomes chained
   `SetupSequence().Returns()` calls.

## Running the examples

```bash
# Build and run the "before" tests (NSubstitute)
dotnet test examples/NSubstituteToMoq/SampleTests.Before/

# Build and run the "after" tests (Moq)
dotnet test examples/NSubstituteToMoq/SampleTests.After/
```

Both projects should produce 25 passing tests with equivalent verification semantics.
