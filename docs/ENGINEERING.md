# Engineering Baseline

## SDK and Targeting

- SDK is pinned via `global.json` (`10.0.104`)
- Runtime projects target `net10.0`
- Shared contracts/generator/analyzer components target `netstandard2.0` for broad host compatibility

## Build Determinism

Configured in `Directory.Build.props`:

- `Deterministic=true`
- `ContinuousIntegrationBuild=true` when running in CI
- `EmbedUntrackedSources=true`

## Warning/Style Policy

- Code style is enforced in build (`EnforceCodeStyleInBuild=true`)
- In CI, warnings are promoted to errors (`TreatWarningsAsErrors=true` only when `CI=true`)
- Analyzer category severities are defined in `.editorconfig`

## Versioning Strategy

- Repository-level pre-release baseline:
  - `VersionPrefix=0.1.0`
  - `VersionSuffix=alpha`
- This gives consistent package/application version stamping while MVP is under active development.
