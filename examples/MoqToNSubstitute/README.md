# Moq to NSubstitute Migration Example

This example demonstrates how WrapGod can orchestrate a migration from
[Moq](https://github.com/devlooped/moq) to
[NSubstitute](https://nsubstitute.github.io/) in a .NET test project.

## Directory layout

```
MoqToNSubstitute/
  SampleTests.Before/     -- test project using Moq
  SampleTests.After/      -- same tests rewritten with NSubstitute
  migration.wrapgod.json  -- manifest describing the Moq API surface
  migration.wrapgod.config.json -- mapping rules from Moq to NSubstitute
```

## Semantic differences

Moq and NSubstitute differ in fundamental design philosophy, which impacts
migration beyond simple find-and-replace:

| Aspect | Moq | NSubstitute |
|--------|-----|-------------|
| Creation | `new Mock<T>()` wraps T | `Substitute.For<T>()` returns T directly |
| Access | `mock.Object` to get instance | Substitute *is* the instance |
| Setup | `mock.Setup(x => x.Foo()).Returns(bar)` | `sub.Foo().Returns(bar)` |
| Verify | `mock.Verify(x => x.Foo(), Times.Once)` | `sub.Received(1).Foo()` |
| Never called | `Times.Never` | `sub.DidNotReceive().Foo()` |
| Any arg | `It.IsAny<T>()` | `Arg.Any<T>()` |
| Predicate | `It.Is<T>(pred)` | `Arg.Is<T>(pred)` |

## What WrapGod does at each step

### 1. Extract the Moq API surface

The `migration.wrapgod.json` manifest captures `Mock<T>`, `It`, `Times`, and
setup/verify infrastructure. In production this comes from the extractor.

### 2. Define the mapping configuration

`migration.wrapgod.config.json` maps each Moq pattern to NSubstitute with
safety ratings:

- **safe** -- Direct structural mapping (e.g., `It.IsAny<T>()` to `Arg.Any<T>()`)
- **guided** -- Requires restructuring (e.g., `Callback`, `MockBehavior.Strict`)
- **manual** -- No equivalent exists (e.g., `VerifyAll`, `VerifyNoOtherCalls`)

### 3. Key migration patterns

**Mock creation and access:**
```csharp
// Moq
var mock = new Mock<IService>();
var sut = new MyClass(mock.Object);

// NSubstitute
var service = Substitute.For<IService>();
var sut = new MyClass(service);
```

**Setup and returns:**
```csharp
// Moq
mock.Setup(x => x.GetById(1)).Returns(expected);

// NSubstitute
service.GetById(1).Returns(expected);
```

**Verification:**
```csharp
// Moq
mock.Verify(x => x.Save(It.IsAny<User>()), Times.Once);
mock.Verify(x => x.Delete(It.IsAny<int>()), Times.Never);

// NSubstitute
service.Received(1).Save(Arg.Any<User>());
service.DidNotReceive().Delete(Arg.Any<int>());
```

### 4. Risky or unsupported patterns

| Moq pattern | NSubstitute | Risk |
|-------------|-------------|------|
| `MockBehavior.Strict` | No equivalent | Tests may pass when they should fail |
| `VerifyAll()` | Manual per-call Received | Must list each expectation |
| `VerifyNoOtherCalls()` | No equivalent | Cannot detect unexpected calls |
| `Callback(action)` | Returns lambda | Structural rewrite |

### 5. Validate

Compare `SampleTests.Before/` and `SampleTests.After/` side by side. Both
projects compile and exercise the same service logic with equivalent
verification semantics.

## Running the examples

```bash
# Build and run the "before" tests
dotnet test examples/MoqToNSubstitute/SampleTests.Before/

# Build and run the "after" tests
dotnet test examples/MoqToNSubstitute/SampleTests.After/
```
