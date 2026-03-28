# FluentAssertions NuGet Version-Matrix Example

This example demonstrates how WrapGod handles **API drift across different versions of the same NuGet package**. Rather than migrating from one library to another (e.g., FluentAssertions to Shouldly), this scenario addresses the challenge of supporting or migrating between major versions of FluentAssertions itself.

## The problem

FluentAssertions has undergone significant API changes across major versions:

- **v5 -> v6**: Removed `ActionAssertions` entry point, added `ThrowExactly`, changed async assertion types, added `NotBeNullOrWhiteSpace`, `BeCloseTo`, `BooleanAssertions.NotBe`
- **v6 -> v8**: Replaced `Execute.Assertion` with `AssertionChain`, renamed `EquivalencyAssertionOptions` to `EquivalencyAssertionConfiguration`, added `Satisfy`, changed `BeEquivalentTo` signature

Teams upgrading FluentAssertions, or maintaining libraries that must support multiple versions, need to understand exactly which APIs are available in each version and how to handle the differences.

## Directory layout

```
fluent-assertions/
  v5/manifest.wrapgod.json           -- API surface snapshot for FA 5.10.3
  v6/manifest.wrapgod.json           -- API surface snapshot for FA 6.12.2
  v8/manifest.wrapgod.json           -- API surface snapshot for FA 8.3.0
  strategies/
    lcd.wrapgod.config.json           -- Lowest-common-denominator strategy
    targeted-v8.wrapgod.config.json   -- Target v8 exclusively
    adaptive.wrapgod.config.json      -- Runtime version detection
  diff-report.md                      -- Detailed API delta report
  SampleTests/
    VersionMatrixTests.cs             -- Code showing version-dependent patterns
```

## Version manifests

Each manifest captures the real FluentAssertions API surface for its major version, following the WrapGod manifest v1 schema. Key types included:

| Type Family | v5 | v6 | v8 |
|-------------|----|----|-----|
| `AssertionExtensions` (entry point) | `Should()` for Action, object, bool, string, collections | Action overload removed; async return type changed | Same as v6 |
| `ObjectAssertions` | Be, NotBe, BeNull, BeOfType, BeEquivalentTo | Same as v5 | BeEquivalentTo signature changed; Satisfy added |
| `BooleanAssertions` | BeTrue, BeFalse, Be | NotBe added | Same as v6 |
| `StringAssertions` | Be, Contain, StartWith, EndWith, BeEmpty, MatchRegex | NotBeNullOrWhiteSpace added | Same as v6 |
| `NumericAssertions` | Be, BeGreaterThan, BeLessThan, BePositive, BeNegative, BeInRange | BeCloseTo added | Same as v6 |
| `CollectionAssertions` | HaveCount, Contain, BeEmpty, NotContainNulls, BeInAscendingOrder, ContainSingle, SatisfyRespectively | AllSatisfy added | Satisfy added |
| `DelegateAssertions` | Throw, NotThrow | ThrowExactly added (via class restructuring) | Same as v6 |
| `ExceptionAssertions` | WithMessage, WithInnerException | Where added | Same as v6 |
| `EquivalencyAssertionOptions` | Present | Present | Removed; replaced by EquivalencyAssertionConfiguration |
| `AssertionScope` | Constructor, BecauseOf | CallerIdentity property added | Same as v6 |
| `AssertionChain` | N/A | N/A | New: GetOrCreate, BecauseOf |

The `presence` field on types and members tracks when APIs were introduced, removed, or changed using `introducedIn`, `removedIn`, and `changedIn`.

## Migration strategies

### LCD (Lowest Common Denominator)

**Use when:** Your code must compile against any FA version from v5 through v8.

The LCD strategy computes the **intersection** of all three version manifests and excludes any member not present in all versions. This means:
- No `ThrowExactly`, `NotBe(bool)`, `NotBeNullOrWhiteSpace`, `BeCloseTo` (v6+ only)
- No `Satisfy`, `AssertionChain` (v8+ only)
- Uses oldest signatures when conflicts exist

### Targeted v8

**Use when:** You are migrating your entire codebase to FluentAssertions v8.

The targeted strategy uses the v8 manifest as the source of truth and generates mappings from v5/v6 patterns to their v8 equivalents. Key mappings:
- `Execute.Assertion` -> `AssertionChain.GetOrCreate()`
- `AssertionOptions.EquivalencyPlan` -> `AssertionConfiguration.Current.Equivalency`
- `action.Should().Throw<T>()` (v5) -> `FluentActions.Invoking(...)` pattern

### Adaptive

**Use when:** You publish a library that must support consumers using different FA versions.

The adaptive strategy generates wrappers with **runtime version detection** that branch to the correct API surface based on the loaded assembly version. Members unavailable in older versions get fallback implementations.

## API deltas

See [diff-report.md](diff-report.md) for a comprehensive analysis of 10 meaningful API differences across versions, including:

| Delta | Type |
|-------|------|
| ActionAssertions removal | Removed |
| Execute.Assertion -> AssertionChain | Replaced |
| EquivalencyAssertionOptions -> Configuration | Renamed |
| BooleanAssertions.NotBe | Added in v6 |
| ThrowExactly | Added in v6 |
| BeEquivalentTo config parameter | Changed in v8 |
| Satisfy | Added in v8 |
| AsyncFunctionAssertions return type | Changed in v6 |
| NotBeNullOrWhiteSpace | Added in v6 |
| BeCloseTo | Added in v6 |

## Sample tests

`SampleTests/VersionMatrixTests.cs` demonstrates test code that would need different handling depending on the FA version. Each test method is annotated with the version-specific behavior and workarounds.

## How WrapGod uses this

1. **Extract** manifests from each target assembly version (or use the pre-built ones here)
2. **Diff** the manifests to identify additions, removals, renames, and signature changes
3. **Select a strategy** (LCD, targeted, or adaptive) based on your use case
4. **Generate** wrapper interfaces and implementations that abstract over version differences
5. **Validate** that generated wrappers compile against all target versions (LCD) or the target version (targeted)
