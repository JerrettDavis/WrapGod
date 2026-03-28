# Doctor Command (`wrap-god doctor`)

`wrap-god doctor` validates setup/tooling, source+lockfile health, and CI workflow readiness.

## Output modes

- `--format text` (default)
- `--format json` (CI-friendly)
- `--format sarif` (code scanning)

## Dependency notes

Until foundational work is complete, doctor emits actionable warnings linked to:

- #123 (convention-first source discovery)
- #124 (`wrapgod.lock.json` lockfile lifecycle)
