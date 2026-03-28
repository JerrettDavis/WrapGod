# Doctor Command (`wrap-god doctor`)

`wrap-god doctor` validates setup, source/lockfile health, and CI readiness.

## Checks
- SDK/tooling prerequisites (`dotnet --version`, `global.json`)
- Source/config and lockfile state (`*.wrapgod.json`, `wrapgod.lock.json`)
- CI/workflow readiness (`.github/workflows`, `ci.yml`)

## Output modes
- `--format text` (default)
- `--format json` (CI-friendly)
- `--format sarif`

## Exit codes
- `0`: success
- `1`: runtime failure
- `2`: effective error diagnostics
- `3`: warnings promoted to failures with `--warnings-as-errors`

## Dependency notes
Doctor emits dependency-tagged warnings while foundational work lands:
- `dependency:#123` for source discovery precedence
- `dependency:#124` for lockfile lifecycle
