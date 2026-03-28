# NuGet Version-Matrix CI Behavior

This document describes deterministic pass/fail behavior for the `nuget-version-matrix` CI job.

## Coverage

The workflow validates all tracked version-divergence examples:

- `fluent-assertions` (`v5`, `v6`, `v8`)
- `moq` (`v4.10`, `v4.16`, `v4.20`)
- `serilog` (`v2`, `v3`, `v4`)

## Deterministic validation gates

For each sample, CI must pass all gates below:

1. **Structure gate**
   - Sample folder exists.
   - Each expected version folder includes `manifest.wrapgod.json`.

2. **Report artifact gate**
   - `compatibility-report.json`, `compatibility-report.md`, and `diff-report.md` must exist.

3. **Schema + content gate**
   - `CompatibilityReportSchemaTests` and `ManifestSchemaTests` pass.
   - `compatibility-report.md` must include package name and all declared versions from `compatibility-report.json`.

4. **Drift guard gate**
   - `diff-report.md` must be non-trivial (length threshold check) to avoid accidentally-empty reports.

5. **Artifact publication gate**
   - CI uploads compatibility report artifacts and manifests for each sample.
   - Missing artifact files fail the run.

## Failure behavior

A run fails immediately for a sample if any required file is missing, schema validation fails, markdown and JSON drift, or artifacts are not publishable.

This ensures repeatable pass/fail behavior and reduces silent regressions in version-matrix examples.
