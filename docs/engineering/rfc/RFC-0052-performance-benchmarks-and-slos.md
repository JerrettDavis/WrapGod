# RFC-0052: Performance Benchmarks and SLOs

- **Status:** Proposed
- **Owner:** @JerrettDavis/agent-jarvis
- **Issue:** #52
- **Last Updated:** 2026-03-27

## Summary

This RFC defines the initial benchmark matrix, measurement methodology, SLO targets, and CI rollout plan for WrapGod performance across:

- `WrapGod.Extractor`
- `WrapGod.Generator`
- `WrapGod.Analyzers`

Goal: establish stable, repeatable, decision-useful performance signals that can detect regressions before release without creating noisy PR friction.

## Scope

### In scope

- Benchmark scenarios and fixture classes (small / medium / large)
- SLO targets by operation and fixture tier
- Measurement methodology and noise controls
- CI execution cadence and phased rollout
- Regression policy and reporting format

### Out of scope

- Hard fail gates on every pull request in phase 1
- Runtime production telemetry
- Micro-optimization of individual methods

## Terminology

- **Fixture tier:** workload class (small / medium / large)
- **Cold run:** process start + first invocation (includes warmup effects)
- **Steady-state run:** repeated invocation after warmup
- **P95:** 95th percentile wall-clock duration for a scenario within one run set

## Benchmark Harness Decision

**Decision:** use a two-part harness:

1. **BenchmarkDotNet project** for reproducible local and CI micro/mid benchmarks
2. **Scenario runner** for end-to-end workflow timings on realistic fixture repositories

Rationale:

- BenchmarkDotNet provides robust statistics, iteration control, and environment capture
- Scenario runner reflects real user workflows (`extract`, `generate`, `analyze`, `fix-all`)
- Combined approach balances precision and product-level relevance

## Benchmark Matrix (Initial)

### Fixtures

| Tier | Approx API Surface | Approx Projects | Typical Use |
|---|---:|---:|---|
| Small | 50-200 public symbols | 1 | smoke and fast signal |
| Medium | 1,000-5,000 public symbols | 3-8 | normal day-to-day SDKs |
| Large | 20,000-100,000+ public symbols | 10-40 | stress and release confidence |

> Exact fixture repos and lockfile commit SHAs are versioned with the harness.

### Scenarios

| Area | Scenario ID | Description | Mode |
|---|---|---|---|
| Extractor | EXTRACT-COLD | first extract on clean temp dir | cold |
| Extractor | EXTRACT-STEADY | repeated extract on same fixture | steady-state |
| Generator | GENERATE-COLD | first generation from manifest | cold |
| Generator | GENERATE-INCR | generation after 5% manifest delta | incremental |
| Analyzers | ANALYZE-SCAN | baseline diagnostics scan | steady-state |
| Analyzers | ANALYZE-FIXALL | apply all eligible fixes | steady-state |

## SLO Targets (Initial)

Targets are conservative enough for CI stability while still meaningful for MVP quality.

### Latency Targets (P95 wall-clock)

| Scenario | Small | Medium | Large |
|---|---:|---:|---:|
| EXTRACT-COLD | <= 2s | <= 8s | <= 30s |
| EXTRACT-STEADY | <= 1s | <= 5s | <= 20s |
| GENERATE-COLD | <= 2s | <= 10s | <= 40s |
| GENERATE-INCR | <= 1s | <= 4s | <= 15s |
| ANALYZE-SCAN | <= 4s | <= 15s | <= 60s |
| ANALYZE-FIXALL | <= 5s | <= 20s | <= 75s |

### Resource Targets

| Metric | Small | Medium | Large |
|---|---:|---:|---:|
| Peak managed memory (extract/generate) | <= 200 MB | <= 500 MB | <= 1.5 GB |
| Peak managed memory (analyze/fix-all) | <= 250 MB | <= 700 MB | <= 2.0 GB |

## Methodology

1. Pin benchmark host image, SDK version, and power profile in CI.
2. For each fixture/scenario:
   - 1 pilot run (discarded)
   - 5 measured runs (record)
3. Compute median and P95 from measured runs.
4. Compare against:
   - absolute SLO threshold
   - rolling baseline (main branch median over last 14 successful runs)
5. Emit machine-readable results (`json`) + Markdown summary artifact.

## Noise Controls

To reduce false positives:

- Run on dedicated CI runners (or pinned runner labels)
- Disable turbo/CPU scaling where possible; otherwise track CPU frequency metadata
- No parallel benchmark jobs sharing host resources
- Warm filesystem cache for steady-state scenarios only
- Retry-once policy for suspected infra outliers (only if host telemetry indicates contention)
- Report coefficient of variation (CV); mark CV > 0.15 as low-confidence

## Regression Policy

A benchmark result is flagged as regression if either condition holds:

1. **SLO breach**: P95 exceeds scenario tier target
2. **Baseline drift**: median degrades by >15% versus rolling baseline on two consecutive scheduled runs

Actions:

- PRs: non-blocking warning status + artifact link (phase 1/2)
- Nightly/release: blocking status for sustained regressions (phase 3)
- Any sustained regression requires:
  - issue with scenario/tier details
  - owner assignment
  - mitigation plan (optimize, rebaseline with rationale, or adjust fixture)

## CI Rollout Plan

### Phase 1 (observe)

- Cadence: nightly on `main`
- Output: benchmark artifact + summary comment
- Enforcement: none (informational only)

### Phase 2 (advisory)

- Cadence: nightly + release branch + manual dispatch
- Output: PR check in warning mode for touched areas
- Enforcement: soft (does not block merge)

### Phase 3 (guardrail)

- Cadence: nightly + release + selective PR paths
- Output: required check for release branches
- Enforcement: block only on sustained regressions or hard SLO breaches in release lanes

## Reporting Format

Each run publishes:

- Benchmark environment block (OS, CPU, RAM, .NET SDK)
- Scenario/tier table with median, P95, peak memory, CV
- Delta versus rolling baseline
- Regression flags and confidence rating
- Link to raw JSON artifact

## Follow-up Implementation Issues

- #73 - Benchmark harness implementation (BenchmarkDotNet + scenario runner)
- #74 - CI benchmark workflow and phased gating strategy

## Open Questions

- Should large-tier fixtures include one generated “worst-case generic explosion” synthetic dataset?
- Should analyzer FIXALL have separate targets for IDE-like partial solution load vs full solution load?

## Decision

Approve this RFC as the baseline policy for performance measurement in WrapGod MVP and iterate thresholds after 2-3 weeks of nightly data.
