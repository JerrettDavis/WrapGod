# Moq Version-Matrix Example

Demonstrates how WrapGod handles API drift across different versions of the same NuGet package -- [Moq](https://github.com/devlooped/moq), the most popular .NET mocking library.

## Why This Matters

Large codebases often pin different projects to different Moq versions. A shared test infrastructure library needs to know: which Moq APIs are safe to use everywhere, and which require version-gating?

WrapGod solves this by generating wrapper interfaces from per-version manifests, then applying a strategy to determine what the wrapper exposes.

## Versions Covered

| Version | Era | Key Additions |
|---------|-----|---------------|
| **4.10** | 2018 -- stable baseline | Core `Mock<T>`, `Setup`, `Verify`, `It` matchers, string-based protected mocking |
| **4.16** | 2021 -- mid-lifecycle | `Callback` arity to 16 params, `Protected().As<TAnalog>()` type-safe protected mocking, void `SetupSequence` |
| **4.20** | 2023 -- final 4.x | `Verifiable(Times)`, `RaiseAsync`, invocation introspection, `CallBase()` in sequences |

## Directory Structure

```
moq/
  v4.10/manifest.wrapgod.json       # API surface for Moq 4.10.1
  v4.16/manifest.wrapgod.json       # API surface for Moq 4.16.1
  v4.20/manifest.wrapgod.json       # API surface for Moq 4.20.72
  strategies/
    lcd.wrapgod.config.json          # Lowest Common Denominator (4.10 baseline)
    targeted-v4.20.wrapgod.config.json  # Full v4.20 surface
    adaptive.wrapgod.config.json     # Tier-based with preprocessor gates
  diff-report.md                     # Detailed API delta analysis
  SampleTests/
    VersionMatrixTests.cs            # Patterns showing per-version differences
```

## Strategies

### LCD (Lowest Common Denominator)

Generates wrappers using only APIs present in **all** targeted versions (>= 4.10). Safe for any consumer but loses modern features:

- No type-safe protected mocking (`As<TAnalog>()`)
- No event subscription setup (`SetupAdd`/`SetupRemove`)
- No `Verifiable(Times)` declarative verification
- No `RaiseAsync` for async events
- Callback arity capped at 4 parameters

### Targeted v4.20

Generates wrappers with the **complete** v4.20 API surface. Recommended for greenfield projects or teams standardized on latest Moq. Includes all modern features.

### Adaptive

Generates wrappers with **compile-time version detection** using preprocessor directives. Consumers see the maximum API their Moq version supports:

```
Tier: baseline (>= 4.10)  -- Core mocking
Tier: enhanced (>= 4.13)  -- Event subscription, void sequences
Tier: typesafe (>= 4.15)  -- Protected().As<TAnalog>()
Tier: introspection (>= 4.17) -- Invocations, Setups
Tier: modern (>= 4.18)    -- Protected property analog, CallBase in sequences
Tier: latest (>= 4.20)    -- Verifiable(Times), RaiseAsync
```

## Key API Deltas

See [diff-report.md](diff-report.md) for the full analysis. Highlights:

1. **Protected member mocking** -- Evolved from error-prone string-based API to type-safe analog interface across 4.10 -> 4.15 -> 4.18
2. **Callback arity** -- Expanded from 4 to 16 generic parameters (4.11 -> 4.16)
3. **Sequential setup** -- Grew from return-values-only to void methods, async, and CallBase
4. **Verifiable(Times)** -- v4.20 enables declarative invocation count specification at setup time
5. **RaiseAsync** -- v4.20 fills the async event testing gap
6. **Invocation introspection** -- v4.17 opens the mock's recorded state for custom verification

## How WrapGod Uses This

```
1. Load manifests:  v4.10/manifest.wrapgod.json
                    v4.16/manifest.wrapgod.json
                    v4.20/manifest.wrapgod.json

2. Apply strategy:  strategies/lcd.wrapgod.config.json
                    -- OR --
                    strategies/adaptive.wrapgod.config.json

3. Generate:        IMoqWrapper<T> interface + implementation
                    with version-appropriate API surface
```

The generated wrapper insulates consuming code from version-specific APIs, making it safe to upgrade Moq incrementally across a solution without breaking shared test infrastructure.
