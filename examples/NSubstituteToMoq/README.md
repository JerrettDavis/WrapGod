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
  SampleTests.Before/     -- test project using NSubstitute
  SampleTests.After/      -- same tests rewritten with Moq
  migration.wrapgod.json  -- manifest describing the NSubstitute API surface
  migration.wrapgod.config.json -- mapping config with asymmetry notes
```

## Key asymmetries vs forward migration

| Aspect | Forward (Moq -> NSub) | Reverse (NSub -> Moq) |
|--------|----------------------|----------------------|
| Creation | Remove wrapper: `Mock<T>` -> direct T | Add wrapper: direct T -> `Mock<T>` + `.Object` |
| Setup | Unwrap lambda: `Setup(x => ...)` -> direct call | Wrap in lambda: direct call -> `Setup(x => ...)` |
| Verify | Simplify: `Verify(..., Times.Once)` -> `Received(1)` | Add ceremony: `Received(1)` -> `Verify(..., Times.Once)` |
| Arg capture | Safe: `Callback` -> `Arg.Do` (simpler) | Risky: `Arg.Do` -> `Callback` (structural) |

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

## Running the examples

```bash
# Build and run the "before" tests (NSubstitute)
dotnet test examples/NSubstituteToMoq/SampleTests.Before/

# Build and run the "after" tests (Moq)
dotnet test examples/NSubstituteToMoq/SampleTests.After/
```
