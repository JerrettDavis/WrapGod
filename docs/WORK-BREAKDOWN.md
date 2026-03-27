# WrapGod Work Breakdown (Initial)

## Track A - Foundation

### A1. Repo hygiene
- [ ] Add `Directory.Build.props` (nullable, analyzers, LangVersion)
- [ ] Add `editorconfig`
- [ ] Add CI baseline workflow

### A2. Core contracts
- [ ] Define namespace conventions
- [ ] Add key abstractions and marker interfaces
- [ ] Add diagnostic ID registry

## Track B - Manifest & Extractor

### B1. Manifest schema v1
- [ ] Draft JSON schema + examples
- [ ] Add schema tests and validation helpers

### B2. Extractor MVP
- [ ] Load one assembly and collect public types/members
- [ ] Emit stable IDs
- [ ] Persist `wrapgod.manifest.json`

### B3. Multi-version diff
- [ ] Compare N versions
- [ ] Mark introduced/removed/changed members
- [ ] Emit compatibility report

## Track C - Planner & Config

### C1. Config ingestion
- [ ] JSON ingestion
- [ ] Attribute ingestion
- [ ] Fluent DSL ingestion (preview)

### C2. Merge engine
- [ ] Precedence rules
- [ ] Conflict diagnostics
- [ ] Emit normalized `VersionedGenerationPlan`

## Track D - Generator

### D1. Generator skeleton
- [ ] Set up incremental pipeline
- [ ] Parse manifest/config additional files
- [ ] Generate one sample interface/facade pair

### D2. Wrapper expansion
- [ ] Methods/properties/constructors
- [ ] Rename/include/exclude rules
- [ ] Async passthrough support

### D3. Version strategies
- [ ] LCD mode
- [ ] Targeted mode
- [ ] Adaptive mode (phase 2)

## Track E - Type Mapping

### E1. Type map definitions
- [ ] Attribute model
- [ ] Fluent model
- [ ] JSON model

### E2. Type map generation
- [ ] Member-level mapping emit
- [ ] Collection + nullability handling
- [ ] Custom converter hooks

### E3. Mapperly bridge
- [ ] Integration spike
- [ ] Decision doc (native vs delegated paths)

## Track F - Analyzer + Code Fixes

### F1. Diagnostics MVP
- [ ] WG2001 direct third-party usage detected
- [ ] WG2002 mapped equivalent available

### F2. Fixes MVP
- [ ] 1:1 method call rewrite
- [ ] using/import updates
- [ ] Fix-All support

### F3. Safety controls
- [ ] Safe-only vs guided fixes
- [ ] Migration report output

## Track G - Tests

- [ ] Snapshot tests for generated code
- [ ] Golden manifest tests
- [ ] Analyzer and code-fix tests
- [ ] Multi-version compatibility matrix tests

## Suggested first subdivision (next pass)

1. **Sprint 1**: A1, A2, B1, B2, D1
2. **Sprint 2**: B3, C1, C2, D2
3. **Sprint 3**: F1, F2, E1, E2
4. **Sprint 4**: D3, E3, F3, G
