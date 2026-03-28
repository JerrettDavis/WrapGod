# MediatR Version-Matrix Diff Report

API delta analysis across MediatR v10 -> v11 -> v12, focused on generic parameter changes.

## Delta 1: Pipeline Behavior Generic Constraint Relaxation

| Aspect | v10 | v11 | v12 |
|--------|-----|-----|-----|
| `IPipelineBehavior<TRequest, TResponse>` constraint | `where TRequest : IRequest<TResponse>` | Same as v10 | `where TRequest : notnull` |
| `IStreamPipelineBehavior<TRequest, TResponse>` constraint | `where TRequest : IStreamRequest<TResponse>` | Same as v10 | `where TRequest : notnull` |
| Open-generic pipeline registration | Blocked (constraint too strict) | Blocked | Enabled |

**Migration safety:** `breaking`

This is the most impactful generic delta. In v10/v11, the tight coupling `where TRequest : IRequest<TResponse>` prevented common open-generic pipeline registration patterns:

```csharp
// v10/v11: FAILS -- TRequest cannot satisfy IRequest<TResponse> in open-generic context
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

// v12: WORKS -- notnull constraint allows open-generic registration
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

**Guidance:** For LCD, wrap pipeline behaviors with the strict `IRequest<TResponse>` constraint. For v12-targeted wrappers, use `notnull` to enable flexible DI registration. Code written for v10/v11 constraints still compiles on v12, but not the reverse.

---

## Delta 2: Void Handler Return Type and IRequest Hierarchy

| Aspect | v10 | v11 | v12 |
|--------|-----|-----|-----|
| `IRequest` inherits | `IRequest<Unit>` | `IRequest<Unit>` | `IBaseRequest` (directly) |
| `IRequestHandler<TRequest>` inherits | `IRequestHandler<TRequest, Unit>` | `IRequestHandler<TRequest, Unit>` | Standalone (no inheritance) |
| Void handler return type | `Task<Unit>` | `Task<Unit>` | `Task` |
| Void handler return idiom | `return Unit.Value;` / `return Unit.Task;` | Same | `return;` / `return Task.CompletedTask;` |
| `IRequestHandler<TRequest>` constraint | `where TRequest : IRequest<Unit>` | `where TRequest : IRequest<Unit>` | `where TRequest : IRequest` |

**Migration safety:** `breaking`

This is a two-part generic hierarchy change. First, `IRequest` no longer extends `IRequest<Unit>`, breaking any code that treats void requests as `IRequest<Unit>`. Second, the single-generic `IRequestHandler<TRequest>` changes its return type from `Task<Unit>` to `Task`, requiring handler implementations to be updated.

```csharp
// v10/v11: Unit-based void handler
public class DeleteUserHandler : IRequestHandler<DeleteUserCommand>
{
    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken ct)
    {
        // ...
        return Unit.Value;
    }
}

// v12: Task-based void handler
public class DeleteUserHandler : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken ct)
    {
        // ...
    }
}
```

**Guidance:** LCD normalizes to `Task<Unit>` (v10/v11 style) since Unit still exists in v12 for backward compatibility. Targeted v12 uses `Task` directly. Adaptive wrappers gate with `MEDIATR_V12`.

---

## Delta 3: Request Handler Generic Constraint Changes

| Aspect | v10 | v11 | v12 |
|--------|-----|-----|-----|
| `IRequestHandler<TRequest, TResponse>` constraint on TRequest | `IRequest<TResponse>` | `IRequest<TResponse>` | `notnull` |
| `IStreamRequestHandler<TRequest, TResponse>` constraint on TRequest | `IStreamRequest<TResponse>` | `IStreamRequest<TResponse>` | `notnull` |
| `IRequestPreProcessor<TRequest>` constraint on TRequest | `IBaseRequest` | `IBaseRequest` | `notnull` |
| `IRequestPostProcessor<TRequest, TResponse>` constraint on TRequest | `IBaseRequest` | `IBaseRequest` | `notnull` |

**Migration safety:** `review`

The constraint relaxation across ALL handler and processor interfaces in v12 is a consistent pattern -- every `TRequest` constraint was loosened. While this enables more flexible patterns, it means v12-written code that relies on the looser constraints will not compile against v10/v11.

**Guidance:** Wrappers that must support v10/v11 must emit the stricter constraints. The strict constraints are a subset of `notnull` (anything satisfying `IRequest<TResponse>` also satisfies `notnull`), so strict-constrained code compiles on v12.

---

## Delta 4: Streaming Support Introduction (v10) and Constraint Evolution

| Aspect | v10 | v11 | v12 |
|--------|-----|-----|-----|
| `IStreamRequest<TResponse>` | NEW -- covariant `out TResponse` | Unchanged | Unchanged |
| `IStreamRequestHandler<TRequest, TResponse>` | NEW -- `where TRequest : IStreamRequest<TResponse>` | Unchanged | `where TRequest : notnull` |
| `IStreamPipelineBehavior<TRequest, TResponse>` | NEW -- `where TRequest : IStreamRequest<TResponse>` | CancellationToken moved to last param | `where TRequest : notnull` |
| `StreamHandlerDelegate<TResponse>` | NEW | Unchanged | Unchanged |
| `ISender.CreateStream` | NEW | Unchanged | Unchanged |

**Migration safety:** `review`

Streaming was introduced in v10 as a complete subsystem. The generic variance pattern (`out TResponse` on `IStreamRequest<TResponse>`) enables covariant stream results. In v12, the strict `IStreamRequest<TResponse>` constraint was relaxed to `notnull`, matching the pattern applied to all other handler interfaces.

**Guidance:** LCD includes streaming types with strict constraints (since they exist across all three versions). The covariant `out TResponse` on `IStreamRequest<TResponse>` is unchanged across versions and can be safely exposed in all strategies.

---

## Delta 5: CancellationToken Parameter Position (v11)

| Aspect | v10 | v11 | v12 |
|--------|-----|-----|-----|
| `IPipelineBehavior.Handle` param order | `(TRequest, RequestHandlerDelegate<TResponse>, CancellationToken)` | Same (finalized) | Same |
| `IStreamPipelineBehavior.Handle` param order | `(TRequest, StreamHandlerDelegate<TResponse>, CancellationToken)` | CancellationToken moved to last | Same |
| Design rule alignment | Partial CA1068 compliance | Full CA1068 compliance | Same |

**Migration safety:** `breaking`

v11 enforced C# design guideline CA1068 (CancellationToken should be last parameter) across pipeline behavior interfaces. This is a signature-level breaking change for any custom pipeline behavior implementations.

**Guidance:** All versions in the matrix (v10+) have CancellationToken as the last parameter for `IPipelineBehavior`. The change primarily affected `IStreamPipelineBehavior` and internal processor behaviors between v10 and v11. LCD wrappers use the v11+ parameter order since it is the most widely compatible.

---

## Delta 6: Custom Notification Publisher (v12)

| Aspect | v10 | v11 | v12 |
|--------|-----|-----|-----|
| `INotificationPublisher` | Not available | Not available | NEW |
| `NotificationHandlerExecutor` | Not available | Not available | NEW |
| Custom dispatch strategies | Not possible | Not possible | Parallel, sequential, fire-and-forget |

**Migration safety:** `safe`

Purely additive. `INotificationPublisher` is a new extension point in v12 that allows custom notification dispatch strategies. It does not change existing interfaces -- code that does not use it is unaffected.

**Guidance:** LCD excludes these types. Targeted v12 and adaptive strategies (with `MEDIATR_V12` symbol) include them for advanced notification dispatch scenarios.

---

## Delta 7: Contracts Package Consolidation

| Aspect | v10 | v11 | v12 |
|--------|-----|-----|-----|
| Contract interfaces location | `MediatR.Contracts` (separate NuGet) | `MediatR.Contracts` (separate NuGet) | `MediatR` (consolidated) |
| DI extension package | `MediatR.Extensions.Microsoft.DependencyInjection` (separate) | Same | `MediatR` (consolidated) |
| `AddMediatR` registration | Extension method with assemblies parameter | Same | Single `MediatRServiceConfiguration` object |

**Migration safety:** `review`

v12 consolidated the contracts and DI extension packages into the main `MediatR` package. The generic interfaces themselves are unchanged, but their assembly location changed, which affects `typeof()` references and assembly scanning in DI registration.

**Guidance:** WrapGod manifests reference types by fully-qualified name, which is unaffected by package consolidation. However, DI registration patterns in wrapper consumers need version-specific configuration code.

---

## Summary Matrix

| Feature | v10 | v11 | v12 | LCD (v10+) | Targeted v12 | Adaptive |
|---------|-----|-----|-----|------------|--------------|----------|
| `IRequest<Unit>` hierarchy | Yes | Yes | No (IBaseRequest) | v10/v11 style | v12 style | `MEDIATR_V12` gate |
| Void handler returns `Task<Unit>` | Yes | Yes | No (returns `Task`) | `Task<Unit>` | `Task` | `MEDIATR_V12` gate |
| Pipeline `where TRequest : IRequest<TResponse>` | Yes | Yes | No (`notnull`) | Strict | Relaxed | `MEDIATR_V12` gate |
| Stream handler `where TRequest : IStreamRequest<TResponse>` | Yes | Yes | No (`notnull`) | Strict | Relaxed | `MEDIATR_V12` gate |
| Pre/post processor `where TRequest : IBaseRequest` | Yes | Yes | No (`notnull`) | Strict | Relaxed | `MEDIATR_V12` gate |
| `IStreamRequest<TResponse>` (covariant) | Yes | Yes | Yes | Included | Included | All tiers |
| `INotificationPublisher` | No | No | Yes | Excluded | Included | `MEDIATR_V12` gate |
| CancellationToken last in pipeline | Yes | Yes (enforced) | Yes | Included | Included | All tiers |
| Contracts in main package | No | No | Yes | N/A (type names unchanged) | N/A | N/A |
