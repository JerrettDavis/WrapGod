# MediatR Version-Matrix Example

Demonstrates how WrapGod handles API drift across different versions of [MediatR](https://github.com/jbogard/MediatR), the most popular .NET mediator/CQRS library -- specifically focusing on **generic parameter changes** across major versions.

## Why This Matters

MediatR is heavily generic: `IRequest<TResponse>`, `IRequestHandler<TRequest, TResponse>`, `IPipelineBehavior<TRequest, TResponse>`, `IStreamRequest<TResponse>`, and more. Between v10, v11, and v12, the library underwent significant generic API changes:

- **Constraint tightening** (v10): Pipeline behaviors gained strict `where TRequest : IRequest<TResponse>` constraints
- **Signature normalization** (v11): CancellationToken parameter position standardized per CA1068
- **Constraint relaxation** (v12): All strict constraints replaced with `where TRequest : notnull`
- **Hierarchy restructuring** (v12): `IRequest` decoupled from `IRequest<Unit>`, void handlers return `Task` instead of `Task<Unit>`

These changes break wrapper generation strategies that assume stable generic signatures. WrapGod solves this by generating version-aware wrappers from per-version manifests.

## Versions Covered

| Version | Era | Key Generic Changes |
|---------|-----|---------------------|
| **10.0** | 2022 -- streaming era | Strict `IRequest<TResponse>` constraints on pipelines, `IStreamRequest<TResponse>` introduced with covariant `out TResponse`, contracts moved to separate package |
| **11.0** | 2022 -- normalization | CancellationToken moved to last parameter in pipeline behaviors (CA1068), strict constraints retained |
| **12.0** | 2023 -- simplification | All constraints relaxed to `notnull`, `IRequest` decoupled from `IRequest<Unit>`, void handlers return `Task` directly, `INotificationPublisher` added |

## Directory Structure

```
mediatr/
  v10/manifest.wrapgod.json          # API surface for MediatR 10.0.1
  v11/manifest.wrapgod.json          # API surface for MediatR 11.1.0
  v12/manifest.wrapgod.json          # API surface for MediatR 12.2.0
  strategies/
    lcd.wrapgod.config.json           # Lowest Common Denominator (v10 baseline)
    targeted-v12.wrapgod.config.json  # Full v12 surface
    adaptive.wrapgod.config.json      # Tier-based with preprocessor gates
  diff-report.md                      # Detailed API delta analysis
  SampleTests/
    VersionMatrixTests.cs             # Patterns showing per-version differences
```

## Strategies

### LCD (Lowest Common Denominator)

Generates wrappers using only APIs compatible with **all** targeted versions (>= v10). Uses the most restrictive generic constraints:

- Pipeline behaviors constrained to `where TRequest : IRequest<TResponse>` (v10/v11 strict)
- Void handlers return `Task<Unit>` (v10/v11 pattern)
- Pre/post processors constrained to `where TRequest : IBaseRequest`
- No `INotificationPublisher` (v12-only)
- No open-generic pipeline registration support

### Targeted v12

Generates wrappers with the **complete** v12 API surface. Recommended for greenfield projects:

- All constraints relaxed to `notnull` -- enables open-generic pipeline registration
- Void handlers return `Task` directly -- no Unit ceremony
- `INotificationPublisher` included for custom dispatch strategies
- NOT backward-compatible with v10/v11

### Adaptive

Generates wrappers with **compile-time version detection** using preprocessor directives:

```
Tier: baseline (>= v10)              -- Core CQRS with strict constraints
Tier: streaming (>= v10)             -- IStreamRequest, IStreamRequestHandler
Tier: relaxed-constraints (>= v12)   -- notnull constraints, open-generic pipelines
Tier: void-simplification (>= v12)   -- Task-returning void handlers
Tier: notification-publishers (>= v12) -- INotificationPublisher, custom dispatch
```

## Key API Deltas

See [diff-report.md](diff-report.md) for the full analysis. Highlights:

1. **Pipeline behavior constraints** -- Evolved from strict `IRequest<TResponse>` (v10/v11) to relaxed `notnull` (v12), fundamentally changing DI registration patterns
2. **Void handler return type** -- `Task<Unit>` (v10/v11) to `Task` (v12), with `IRequest` decoupled from `IRequest<Unit>` hierarchy
3. **Handler constraint relaxation** -- Every handler and processor interface had its `TRequest` constraint loosened in v12
4. **Streaming constraint evolution** -- `IStreamRequest<TResponse>` constraint (v10/v11) relaxed to `notnull` (v12)
5. **CancellationToken normalization** -- Parameter position standardized to last in v11 pipeline behaviors
6. **Custom notification publishers** -- v12 added `INotificationPublisher` as a new extension point
7. **Package consolidation** -- v12 merged contracts and DI packages into main MediatR package

## How WrapGod Uses This

```
1. Load manifests:  v10/manifest.wrapgod.json
                    v11/manifest.wrapgod.json
                    v12/manifest.wrapgod.json

2. Apply strategy:  strategies/lcd.wrapgod.config.json
                    -- OR --
                    strategies/adaptive.wrapgod.config.json

3. Generate:        IMediatRWrapper interface + implementation
                    with version-appropriate generic constraints
```

The generated wrapper insulates consuming code from version-specific generic signatures. A shared library can safely reference the wrapper whether the consuming project uses MediatR v10 or v12 -- the wrapper emits the correct constraints, return types, and interface hierarchy for the target version.

### Generic-Specific Value

Unlike the Moq example (which focuses on feature additions/removals), MediatR demonstrates WrapGod's handling of **generic parameter drift**: the same interfaces exist across all versions but with different constraints, variance annotations, and type hierarchies. This is the harder problem for source generators because the type structure changes, not just the member list.
