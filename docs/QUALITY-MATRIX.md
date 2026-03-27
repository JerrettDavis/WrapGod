# Quality Matrix (Issue #15)

This matrix tracks required confidence areas for WrapGod.

## Domains

- Manifest schema validation
- Extractor deterministic output
- Config ingestion/merge precedence
- Generator output snapshots
- Analyzer diagnostics + code fixes
- End-to-end pipeline behavior

## Matrix

| Domain | Current Coverage | Target |
|---|---|---|
| Manifest schema | Basic valid/invalid fixtures | Expanded fixture set (version drift + edge signatures) |
| Extractor | Unit tests + multi-version diffs | Golden manifests across fixture versions |
| Config merge | JSON/attributes precedence tests | Conflict matrix across all directives |
| Generator | Smoke + expansion tests | Snapshot baselines by compatibility mode |
| Analyzer/CodeFix | WG2001/WG2002 + safe rewrite tests | Broader semantic safety + FixAll matrix |
| E2E | Core pipeline scenarios | Multiple third-party fixture packs |

## Next additions in this issue

1. Add golden manifest fixtures for regression lock
2. Add generator snapshot baselines
3. Add analyzer/code-fix regression matrix test set
4. Add CI gate that runs full matrix suite
