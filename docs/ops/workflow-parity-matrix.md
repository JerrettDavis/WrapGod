# Workflow Parity Matrix

This document maps each adopted workflow and template file to its PatternKit source
and documents any intentional deviations for WrapGod.

## Workflows

| WrapGod File | PatternKit Source | Status | Deviations |
|---|---|---|---|
| `.github/workflows/ci.yml` | Pre-existing | Kept as-is | WrapGod original; not ported from PatternKit |
| `.github/workflows/benchmarks.yml` | Pre-existing | Kept as-is | WrapGod original; no PatternKit equivalent |
| `.github/workflows/examples.yml` | Pre-existing | Kept as-is | WrapGod original; no PatternKit equivalent |
| `.github/workflows/codeql-analysis.yml` | [PatternKit codeql-analysis.yml](https://github.com/JerrettDavis/PatternKit/blob/main/.github/workflows/codeql-analysis.yml) | Ported | Uses `global-json-file` instead of explicit dotnet versions; references `WrapGod.slnx` instead of `PatternKit.slnx`; uses `actions/checkout@v4` and `actions/setup-dotnet@v4` to match existing WrapGod conventions |
| `.github/workflows/dependency-review.yml` | [PatternKit dependency-review.yml](https://github.com/JerrettDavis/PatternKit/blob/main/.github/workflows/dependency-review.yml) | Ported | Direct port, no deviations |
| `.github/workflows/stale.yml` | [PatternKit stale.yml](https://github.com/JerrettDavis/PatternKit/blob/main/.github/workflows/stale.yml) | Ported | Direct port, no deviations |
| `.github/workflows/labeler.yml` | [PatternKit labeler.yml](https://github.com/JerrettDavis/PatternKit/blob/main/.github/workflows/labeler.yml) | Ported | Direct port, no deviations |
| `.github/workflows/pr-validation.yml` | [PatternKit pr-validation.yml](https://github.com/JerrettDavis/PatternKit/blob/main/.github/workflows/pr-validation.yml) | Ported | Removed GitVersion/versioning steps; removed docfx build step; removed NuGet dry-run packaging; removed PR comment step; uses `global-json-file` for .NET setup; references `WrapGod.slnx` |
| `.github/workflows/docs.yml` | [PatternKit docs.yml](https://github.com/JerrettDavis/PatternKit/blob/main/.github/workflows/docs.yml) | Ported | Added `paths` filter for docs changes only; uses `global-json-file`; added `continue-on-error` on docfx steps (no `docfx.json` yet); removed explicit project build steps |

## Label Configuration

| WrapGod File | PatternKit Source | Status | Deviations |
|---|---|---|---|
| `.github/labeler.yml` | [PatternKit labeler.yml](https://github.com/JerrettDavis/PatternKit/blob/main/.github/labeler.yml) | Ported | Labels adapted to WrapGod project structure (Abstractions, Analyzers, CLI, Extractor, Fluent, Generator, Manifest, Runtime, TypeMap, Benchmarks, Schemas) instead of PatternKit components |

## Templates

| WrapGod File | PatternKit Source | Status | Deviations |
|---|---|---|---|
| `.github/PULL_REQUEST_TEMPLATE.md` | [PatternKit PULL_REQUEST_TEMPLATE.md](https://github.com/JerrettDavis/PatternKit/blob/main/.github/PULL_REQUEST_TEMPLATE.md) | Ported | Direct port, no deviations |
| `.github/ISSUE_TEMPLATE/bug_report.yml` | [PatternKit bug_report.yml](https://github.com/JerrettDavis/PatternKit/blob/main/.github/ISSUE_TEMPLATE/bug_report.yml) | Ported | Component dropdown updated to WrapGod components; code sample placeholder updated to WrapGod API examples |
| `.github/ISSUE_TEMPLATE/feature_request.yml` | [PatternKit feature_request.yml](https://github.com/JerrettDavis/PatternKit/blob/main/.github/ISSUE_TEMPLATE/feature_request.yml) | Ported | Component dropdown updated to WrapGod components; code example placeholder updated to WrapGod API examples |

## Validation Checklist

- [ ] `ci.yml` -- existing, no changes needed
- [ ] `benchmarks.yml` -- existing, no changes needed
- [ ] `examples.yml` -- existing, no changes needed
- [ ] `codeql-analysis.yml` -- runs on push/PR to main + weekly schedule
- [ ] `dependency-review.yml` -- runs on PRs to main
- [ ] `stale.yml` -- runs daily + manual dispatch
- [ ] `labeler.yml` -- runs on PR/issue open/sync/reopen
- [ ] `pr-validation.yml` -- runs on PRs to main (non-docs paths)
- [ ] `docs.yml` -- runs on push to main when docs change
- [ ] PR template renders correctly
- [ ] Bug report template renders correctly
- [ ] Feature request template renders correctly
