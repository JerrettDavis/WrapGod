# EF Core <-> Dapper Bidirectional Integration Pack

This migration pack demonstrates pragmatic bidirectional service-boundary migration patterns
between Entity Framework Core and Dapper.

## Layout

- `EfCoreApp/` — EF Core service/repository implementation sample
- `DapperApp/` — Dapper service/repository implementation sample
- `ParityTests/` — CI parity checks for equivalent repository outputs
- `compatibility-matrix.md` — feature mapping matrix and automation boundaries
- `diagnostics.md` — unsupported direct-translation diagnostics + manual refactor guidance
- `migration-checklist.md` — staged coexistence and verification playbook

## Scope covered

- Repository/service boundary wrapper patterns
- Query and command abstraction mapping guidance
- Transaction/unit-of-work behavior differences
- Unsupported direct ORM feature translation guidance

## Run locally

```bash
dotnet test examples/migrations/efcore-dapper-bidirectional/ParityTests/ParityTests.csproj
```
