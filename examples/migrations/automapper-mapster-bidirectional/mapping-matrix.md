# AutoMapper <-> Mapster Mapping Matrix

| Concern | AutoMapper Pattern | Mapster Pattern | Direction | Safety | Notes |
|---|---|---|---|---|---|
| Type map registration | `CreateMap<TSource,TDest>()` | `NewConfig<TSource,TDest>()` | both | safe | Direct profile/config equivalent |
| Reverse map | `CreateMap<A,B>(); CreateMap<B,A>()` | `NewConfig<A,B>(); NewConfig<B,A>()` | both | safe | Keep explicit reverse maps for clarity |
| Constructor mapping | `.ConstructUsing(...)` | `.MapToConstructor(true)` | both | review | Confirm constructor parameter ordering |
| Nested object map | `.ForMember(... MapFrom(...))` | `.Map(..., s => s.Nested.Prop)` | both | safe | Same outcome when null behavior is explicit |
| Collection mapping | list map via child type maps | list map via child type maps | both | safe | Item maps must be declared first |
| Computed field | `.ForMember(d => d.Total, MapFrom(...))` | `.Map(d => d.Total, s => ...)` | both | safe | Keep deterministic arithmetic in both mappings |
| Nullables | implicit + explicit assignments | explicit `.Map(...)` where needed | both | safe | Preserve null for optional fields |
| Custom resolver | `IValueResolver` / `ITypeConverter` | `MapWith` / custom adapters | both | manual | Requires manual migration review and tests |
| Projection to IQueryable | `ProjectTo<T>()` | `ProjectToType<T>()` | both | review | Query-provider translation can diverge |
