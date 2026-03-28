# Diagnostics and Manual Migration Guidance

Automated migration should flag the following patterns for manual review.

## Unsupported or risky patterns

1. **Custom value resolvers / converters with side effects**
   - AutoMapper `IValueResolver`, `ITypeConverter`
   - Mapster custom adapters / `MapWith`
   - Recommendation: emit manual-review diagnostic (custom resolver parity validation required).

2. **IQueryable projection semantics**
   - AutoMapper `ProjectTo<T>()` and Mapster `ProjectToType<T>()` may differ by LINQ provider translation.
   - Recommendation: emit review diagnostic for server-side query verification.

3. **Conditional mapping and before/after hooks**
   - AutoMapper `BeforeMap` / `AfterMap` hooks do not always have direct Mapster equivalents.
   - Recommendation: keep explicit manual path and parity tests for side-effectful hooks.

## Manual path checklist

- Inventory custom profiles/configs (resolvers, converters, conditions)
- Port baseline maps first (simple + nested + collections)
- Add focused parity tests for custom resolvers/projections
- Validate null handling and constructor mapping behavior in both directions
- Verify query translations against real DB provider when projections are used
