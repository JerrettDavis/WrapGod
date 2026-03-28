# MediatR <-> MassTransit Mediator Pipeline Mapping Matrix

| Concern | MediatR Pattern | MassTransit Mediator Pattern | Direction | Safety | Notes |
|---|---|---|---|---|---|
| Request/response | `IRequest<T>` + `IRequestHandler<TReq,TRes>` | request contract + `IConsumer<TReq>` + `RespondAsync` | both | safe | Keep DTO contracts stable |
| Notifications | `INotification` + `INotificationHandler<T>` | `Publish` + `IConsumer<T>` | both | safe | Both support fan-out semantics |
| Pipeline behavior | `IPipelineBehavior<TReq,TRes>` | consume/send filters (`IFilter<ConsumeContext<T>>`) | both | review | Ordering and scope must be verified |
| Registration | `AddMediatR(RegisterServicesFromAssembly...)` | `AddMediator(cfg => cfg.AddConsumer(...))` | both | safe | Explicit registration helps deterministic behavior |
| Cross-cutting concerns | behaviors (validation/logging) | middleware/filters (validation/logging) | both | review | Translate behavior order intentionally |
| Transport assumptions | generally in-process only | can share concepts with transport bus | MT->MediatR | manual | Remove/rewrite transport-only policies |
