# FluentAssertions API Diff Report: v5 vs v6 vs v8

This report documents meaningful API deltas across FluentAssertions major versions (v5.10.3, v6.12.2, v8.3.0) as captured in the version manifests.

## 1. Exception Assertion Entry Point (Removed in v6)

**Type:** `FluentAssertions.Specialized.ActionAssertions`

| Version | Status | Usage |
|---------|--------|-------|
| v5 | Present | `action.Should().Throw<T>()` |
| v6 | Removed | Compile error: `Should()` no longer returns `ActionAssertions` for `Action` |
| v8 | Removed | Same as v6 |

**Impact:** In v5, `Action` had a `Should()` extension returning `ActionAssertions`. In v6+, this was removed. Use `FluentActions.Invoking(() => action()).Should().Throw<T>()` instead.

**WrapGod handling:** The v6 and v8 manifests mark this overload with `"removedIn": "6.0.0"`. The LCD strategy excludes it; the targeted-v8 strategy maps to the new pattern.

---

## 2. Execute.Assertion Replaced by AssertionChain (Changed in v8)

**Type:** `FluentAssertions.Execution.AssertionChain` (new in v8)

| Version | API | Usage |
|---------|-----|-------|
| v5 | `Execute.Assertion` | `Execute.Assertion.ForCondition(...)` |
| v6 | `Execute.Assertion` | Same as v5 |
| v8 | `AssertionChain.GetOrCreate()` | `AssertionChain.GetOrCreate().ForCondition(...)` |

**Impact:** Any custom assertion class that extends FluentAssertions via `Execute.Assertion` must be rewritten for v8. This is a breaking change for library authors building custom assertions.

**WrapGod handling:** `AssertionChain` appears in the v8 manifest with `"introducedIn": "8.0.0"`. The adaptive strategy runtime-branches between the two patterns.

---

## 3. EquivalencyAssertionOptions Renamed to EquivalencyAssertionConfiguration (Changed in v8)

**Type:** `FluentAssertions.Equivalency.EquivalencyAssertionOptions` -> `EquivalencyAssertionConfiguration`

| Version | Class | Usage |
|---------|-------|-------|
| v5 | `EquivalencyAssertionOptions` | `AssertionOptions.EquivalencyPlan.Using<T>(step)` |
| v6 | `EquivalencyAssertionOptions` | Same as v5 |
| v8 | `EquivalencyAssertionConfiguration` | `AssertionConfiguration.Current.Equivalency.Modify(opts => ...)` |

**Impact:** Global equivalency configuration entry point completely changed. The old class is removed; the new class has a different method signature (`Modify` with a delegate instead of `Using` with a step).

**WrapGod handling:** The v8 manifest marks `EquivalencyAssertionOptions` with `"removedIn": "8.0.0"` and introduces `EquivalencyAssertionConfiguration` with `"introducedIn": "8.0.0"`.

---

## 4. BooleanAssertions.NotBe Added in v6

**Type:** `FluentAssertions.Primitives.BooleanAssertions`

| Version | `NotBe(bool)` | Workaround |
|---------|---------------|------------|
| v5 | Not available | `value.Should().Be(!expected)` |
| v6 | Available | `value.Should().NotBe(expected)` |
| v8 | Available | Same as v6 |

**Impact:** Minor convenience addition. Code using `NotBe` will not compile against v5.

**WrapGod handling:** The v6 and v8 manifests mark this member with `"introducedIn": "6.0.0"`. The LCD strategy excludes it.

---

## 5. ThrowExactly Added in v6

**Type:** `FluentAssertions.Specialized.DelegateAssertions`1`

| Version | `ThrowExactly<T>()` | Workaround |
|---------|---------------------|------------|
| v5 | Not available | `Throw<T>().And.GetType().Should().Be(typeof(T))` |
| v6 | Available | `.Should().ThrowExactly<T>()` |
| v8 | Available | Same as v6 |

**Impact:** `ThrowExactly` asserts the exact exception type (not subclasses). In v5, this required a manual type check after `Throw`.

**WrapGod handling:** Marked `"introducedIn": "6.0.0"` in v6/v8 manifests. The adaptive strategy provides a v5 fallback.

---

## 6. BeEquivalentTo Signature Changed in v8

**Type:** `FluentAssertions.Primitives.ObjectAssertions`

| Version | Signature |
|---------|-----------|
| v5 | `BeEquivalentTo<T>(T expectation, string because, params object[] becauseArgs)` |
| v6 | Same as v5 |
| v8 | `BeEquivalentTo<T>(T expectation, Func<EquivalencyOptions<T>, EquivalencyOptions<T>> config, string because, params object[] becauseArgs)` |

**Impact:** The v8 signature added an optional `config` parameter for inline equivalency configuration. Existing calls compile (parameter is optional), but the method signature in the manifest differs.

**WrapGod handling:** The v8 manifest marks this member with `"changedIn": "8.0.0"`.

---

## 7. Satisfy Added in v8

**Type:** `FluentAssertions.Primitives.ObjectAssertions`, `FluentAssertions.Collections.GenericCollectionAssertions`1`

| Version | `Satisfy<T>(Action<T>)` |
|---------|------------------------|
| v5 | Not available |
| v6 | Not available |
| v8 | Available |

**Impact:** `Satisfy` enables nested assertions on reference types via an inspector delegate. Provides a more flexible alternative to `BeOfType<T>().Which` chains.

**WrapGod handling:** Marked `"introducedIn": "8.0.0"`. Not available in LCD strategy. The adaptive strategy has no direct fallback (recommends `.BeOfType<T>().Which` pattern).

---

## 8. AsyncFunctionAssertions Return Type Changed in v6

**Type:** `FluentAssertions.AssertionExtensions.Should(Func<Task>)`

| Version | Return Type |
|---------|-------------|
| v5 | `FluentAssertions.Specialized.AsyncFunctionAssertions` |
| v6 | `FluentAssertions.Specialized.NonGenericAsyncFunctionAssertions` |
| v8 | `FluentAssertions.Specialized.NonGenericAsyncFunctionAssertions` |

**Impact:** Code that explicitly stores the return type of `Should()` on async delegates will break. Most code using `await act.Should().ThrowAsync<T>()` is unaffected.

**WrapGod handling:** The v6 manifest marks this overload with `"changedIn": "6.0.0"`.

---

## 9. StringAssertions.NotBeNullOrWhiteSpace Added in v6

**Type:** `FluentAssertions.Primitives.StringAssertions`

| Version | `NotBeNullOrWhiteSpace()` | Workaround |
|---------|--------------------------|------------|
| v5 | Not available | `value.Should().NotBeNullOrEmpty()` + manual whitespace check |
| v6 | Available | `value.Should().NotBeNullOrWhiteSpace()` |
| v8 | Available | Same as v6 |

**WrapGod handling:** Marked `"introducedIn": "6.0.0"` in v6/v8 manifests. LCD strategy excludes it.

---

## 10. NumericAssertions.BeCloseTo Added in v6

**Type:** `FluentAssertions.Numeric.NumericAssertions`1`

| Version | `BeCloseTo(T, T)` | Workaround |
|---------|-------------------|------------|
| v5 | Not available | `value.Should().BeInRange(expected - delta, expected + delta)` |
| v6 | Available | `value.Should().BeCloseTo(expected, delta)` |
| v8 | Available | Same as v6 |

**WrapGod handling:** Marked `"introducedIn": "6.0.0"`. LCD strategy excludes it.

---

## Summary Matrix

| API | v5 | v6 | v8 | Delta Type |
|-----|----|----|----|----|
| `action.Should().Throw<T>()` | Yes | No | No | Removed |
| `Execute.Assertion` | Yes | Yes | No | Replaced |
| `EquivalencyAssertionOptions` | Yes | Yes | No | Renamed |
| `BooleanAssertions.NotBe` | No | Yes | Yes | Added |
| `ThrowExactly<T>()` | No | Yes | Yes | Added |
| `BeEquivalentTo` (config param) | No | No | Yes | Changed |
| `Satisfy<T>()` | No | No | Yes | Added |
| `AsyncFunctionAssertions` type | `Async...` | `NonGenericAsync...` | `NonGenericAsync...` | Changed |
| `NotBeNullOrWhiteSpace` | No | Yes | Yes | Added |
| `BeCloseTo` | No | Yes | Yes | Added |
