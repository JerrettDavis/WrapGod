# RFC 0054 — Structured Diagnostics Contract and Reporting

- **Issue:** #54
- **Status:** Proposed
- **Owner:** @JerrettDavis/agent-jarvis
- **Last updated:** 2026-03-27

## 1) Summary

Define a single diagnostics contract used across WrapGod pipeline stages (extract, plan, generate, analyze, fix), with clear severity semantics, CLI exit-code behavior, and deterministic mapping to SARIF.

This RFC standardizes:

1. A canonical JSON diagnostic schema (`wg.diagnostic.v1`)
2. Diagnostic code catalog classes and ownership (`WG1xxx`, `WG2xxx`, `WG3xxx`, plus existing legacy classes)
3. Severity and policy model (`error|warning|note|none`)
4. CLI exit-code policy for automation and CI
5. SARIF mapping guidance (`SARIF 2.1.0`)

## 2) Goals and non-goals

### Goals

- One machine-readable model for all diagnostics in WrapGod.
- Stable, grep-friendly code taxonomy with documented class ownership.
- Unambiguous mapping from diagnostics to process exit codes.
- First-class CI/security tooling interoperability via SARIF.
- Backward compatibility path for current diagnostics (including `WG2001`, `WG2002`, and config merge codes `WG6001`-`WG6004`).

### Non-goals

- Full implementation of every formatter/transport in this RFC.
- Deciding every future specific diagnostic code.
- IDE UX policy beyond what naturally follows from Roslyn and SARIF semantics.

## 3) Terminology

- **Diagnostic**: One finding emitted by WrapGod tooling.
- **Rule/Code**: Stable identifier (`WG####`) for a diagnostic type.
- **Stage**: Pipeline phase emitting a diagnostic (`extract|plan|generate|analyze|fix|cli|config`).
- **Policy level**: Effective severity after config/editorconfig/CLI overrides.
- **Report**: Aggregate output in console, JSON, or SARIF.

## 4) Canonical contract (`wg.diagnostic.v1`)

### 4.1 Required fields

Each diagnostic record MUST include:

- `schema` (string): exact contract ID, currently `wg.diagnostic.v1`
- `code` (string): WrapGod code, pattern `^WG[0-9]{4}$`
- `severity` (enum): `error|warning|note|none`
- `stage` (enum): `extract|plan|generate|analyze|fix|cli|config`
- `message` (string): human-readable explanation
- `source` (object): emitter identity (`tool`, optional `component`)
- `location` (object|null): file/span context if applicable
- `helpUri` (string|null): documentation URL for this code
- `timestampUtc` (ISO-8601 string)

### 4.2 Recommended fields

- `category` (string): high-level category (`compatibility`, `migration`, `typemap`, `config`, etc.)
- `tags` (string[]): additional traits (`breaking`, `autofixable`, `performance`)
- `relatedLocations` (array): additional source locations
- `properties` (object): extension bag for machine metadata
- `fingerprint` (string): stable dedupe key
- `suppression` (object|null): suppression metadata (`kind`, `justification`, `source`)

### 4.3 JSON shape (normative draft)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://wrapgod.dev/schemas/wg.diagnostic.v1.json",
  "title": "WrapGod Diagnostic v1",
  "type": "object",
  "required": [
    "schema",
    "code",
    "severity",
    "stage",
    "message",
    "source",
    "timestampUtc"
  ],
  "properties": {
    "schema": { "const": "wg.diagnostic.v1" },
    "code": { "type": "string", "pattern": "^WG[0-9]{4}$" },
    "severity": { "enum": ["error", "warning", "note", "none"] },
    "stage": {
      "enum": ["extract", "plan", "generate", "analyze", "fix", "cli", "config"]
    },
    "category": { "type": "string" },
    "message": { "type": "string", "minLength": 1 },
    "source": {
      "type": "object",
      "required": ["tool"],
      "properties": {
        "tool": { "type": "string", "minLength": 1 },
        "component": { "type": "string" },
        "version": { "type": "string" }
      },
      "additionalProperties": true
    },
    "location": {
      "type": ["object", "null"],
      "properties": {
        "uri": { "type": "string" },
        "line": { "type": "integer", "minimum": 1 },
        "column": { "type": "integer", "minimum": 1 },
        "endLine": { "type": "integer", "minimum": 1 },
        "endColumn": { "type": "integer", "minimum": 1 },
        "symbol": { "type": "string" }
      },
      "additionalProperties": true
    },
    "relatedLocations": {
      "type": "array",
      "items": { "$ref": "#/$defs/location" }
    },
    "helpUri": { "type": ["string", "null"], "format": "uri" },
    "fingerprint": { "type": "string" },
    "properties": { "type": "object", "additionalProperties": true },
    "suppression": {
      "type": ["object", "null"],
      "properties": {
        "kind": { "enum": ["editorconfig", "pragma", "global", "cli", "baseline"] },
        "justification": { "type": "string" },
        "source": { "type": "string" }
      },
      "additionalProperties": true
    },
    "timestampUtc": { "type": "string", "format": "date-time" }
  },
  "$defs": {
    "location": {
      "type": "object",
      "properties": {
        "uri": { "type": "string" },
        "line": { "type": "integer", "minimum": 1 },
        "column": { "type": "integer", "minimum": 1 },
        "endLine": { "type": "integer", "minimum": 1 },
        "endColumn": { "type": "integer", "minimum": 1 },
        "symbol": { "type": "string" }
      },
      "additionalProperties": true
    }
  },
  "additionalProperties": true
}
```

> Note: `relatedLocations` and `location` allow either file-based and symbol-based diagnostics.

## 5) WG code catalog classes (baseline)

This RFC confirms catalog ownership and code-family semantics.

### 5.1 Canonical classes in-scope

| Class | Area | Typical stage(s) | Meaning |
|---|---|---|---|
| `WG1xxx` | Compatibility/versioning | `extract`, `plan`, `generate` | API compatibility, version diff and contract drift findings |
| `WG2xxx` | Migration/analyzers | `analyze`, `fix` | Direct usage, migration opportunities, unsafe rewrite blockers |
| `WG3xxx` | Type mapping | `plan`, `generate`, `analyze` | Mapping completeness, ambiguity, unsupported conversion patterns |

### 5.2 Existing/legacy classes retained

| Class | Area |
|---|---|
| `WG6xxx` | Configuration merge/precedence conflicts (already in docs and code) |

`WG6001`-`WG6004` remain valid and are brought under this contract without renumbering.

### 5.3 Reservation guidance

To avoid collisions, reserve future bands now:

- `WG4xxx`: generator/template output integrity and deterministic rendering checks
- `WG5xxx`: extractor/planner infrastructure/runtime environment diagnostics
- `WG7xxx`: CLI/reporting/automation integration diagnostics
- `WG8xxx`-`WG9xxx`: reserved for future product areas

## 6) Severity model

### 6.1 Effective severities

- `error`: must fail quality gate
- `warning`: actionable but not always failing by default
- `note`: informational/suggestion
- `none`: suppressed/not emitted in human-facing channels

### 6.2 Default severity baseline

- Safety/correctness-breaking findings default to `error`
- Migration guidance and non-blocking drift default to `warning`
- Hints/telemetry-style findings default to `note`

Overrides MAY come from editorconfig, analyzerconfig, project config, or CLI switches.

## 7) Exit-code policy (CLI/automation)

### 7.1 Standard exit codes

- `0`: success; no effective `error` diagnostics
- `1`: command/runtime failure (exception, bad args, IO failure, infra fault)
- `2`: diagnostics gate failed due to `error` diagnostics
- `3`: diagnostics gate failed due to warnings when warnings-as-errors is enabled

### 7.2 Gate evaluation rules

1. Apply suppression/override policy to derive effective severities.
2. If command fails technically, return `1` regardless of diagnostics.
3. If any effective `error` exists, return `2`.
4. Else if `warningsAsErrors` policy is enabled and any effective `warning` exists, return `3`.
5. Else return `0`.

### 7.3 Why split `1` vs `2`/`3`

CI and scripts can distinguish:

- tool/system failures (retry/infrastructure action) vs
- product-quality gate failures (code/config action)

## 8) SARIF mapping guidance (`SARIF 2.1.0`)

WrapGod SARIF emitter SHOULD use one `run` per WrapGod invocation with stable `tool.driver.name = "WrapGod"`.

### 8.1 Field mapping

| WrapGod field | SARIF target |
|---|---|
| `code` | `result.ruleId` |
| `message` | `result.message.text` |
| `severity` | `result.level` (`error` -> `error`, `warning` -> `warning`, `note` -> `note`) |
| `helpUri` | `tool.driver.rules[].helpUri` |
| `category`, `tags`, `properties` | `result.properties` / `rule.properties.tags` |
| `location` | `result.locations[0].physicalLocation` |
| `relatedLocations` | `result.relatedLocations[]` |
| `fingerprint` | `result.fingerprints` |
| `suppression` | `result.suppressions[]` |

### 8.2 Rule catalog behavior

- Emit each unique `WG####` once in `tool.driver.rules[]`.
- Provide `shortDescription` and `fullDescription` where available.
- Include deterministic rule metadata (default severity and category).

### 8.3 URI handling

- Use repository-relative URIs where possible (`src/...`) with SARIF base URI support.
- Preserve absolute URIs only when required by host environment.

### 8.4 Interop notes

- Keep `ruleId` exactly `WG####` to maintain parity with Roslyn/analyzer UX.
- Preserve suppressed diagnostics in SARIF when requested (`--include-suppressed`) so governance tooling can audit suppression use.

## 9) Output format contract

All formats should derive from the same in-memory model:

- **Console**: human-oriented summary + top diagnostics
- **JSON**: canonical list of `wg.diagnostic.v1` records
- **SARIF**: mapped output for CI/security systems

No formatter may invent data not present in canonical model except presentation-only fields.

## 10) Backward compatibility

- Existing analyzer IDs (`WG2001`, `WG2002`) are unchanged.
- Existing config diagnostics (`WG6001`-`WG6004`) are unchanged.
- Any legacy output without `schema` should be wrapped/adapted to `wg.diagnostic.v1` before JSON/SARIF emission.

## 11) Follow-up implementation issues

- [ ] Implement `wg.diagnostic.v1` model + JSON emitter
- [ ] Implement SARIF emitter + rule catalog projection
- [ ] Implement CLI gate evaluator and standardized exit codes
- [ ] Adopt contract in extractor/planner/generator/analyzer pipelines

## 12) Decision

Approve this RFC as the baseline diagnostics contract for MVP. Treat `wg.diagnostic.v1` + exit-code policy + SARIF mapping as normative for all new diagnostic-producing features.
