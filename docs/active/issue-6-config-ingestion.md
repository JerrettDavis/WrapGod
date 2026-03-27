# Issue #6 — Config Ingestion (WIP)

Status: In progress
Owner: Jarvis

## Scope
- JSON config model + parsing
- Attribute-based config discovery
- Merge precedence and conflict diagnostics

## Implementation Checklist
- [ ] Add config contracts in `WrapGod.Abstractions`
- [ ] Implement JSON ingestion in `WrapGod.Manifest`/planner layer
- [ ] Implement attribute ingestion in generator/planner path
- [ ] Add merge engine with precedence tests
- [ ] Emit conflict diagnostics (WG-series)

## Notes
This file is a live implementation log for visibility during active development.
