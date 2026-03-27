# WrapGod Initial Plan

## North Star

Given one or more third-party assembly versions, WrapGod can generate stable internal abstractions and provide automated code migration onto those abstractions.

## Pillars

1. **Extractor** (multi-version API manifest)
2. **Planner** (merge attribute/fluent/json config)
3. **Generator** (wrappers/facades/proxies/adapters)
4. **Analyzers** (diagnostics + code-fix migration)
5. **Type Mapping** (source-generated source->destination maps)

## Scope for V1

### In-scope

- Extract public API surface for selected assemblies into `wrapgod.manifest.json`
- Support multiple input versions and compute diff metadata
- Generate wrapper interfaces and forwarding facades for methods/properties
- Config inputs: JSON + attributes (fluent starts in preview)
- Basic type map definitions and generated mapping stubs
- Analyzer detection of direct third-party usage
- Code fixes for safe 1:1 replacements

### Out-of-scope (V1)

- Full event/delegate wrapping coverage
- Complete unsafe/pointer/ref struct support
- Behavioral rewrite automation beyond safe deterministic fixes
- IDE extension UI

## High-Level Design

- Canonical manifest is source of truth
- Planner builds `VersionedGenerationPlan`
- Generator consumes plan deterministically with Incremental Generator
- Analyzer/code-fix uses same plan metadata for safe migration actions

## Compatibility Modes

- **LCD** (lowest common denominator across versions)
- **Targeted** (single selected version)
- **Adaptive** (generated runtime branching by available members)

## Diagnostic Families

- `WG1xxx`: API compatibility/version diagnostics
- `WG2xxx`: migration/analyzer diagnostics
- `WG3xxx`: type mapping diagnostics

## Immediate Next Steps

1. Lock manifest JSON schema (v1 draft)
2. Implement extractor prototype + stable symbol identity
3. Build generator skeleton with one successful wrapper output path
4. Build first analyzer rule for direct vendor usage (`WG2001`)
5. Build first safe code fix (`WG2002`) for mapped 1:1 method calls
