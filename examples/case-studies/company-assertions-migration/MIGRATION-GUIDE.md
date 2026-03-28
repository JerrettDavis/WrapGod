# Enterprise Assertions Unification: Company.Assertions Migration Guide

This case study demonstrates how an enterprise with multiple legacy applications -- each
using different assertion libraries (FluentAssertions v5, v6, v8; Shouldly 4.1, 4.3; or a
mix) -- can consolidate onto a single `Company.Assertions` wrapper package backed by Shouldly.

---

## 1. Original State (Before Migration)

### File Tree

```
LegacyApp.A/          FluentAssertions 6.12.0
LegacyApp.B/          FluentAssertions 8.3.0 + Shouldly 4.3.0 (mixed)
LegacyApp.C/          Shouldly 4.1.0
LegacyApp.D/          FluentAssertions 5.10.3 + custom helpers
```

### Package References per App

| App | FluentAssertions | Shouldly | Custom Helpers |
|-----|-----------------|----------|----------------|
| LegacyApp.A | 6.12.0 | -- | No |
| LegacyApp.B | 8.3.0 | 4.3.0 | No |
| LegacyApp.C | -- | 4.1.0 | No |
| LegacyApp.D | 5.10.3 | -- | Yes (`AssertionHelpers`) |

### Divergent Assertion Idioms

**LegacyApp.A** -- FluentAssertions v6 style:

```csharp
total.Should().Be(39.49m);
order.Should().NotBeNull();
order.Items.Should().HaveCount(3);
act.Should().Throw<ArgumentException>();
```

**LegacyApp.B** -- Mixed FA + Shouldly in the same file:

```csharp
// FluentAssertions style
user.Name.Should().NotBeNullOrEmpty();
user.Email.Should().Contain("@");

// Shouldly style (same file!)
user.Age.ShouldBeGreaterThan(0);
user.DisplayName.ShouldBe("frank");
```

**LegacyApp.C** -- Older Shouldly 4.1.0:

```csharp
result.ShouldBe(5);
Calculator.IsEven(4).ShouldBeTrue();
Should.Throw<DivideByZeroException>(() => Calculator.Divide(10, 0));
```

**LegacyApp.D** -- FluentAssertions v5 + custom assertion helpers:

```csharp
payment.Amount.Should().BeGreaterThan(0);
payment.Tags.Should().Contain("Online");

// Custom wrapper that delegates to FA internally
AssertionHelpers.AssertIsTrue(payment.IsRefunded);
```

---

## 2. Company.Assertions Wrapper Convention

### What It Provides

`Company.Assertions` is a WrapGod-generated facade library that:

1. References Shouldly 4.3.0 internally as its implementation dependency
2. **Generates wrapper extension methods** under the `Company.Assertions` namespace
   that fully wrap the Shouldly public API surface
3. Consumers use `using Company.Assertions;` exclusively — never `using Shouldly;`

This is exactly what WrapGod's generation pipeline produces: a complete facade namespace
that decouples consumer code from the underlying assertion library.

```xml
<!-- Company.Assertions.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Shouldly" Version="4.3.0" />
  </ItemGroup>
</Project>
```

```csharp
// AssertionExtensions.cs — WrapGod-generated facade over Shouldly.
// Each method delegates to the underlying Shouldly implementation.
namespace Company.Assertions;

public static class AssertionExtensions
{
    public static void ShouldBe<T>(this T actual, T expected)
        => Shouldly.ShouldBeTestExtensions.ShouldBe(actual, expected);

    public static void ShouldNotBeNull<T>(this T? actual) where T : class
        => Shouldly.ShouldBeNullExtensions.ShouldNotBeNull(actual);

    public static void ShouldBeTrue(this bool actual)
        => Shouldly.ShouldBeBooleanExtensions.ShouldBeTrue(actual);

    // ... full Shouldly API surface wrapped under Company.Assertions
}

// Static assertion facade — wraps Shouldly.Should
public static class Should
{
    public static TException Throw<TException>(Action actual) where TException : Exception
        => Shouldly.Should.Throw<TException>(actual);
}
```

### Why This Convention Scales

- **Complete decoupling**: Consumer code only knows the `Company.Assertions` namespace.
  The Shouldly dependency is an implementation detail, not a consumer concern.
- **Single dependency**: Every test project references `Company.Assertions` instead of
  Shouldly directly. One place to update the version.
- **Swappable implementation**: If the enterprise later switches from Shouldly to another
  library, only `Company.Assertions` regenerates. All consumer code is untouched.
- **WrapGod pipeline**: The generation is automated — run the extractor on Shouldly,
  generate facades via WrapGod, and the `Company.Assertions` namespace is ready.
- **Governance**: Package approval processes only vet one assertion dependency.

### Consumer Setup

```xml
<!-- Any test project -->
<ItemGroup>
  <ProjectReference Include="..\Company.Assertions\Company.Assertions.csproj" />
</ItemGroup>
```

```csharp
using Company.Assertions;    // The ONLY using needed — full assertion API available

result.ShouldBe(42);         // Delegated to Shouldly internally
Should.Throw<Exception>(act); // Static facade, also delegated
```

---

## 3. Migration Execution

### Per-App Steps

Repeat the following for each legacy application. The example uses LegacyApp.A.

#### Step 1: Add Company.Assertions reference

```bash
dotnet add LegacyApp.A/LegacyApp.A.csproj reference Company.Assertions/Company.Assertions.csproj
```

#### Step 2: Remove old assertion packages

```bash
# For FluentAssertions apps:
dotnet remove LegacyApp.A/LegacyApp.A.csproj package FluentAssertions

# For mixed apps (LegacyApp.B), remove both:
dotnet remove LegacyApp.B/LegacyApp.B.csproj package FluentAssertions
dotnet remove LegacyApp.B/LegacyApp.B.csproj package Shouldly

# For older Shouldly apps (LegacyApp.C), remove Shouldly:
dotnet remove LegacyApp.C/LegacyApp.C.csproj package Shouldly
```

#### Step 3: Update using directives

Search and replace across the project:

```
Replace:  using FluentAssertions;
With:     using Company.Assertions;
```

The `Company.Assertions` namespace provides the full wrapped API surface.
Consumer code never references `Shouldly` directly.

#### Step 4: Run WrapGod analyzer + code fixes

```bash
dotnet format LegacyApp.A/LegacyApp.A.csproj --diagnostics WG2001 --severity info
```

#### Step 5: Manual transformations

Some patterns require manual rewriting:

| Pattern | Before (FA) | After (Shouldly) | Notes |
|---------|-------------|-------------------|-------|
| Exception assert | `act.Should().Throw<T>()` | `Should.Throw<T>(act)` | Structural change |
| Collection count | `.Should().HaveCount(n)` | `.Count.ShouldBe(n)` | Property access |
| Custom helpers | `AssertionHelpers.AssertIsTrue(x)` | `x.ShouldBeTrue()` | Remove helper class |

### All Four Apps

```bash
# LegacyApp.A (FA v6 only)
dotnet add LegacyApp.A/LegacyApp.A.csproj reference Company.Assertions/Company.Assertions.csproj
dotnet remove LegacyApp.A/LegacyApp.A.csproj package FluentAssertions
dotnet format LegacyApp.A/LegacyApp.A.csproj --diagnostics WG2001 --severity info

# LegacyApp.B (FA v8 + Shouldly mixed)
dotnet add LegacyApp.B/LegacyApp.B.csproj reference Company.Assertions/Company.Assertions.csproj
dotnet remove LegacyApp.B/LegacyApp.B.csproj package FluentAssertions
dotnet remove LegacyApp.B/LegacyApp.B.csproj package Shouldly
dotnet format LegacyApp.B/LegacyApp.B.csproj --diagnostics WG2001 --severity info

# LegacyApp.C (Shouldly 4.1 only)
dotnet add LegacyApp.C/LegacyApp.C.csproj reference Company.Assertions/Company.Assertions.csproj
dotnet remove LegacyApp.C/LegacyApp.C.csproj package Shouldly
dotnet format LegacyApp.C/LegacyApp.C.csproj --diagnostics WG2001 --severity info

# LegacyApp.D (FA v5 + custom helpers)
dotnet add LegacyApp.D/LegacyApp.D.csproj reference Company.Assertions/Company.Assertions.csproj
dotnet remove LegacyApp.D/LegacyApp.D.csproj package FluentAssertions
dotnet format LegacyApp.D/LegacyApp.D.csproj --diagnostics WG2001 --severity info
```

---

## 4. Resulting State (After Migration)

### Updated Code Snippets

**Migrated.App.A** (was FA v6):

```csharp
using Company.Assertions;

total.ShouldBe(39.49m);
order.ShouldNotBeNull();
order.Items.Count.ShouldBe(3);
Should.Throw<ArgumentException>(() => CreateOrder(""));
```

**Migrated.App.B** (was mixed FA + Shouldly):

```csharp
using Company.Assertions;

// All assertions now use the Company.Assertions facade
user.Name.ShouldNotBeNullOrEmpty();
user.Email.ShouldContain("@");
user.Age.ShouldBeGreaterThan(0);
user.DisplayName.ShouldBe("frank");
```

**Migrated.App.C** (was Shouldly 4.1):

```csharp
using Company.Assertions;   // Same API surface, now through centralized facade

result.ShouldBe(5);         // Delegates to Shouldly 4.3.0 internally
```

**Migrated.App.D** (was FA v5 + custom helpers):

```csharp
using Company.Assertions;

payment.Amount.ShouldBeGreaterThan(0);
payment.Tags.ShouldContain("Online");

// Custom AssertionHelpers class removed entirely
payment.IsRefunded.ShouldBeFalse();
payment.IsRefunded = true;
payment.IsRefunded.ShouldBeTrue();
```

### Package References After Migration

| App | FluentAssertions | Shouldly | Company.Assertions |
|-----|-----------------|----------|--------------------|
| Migrated.App.A | -- | -- | Project ref |
| Migrated.App.B | -- | -- | Project ref |
| Migrated.App.C | -- | -- | Project ref |
| Migrated.App.D | -- | -- | Project ref |

---

## 5. Validation

### Build

```bash
dotnet build Company.Assertions.Migration.slnx --nologo
```

All 9 projects (1 library + 4 legacy + 4 migrated) should compile successfully.

### Test

```bash
dotnet test Company.Assertions.Migration.slnx --nologo
```

All 76 tests (duplicated across Before and After projects) should pass.

### Diff Summary

| File | Key Changes |
|------|-------------|
| `*.csproj` | `FluentAssertions`/`Shouldly` package refs replaced with `Company.Assertions` project ref |
| `*.cs` | `using FluentAssertions;` replaced with `using Company.Assertions;` (WrapGod-generated facade) |
| `OrderTests.cs` | `.Should().X()` calls replaced with `.ShouldX()` |
| `UserTests.cs` | Mixed FA/Shouldly unified to Shouldly-only via Company.Assertions |
| `CalculatorTests.cs` | `using Shouldly;` replaced with `using Company.Assertions;` (same API through facade) |
| `PaymentTests.cs` | FA calls replaced + `AssertionHelpers` class removed |

---

## 6. Safety and Boundaries

### Safe Transformations (Automated)

All mappings marked `"safety": "safe"` in `migration.wrapgod.config.json` can be applied
automatically without risk of semantic change:

- `.Should().Be(x)` to `.ShouldBe(x)`
- `.Should().BeTrue()` / `.BeFalse()` to `.ShouldBeTrue()` / `.ShouldBeFalse()`
- `.Should().NotBeNull()` to `.ShouldNotBeNull()`
- `.Should().Contain(x)` to `.ShouldContain(x)`
- `.Should().StartWith(x)` / `.EndWith(x)` to `.ShouldStartWith(x)` / `.ShouldEndWith(x)`
- `.Should().BeEmpty()` to `.ShouldBeEmpty()`
- `.Should().BeGreaterThan(x)` to `.ShouldBeGreaterThan(x)`

### Guided Transformations (Manual Review)

These require structural changes and should be reviewed:

| Pattern | Risk | Recommendation |
|---------|------|----------------|
| `act.Should().Throw<T>()` | Structural rewrite to `Should.Throw<T>(act)` | Verify lambda capture |
| `.Should().HaveCount(n)` | Rewrite to `.Count.ShouldBe(n)` | Verify `.Count` exists on type |
| `.Should().BeInAscendingOrder()` | Rewrite to sorted comparison | Manual review required |
| Custom `AssertionHelpers` | Inline replacement | Remove helper class after migration |

### Unsupported (Not Covered)

- FluentAssertions `WithMessage()` fluent chains (Shouldly uses different error customization)
- `act.Should().ThrowExactly<T>()` (Shouldly has no exact-type-only throw assertion)
- `x.Should().BeEquivalentTo(y)` (deep structural comparison; Shouldly has no built-in equivalent)
- Chained `.And.` / `.Which.` assertions (no Shouldly equivalent; split into separate assertions)

These patterns must be handled manually and are outside WrapGod's automated scope.
