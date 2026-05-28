# WrapGod Migration Engine — Executive Summary

**Scope:** Close all 17 open issues. The full mechanical plan is in `MIGRATION-ENGINE-PLAN.md`.

## Order at a glance

1. #193 — Schema generator from `VersionDiff`
2. #194 — `WrapGod.Migration.Engine` scaffold + `IRuleRewriter`
3. #195 — A-level syntax rewriters (7 rewriters)
4. #198 — CLI `migrate generate` *(parallel with #195)*
5. #196 — `MigrationEngine` orchestrator (Apply + DryRun)
6. #202 — B-level structural rewriters (4 rewriters) *(parallel with #197)*
7. #197 — State tracking, idempotent re-runs, schema hash
8. #199 — CLI `migrate apply --dry-run`
9. #200 — CLI `migrate status [--json]` *(parallel with #199)*
10. #201 — CLI `migrate verify` (±3 lines attribution)
11. #203 — E2E example: Serilog v2 → v3
12. #204 — Docs: authoring + applying + schema + CLI + README
13. #191 — Epic close (auto when #193–#204 done)
14. #155 — Bidirectional packs epic — scope-narrow close (children shipped)
15. #163 — NuGet version-matrix epic — scope-narrow close (children shipped)
16. #1   — MVP master plan — close (sub-issues all closed)
17. #53  — IDE RFC — close as deferred to vNext

## Critical path

```
#193 → #196 → #197 → #199 → #201 → #203 → #204 → #191
```

Eight sequential issues. Everything else parallelises off this trunk.

## T-shirt time estimates

| Issue | Size | Notes |
|---|---|---|
| #193 | M | Pure algorithm + tests; similarity heuristics need care |
| #194 | S | Contracts only; small surface |
| #195 | XL | Seven rewriters × ~6 tests each + integration |
| #196 | L | Orchestration + I/O + perf test |
| #197 | M | State serialiser + idempotence + hash logic |
| #198 | M | CLI plumbing; reuse existing patterns |
| #199 | M | CLI + glob filter + integration with state |
| #200 | S | Read-only CLI; small |
| #201 | L | Build runner + diagnostic parser + attribution + graceful degradation |
| #202 | L | Four structural rewriters; complex edge cases |
| #203 | M | Real example + CI parity test; fixture authoring is the bulk |
| #204 | M | Docs across ~9 pages + small test class |
| #191 | XS | Single close comment |
| #1   | XS | Single close comment |
| #53  | XS | Single close comment |
| #155 | XS | Single close comment |
| #163 | XS | Single close comment |

Sizing key: XS = ≤1 hour, S = ~½ day, M = 1–2 days, L = 3–4 days, XL = ≥1 week.

Total bottom-up estimate (sequential one developer): ~6–8 weeks. With one developer hitting the parallel branches (e.g., #194/#193 same week; #195/#198 same week; #197/#202 same week; #199/#200 same week), ~3–4 weeks wall-clock.

## Parallelism opportunities

| After this lands | Run these in parallel |
|---|---|
| #192 (already done) | #193 + #194 |
| #194 | #195 |
| #195 | #196 |
| #196 | #197 + #202 |
| #197 | #199 + #200 |
| #199 | #201 |
| #201 + #202 | #203 |
| #203 | #204 |
| #204 | All five epic closures (#191, #1, #53, #155, #163) |

## Epic closure path

| Epic | Path | Trigger |
|---|---|---|
| **#191** Migration engine | Auto-close when last child (anywhere in #193–#204) merges. Draft close comment ready. | Last child closes. |
| **#1** MVP master plan | Scope-narrow close — every listed child (#68–#71, #73–#74, #76, #81–#83) already closed. | Bundle with #191. |
| **#53** IDE RFC | Deferred-close — all three RFC prerequisites now met; future IDE work files fresh epics. | Bundle with #191. |
| **#155** Bidirectional wrappers | Scope-narrow close — all P0/P1 sub-issues (#156–#162) shipped; deliverables in `examples/migrations/*-bidirectional/`. | Bundle with #191. |
| **#163** NuGet version-matrix | Scope-narrow close — all sub-issues (#164–#169) shipped; deliverables in `examples/migrations/nuget-version-matrix/`. | Bundle with #191. |

All five epic closures fire as a single batch on the day the engine lands.

## Risk highlights

- **Trivia preservation** is the #1 bug source — mandate per-rewriter trivia tests.
- **`MigrateInitCommand` already owns the `migrate` parent command** — extract `init` to a subcommand and build a fresh `MigrateCommandBuilder` in #198.
- **Coverage gate is 90% per package** — ship contract tests in the same PR as scaffolds.
- **Roslyn version drift** — pin via centralised MSBuild property in `Directory.Build.props`.

## Definition of done

All 17 issues closed; `main` green; README + docs updated; Serilog v2→v3 example shipping with CI parity; v0.2-alpha tag cut.

---
*See `MIGRATION-ENGINE-PLAN.md` for the per-issue blueprints.*