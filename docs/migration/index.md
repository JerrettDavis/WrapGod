# Migration

Strategies and automation for migrating codebases between library versions and across libraries.

- [Migration Playbook](playbook.md) — strategy selection, safety model, validation checklist
- [Automation Guide](automation.md) — end-to-end automation for eliminating library tech debt
- [Migration Schema](schema.md) — `WrapGod.Migration` schema model and rule kinds reference
- [Migration Engine](engine.md) — `WrapGod.Migration.Engine` rewrite pipeline contracts (`IRuleRewriter`, `RewriteContext`, `MigrationResult`)
- [Schema Generation](schema-generation.md) — `MigrationSchemaGenerator.FromDiff` — diff → schema mapping, similarity thresholds, deterministic IDs
- [A-Level Rewriters](rewriters.md) — Seven concrete `IRuleRewriter` implementations: `RenameType`, `RenameNamespace`, `RenameMember`, `ChangeParameter`, `RemoveMember`, `AddRequiredParameter`, `ChangeTypeReference`
- [Migration State](state.md) — `MigrationState`, `MigrationStateStore`, `StatefulMigrationEngine`: state file location, SHA-256 schema hash, idempotent re-runs, corruption recovery
- [Applying Migrations](applying.md) — `wrap-god migrate apply` consumer workflow: dry-run, glob filtering, idempotence, state management
- [Examples](examples.md) — End-to-end, CI-validated examples (Serilog v2 → v3 upgrade walkthrough + bidirectional comparisons)
