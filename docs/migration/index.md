# Migration

Strategies and automation for migrating codebases between library versions and across libraries.

- [Migration Playbook](playbook.md) — strategy selection, safety model, validation checklist
- [Automation Guide](automation.md) — end-to-end automation for eliminating library tech debt
- [Migration Schema](schema.md) — `WrapGod.Migration` schema model and rule kinds reference
- [Migration Engine](engine.md) — `WrapGod.Migration.Engine` rewrite pipeline contracts (`IRuleRewriter`, `RewriteContext`, `MigrationResult`)
- [Schema Generation](schema-generation.md) — `MigrationSchemaGenerator.FromDiff` — diff → schema mapping, similarity thresholds, deterministic IDs
