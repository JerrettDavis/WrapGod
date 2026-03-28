# MediatR <-> MassTransit Mediator Bidirectional Integration Pack

This migration pack demonstrates in-process mediator workflow migration patterns
between MediatR and MassTransit Mediator.

## Layout

- `MediatRApp/` — MediatR request/notification + pipeline behavior sample
- `MassTransitMediatorApp/` — MassTransit Mediator request/notification + consume filter sample
- `ParityTests/` — CI tests for request/notification and pipeline equivalence
- `pipeline-mapping-matrix.md` — behavior/middleware mapping guidance
- `diagnostics.md` — unsupported transport-specific assumptions and guidance

## Scope covered

- Request/response path equivalence
- Notification publishing/handling equivalence
- Pipeline behavior vs middleware/filter equivalence
- Registration/convention mapping notes

## Run locally

```bash
dotnet test examples/migrations/mediatr-masstransit-mediator-bidirectional/ParityTests/ParityTests.csproj
```
