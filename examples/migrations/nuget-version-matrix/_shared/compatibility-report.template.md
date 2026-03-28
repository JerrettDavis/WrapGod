# {{package.name}} Version Compatibility Report

API delta analysis across {{package.versions}}.

## Summary

- Baseline: `{{versionMatrix.baseline}}`
- Latest: `{{versionMatrix.latest}}`
- Deltas: `{{summary.totalDeltas}}`
- Introduced members: `{{summary.introducedMembers}}`
- Removed members: `{{summary.removedMembers}}`
- Changed members: `{{summary.changedMembers}}`

---

{{#each deltas}}
## {{title}} ({{id}})

**Severity:** `{{severity}}`

**Migration recommendation:** {{migrationRecommendation}}

### Introduced members
{{#if members.introduced.length}}
{{#each members.introduced}}
- `{{displayName}}` ({{kind}}, introduced in {{version}})
{{/each}}
{{else}}
- None
{{/if}}

### Removed members
{{#if members.removed.length}}
{{#each members.removed}}
- `{{displayName}}` ({{kind}}, removed by {{version}})
{{/each}}
{{else}}
- None
{{/if}}

### Changed members
{{#if members.changed.length}}
{{#each members.changed}}
- `{{displayName}}` ({{kind}}, changed in {{version}}): {{changeSummary}}
{{/each}}
{{else}}
- None
{{/if}}

{{#if notes}}
**Notes:** {{notes}}
{{/if}}

---
{{/each}}
