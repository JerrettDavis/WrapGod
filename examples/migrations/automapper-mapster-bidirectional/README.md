# AutoMapper <-> Mapster Bidirectional Integration Pack

This migration pack demonstrates bidirectional mapping coverage between
AutoMapper-style profile mappings and [Mapster](https://github.com/MapsterMapper/Mapster).

> Note: this sample intentionally avoids taking a direct AutoMapper package dependency
> because current published AutoMapper versions are flagged by dependency review in CI
> (`GHSA-rvv3-g6hj-g44x`). The source-side mapping code mirrors AutoMapper profile semantics
> (`CreateMap` / member mapping style) so migration behavior remains demonstrable.

## Layout

- `AutoMapperApp/` — AutoMapper source/target mapping implementation
- `MapsterApp/` — Mapster source/target mapping implementation
- `ParityTests/` — object-graph parity checks for both directions
- `mapping-matrix.md` — mapping profile/config equivalence table
- `diagnostics.md` — unsupported custom resolver/projection guidance

## Scope covered

- Bidirectional map declarations (domain -> DTO and DTO -> domain)
- Nested object mapping (`Customer`, `Address`)
- Collection mapping (`Lines`/`Items`)
- Nullable field handling (`DiscountCode`, `ShippingFee`, nullable state)
- Runtime profile/config parity between AutoMapper and Mapster

## Run locally

```bash
dotnet test examples/migrations/automapper-mapster-bidirectional/ParityTests/ParityTests.csproj
```
