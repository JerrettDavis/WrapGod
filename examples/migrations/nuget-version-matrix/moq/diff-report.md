# Moq Version-Matrix Diff Report

API delta analysis across Moq v4.10 -> v4.16 -> v4.20.

## Delta 1: Protected Member Mocking Evolution

| Aspect | v4.10 | v4.16 | v4.20 |
|--------|-------|-------|-------|
| Protected Setup | `Protected().Setup("MethodName", args)` | `Protected().As<TAnalog>().Setup(x => x.Method())` | Same as v4.16 + SetupGet/SetupSet |
| Type Safety | None (string-based) | Full (expression-based via analog) | Full + property support |
| API | `IProtectedMock<T>` | + `IProtectedAsMock<T, TAnalog>` (4.15) | + SetupGet/SetupSet/VerifyGet/VerifySet (4.18) |

**Migration safety:** `review`

The string-based `Protected().Setup("MethodName", ...)` API is still available in all versions, so it works as an LCD fallback. However, `Protected().As<TAnalog>()` (introduced 4.15) provides compile-time safety and should be preferred when the minimum version allows it. In 4.18+, property-specific overloads on the analog interface complete the protected mocking story.

**Guidance:** If your minimum target is >= 4.15, use `Protected().As<TAnalog>()` exclusively. For LCD, fall back to string-based protected setup and accept the loss of compile-time checking.

---

## Delta 2: Callback Generic Arity Expansion

| Aspect | v4.10 | v4.16 | v4.20 |
|--------|-------|-------|-------|
| Max generic params | `Action<T1, T2, T3, T4>` (4 params) | `Action<T1, ..., T16>` (16 params) | Same as v4.16 |
| Overloads | 5 (0-4 params) | 17 (0-16 params) | 17 (0-16 params) |

**Migration safety:** `safe`

The expansion is purely additive. Code targeting v4.10 callbacks continues to work on v4.16+. The only concern is wrapper generation: LCD strategies must cap at arity 4, while targeted strategies can expose all 16 overloads.

**Guidance:** Methods with more than 4 parameters are a code smell but exist in legacy codebases. If your test subjects have high-arity methods, target >= 4.11.

---

## Delta 3: Verifiable(Times) Setup-Time Invocation Counts

| Aspect | v4.10 | v4.16 | v4.20 |
|--------|-------|-------|-------|
| Verifiable | `.Verifiable()` or `.Verifiable("message")` | Same as v4.10 | + `.Verifiable(Times.Once)` and `.Verifiable(Times.Once, "message")` |
| Verification | Times specified at `Verify()` call site | Same as v4.10 | Times can be specified at setup or verify |

**Migration safety:** `safe`

This is a purely additive API. Existing `Verifiable()` calls work unchanged. The new overload allows specifying expected call counts at setup time rather than at verify time, enabling a more declarative test style.

**Guidance:** For LCD, omit the `Verifiable(Times)` overloads from generated wrappers. For v4.20-targeted wrappers, expose them to enable the declarative pattern: `mock.Setup(x => x.Foo()).Returns(42).Verifiable(Times.Once)`.

---

## Delta 4: Event Subscription Mocking (SetupAdd / SetupRemove)

| Aspect | v4.10 | v4.16 | v4.20 |
|--------|-------|-------|-------|
| Event add/remove setup | Not available | `SetupAdd(x => x.Event += ...)` / `SetupRemove(...)` | Same as v4.16 |
| Event add/remove verify | Not available | `VerifyAdd(...)` / `VerifyRemove(...)` | Same as v4.16 |

**Migration safety:** `review`

Before 4.13, there was no way to mock event subscription/unsubscription handlers. Tests relying on event subscription verification cannot target v4.10. This is a feature gap, not a breaking change -- code that doesn't use these APIs is unaffected.

**Guidance:** If your test suite verifies event subscription behavior, your minimum version must be >= 4.13. LCD wrappers exclude these methods entirely. Adaptive wrappers gate them behind `MOQ_V4_13`.

---

## Delta 5: Sequential Setup Enhancements

| Aspect | v4.10 | v4.16 | v4.20 |
|--------|-------|-------|-------|
| Return sequences | `SetupSequence(x => x.Foo()).Returns(1).Returns(2)` | Same | Same |
| Void sequences | Not available | `SetupSequence(x => x.Bar()).Pass().Throws(ex)` | Same |
| Async sequences | Not available | `SetupSequence(...).ReturnsAsync(1).ReturnsAsync(2)` | Same |
| CallBase in sequence | Not available | Not available | `.CallBase()` in sequential chain (4.18) |
| Interface | `ISetupSequentialResult<T>` only | + `ISetupSequentialAction` (4.12) | + `CallBase()` on both (4.18) |

**Migration safety:** `review`

v4.10 supports only return-value sequential setup. Void method sequences and async sequences require >= 4.12. The `CallBase()` chain method for delegating to the base implementation in a sequence requires >= 4.18.

**Guidance:** LCD excludes `ISetupSequentialAction` and async sequential helpers. Tests that need sequential void behavior must target >= 4.12. The `CallBase()` chain is a niche feature primarily useful for partial mocks -- gate behind `MOQ_V4_18` in adaptive builds.

---

## Delta 6: RaiseAsync for Async Event Handlers

| Aspect | v4.10 | v4.16 | v4.20 |
|--------|-------|-------|-------|
| Sync event raising | `mock.Raise(x => x.Event += null, args)` | Same | Same |
| Async event raising | Not available | Not available | `mock.RaiseAsync(x => x.Event += null, args)` |

**Migration safety:** `safe`

Purely additive. `RaiseAsync` is a v4.20-only feature that enables proper testing of async event handlers (delegates returning `Task`). Before 4.20, raising such events synchronously could mask timing issues or cause deadlocks in test runners.

**Guidance:** Exclude from LCD and v4.16 targets. For v4.20 targets, expose to enable proper async event testing patterns.

---

## Delta 7: Mock Introspection (Invocations / Setups)

| Aspect | v4.10 | v4.16 | v4.20 |
|--------|-------|-------|-------|
| Invocation list | Not available | Not available | `mock.Invocations` (`IInvocationList`) |
| Setup list | Not available | Not available | `mock.Setups` (`ISetupList`) |
| Invocation clearing | Not available | Not available | `mock.Invocations.Clear()` |

**Migration safety:** `review`

Introduced in 4.17, these properties expose the mock's internal recorded state. Useful for custom verification logic, diagnostics, or resetting invocation state between test phases. Code that relies on invocation introspection cannot target < 4.17.

**Guidance:** LCD excludes entirely. Adaptive builds gate behind `MOQ_V4_17`. These are power-user features -- most test suites don't need them.

---

## Summary Matrix

| Feature | Introduced | LCD (4.10+) | Targeted (4.20) | Adaptive |
|---------|-----------|-------------|-----------------|----------|
| String-based protected mocking | 4.0 | Included | Included (deprecated style) | All tiers |
| Type-safe protected (`As<T>()`) | 4.15 | Excluded | Included (preferred) | >= 4.15 |
| Protected property analog | 4.18 | Excluded | Included | >= 4.18 |
| Callback arity 5-16 | 4.11-4.16 | Excluded | Included | >= 4.11/4.16 |
| SetupAdd / SetupRemove | 4.13 | Excluded | Included | >= 4.13 |
| Void SetupSequence | 4.12 | Excluded | Included | >= 4.12 |
| Async SetupSequence | 4.12 | Excluded | Included | >= 4.12 |
| Sequential CallBase | 4.18 | Excluded | Included | >= 4.18 |
| It.IsNotNull | 4.14 | Excluded | Included | >= 4.14 |
| MockRepository.Of | 4.14 | Excluded | Included | >= 4.14 |
| Invocations / Setups | 4.17 | Excluded | Included | >= 4.17 |
| Verifiable(Times) | 4.20 | Excluded | Included | >= 4.20 |
| RaiseAsync | 4.20 | Excluded | Included | >= 4.20 |
| Switches (diagnostics) | 4.15 | Excluded | Included | >= 4.15 |
