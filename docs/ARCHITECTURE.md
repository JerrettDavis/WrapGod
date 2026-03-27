# WrapGod Architecture (Draft)

## Pipeline

1. **Extract**
   - Inputs: one or more assembly artifacts (DLL/NuGet) + version labels
   - Output: versioned API manifest with stable IDs and per-version presence/signature metadata

2. **Plan**
   - Inputs:
     - Manifest
     - Attributes in user code
     - Fluent DSL model
     - JSON override config
   - Output: normalized `VersionedGenerationPlan`

3. **Generate**
   - Roslyn Incremental Generator reads plan
   - Emits:
     - interfaces
     - facade/proxy implementations
     - adapter shims
     - mapping invocation glue

4. **Analyze + Fix**
   - Analyzer identifies direct third-party usage and incompatibilities
   - CodeFix provider rewrites usages to generated APIs when safe
   - Produces migration report

## Core Models

- `ApiManifest`
- `VersionSnapshot`
- `ApiTypeNode`
- `ApiMemberNode`
- `ApiSignature`
- `VersionDiff`
- `TypeMappingPlan`
- `VersionedGenerationPlan`

## Determinism and Reproducibility

- Stable symbol/member IDs
- Schema versioning in manifest
- Hash source artifacts and include in manifest metadata
- Fail generation with actionable diagnostics on drift

## Performance Notes

- Incremental caches keyed by:
  - manifest hash
  - config hash
  - compilation symbol version
- Keep generated code partitioned by source type to limit invalidations
- Avoid reflection in generator path; reflection/metadata APIs only in extractor
