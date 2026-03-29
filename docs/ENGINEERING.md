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

## Project Structure

| Project | TFM | Role |
|---|---|---|
| `WrapGod.Abstractions` | `netstandard2.0` | Shared attributes and config model types |
| `WrapGod.Manifest` | `netstandard2.0` | Manifest models, serialization, config loading/merging |
| `WrapGod.Extractor` | `net10.0` | Reflection-only API surface extraction via `MetadataLoadContext` |
| `WrapGod.TypeMap` | `netstandard2.0` | Type mapping plan and mapper source emitter |
| `WrapGod.Fluent` | `net10.0` | Fluent DSL for programmatic wrapper configuration |
| `WrapGod.Generator` | `netstandard2.0` | Roslyn incremental source generator |
| `WrapGod.Analyzers` | `netstandard2.0` | Roslyn analyzer and code-fix provider |
| `WrapGod.Runtime` | `netstandard2.0` | Runtime helpers for generated code |
| `WrapGod.Tests` | `net10.0` | Unit and integration tests |

Generator, analyzer, and shared contract projects target `netstandard2.0` to be
loadable by any Roslyn host. The extractor and CLI/benchmark apps target
`net10.0`; the runtime helper library remains `netstandard2.0` for broad reuse.

## Contribution Guidelines

### Branching

- Feature branches: `feat/<short-description>` or `feat/<issue-number>-<description>`
- Bug fixes: `fix/<short-description>`
- Prefer pull requests to land changes on `main`. Direct commits are acceptable
  for small, low-risk momentum changes.

### Commit Messages

Use conventional commit style when practical:

```
feat(generator): add adaptive mode version guards
fix(extractor): handle ReflectionTypeLoadException gracefully
docs: expand architecture documentation
```

### Tests

- All new behavior should have corresponding tests in `WrapGod.Tests`.
- Run `dotnet test` from the repository root before pushing.
- Tests are organized by component: `ExtractorTests`, `GeneratorTests`,
  `AnalyzerTests`, `ConfigIngestionTests`, `TypeMappingTests`, etc.

### Code Style

- Code style is enforced via `.editorconfig` and `EnforceCodeStyleInBuild`.
- In CI, all warnings are promoted to errors (`TreatWarningsAsErrors=true`).
- Run `dotnet build` locally and resolve any warnings before pushing.

### Adding a New Component

1. Create the project targeting the appropriate TFM (see table above).
2. Reference `WrapGod.Abstractions` for shared types.
3. Add the project to the solution file.
4. Add corresponding test coverage in `WrapGod.Tests`.
