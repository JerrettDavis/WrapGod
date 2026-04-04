# AutoMapper <-> Mapster Bidirectional Integration Pack

This migration pack demonstrates bidirectional mapping coverage between
[AutoMapper](https://github.com/LuckyPennySoftware/AutoMapper) profile mappings and
[Mapster](https://github.com/MapsterMapper/Mapster), using both frameworks directly.

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
