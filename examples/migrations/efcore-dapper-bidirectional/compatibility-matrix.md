# EF Core <-> Dapper Feature Compatibility Matrix

| Concern | EF Core Pattern | Dapper Pattern | Direction | Safety | Notes |
|---|---|---|---|---|---|
| Repository abstraction | `DbContext` + repository service | `IDbConnection` + repository service | both | safe | Keep service contract stable during migration |
| Query composition | LINQ query translation | SQL query strings | both | review | Complex LINQ requires manual SQL validation |
| Write operations | `Add/Update` + `SaveChanges` | `INSERT/UPDATE` via parameterized SQL | both | safe | Ensure consistent key/value conventions |
| Change tracking | automatic state tracking | manual diff/update intent | EF->Dapper | manual | Manual state management required in Dapper |
| Unit of work | implicit context scope | explicit transaction scope | both | review | Co-locate commit boundaries in app layer |
| Raw SQL | `FromSql` / `ExecuteSql` | native primary path | both | safe | Parameterization must remain enforced |
| Migrations | EF migrations | external migration tooling/scripts | EF->Dapper | manual | Not directly automatable |
| Projection materialization | mapped entities + DTO projection | direct row-to-DTO mapping | both | safe | DTO parity tests should gate behavior |
