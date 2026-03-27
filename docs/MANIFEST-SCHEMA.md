# Manifest Schema v1 (`wrapgod.manifest.v1`)

Canonical schema file:

- `schemas/wrapgod.manifest.v1.schema.json`

Examples:

- valid: `schemas/examples/manifest.valid.json`
- invalid: `schemas/examples/manifest.invalid.missing-assembly.json`

## Versioning Strategy

- `schemaVersion` is required and currently fixed at `1.0`.
- Breaking schema changes require a new major schema version file (`v2`, etc.).
- Non-breaking additive fields can be introduced in-place while keeping `1.0` compatibility.
- Consumers should ignore unknown fields only when explicitly allowed by future schema versions; v1 currently uses `additionalProperties: false` in core objects to enforce strictness.

## Design Notes

V1 intentionally captures:

- assembly identity (`name`, `version`, optional culture/token/tfm)
- stable type/member identifiers
- member signatures (parameters, generics)
- per-type/per-member version presence metadata (`introducedIn`, `removedIn`, `changedIn`)

This gives us deterministic extraction output and a stable planner/generator contract.
