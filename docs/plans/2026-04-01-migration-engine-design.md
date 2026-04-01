# Migration Engine Design

**Date:** 2026-04-01
**Status:** Accepted
**Authors:** JerrettDavis, Claude Opus 4.6

## Problem

When .NET libraries ship breaking changes between versions (renamed types, changed signatures, removed members), developers manually hunt through their codebase to fix every call site. This is tedious, error-prone, and scales poorly across large solutions.

WrapGod already extracts API manifests and diffs versions to detect breaking changes. What's missing is the ability to encode those changes as executable transformation rules and apply them to a codebase automatically.

## Goals

- Let anyone author migration rules for any .NET library (not just WrapGod-wrapped ones)
- Auto-generate draft migration schemas from manifest diffs (80% coverage)
- Let authors enrich schemas with semantic context the diff can't capture
- Apply migrations via CLI with dry-run, progress tracking, and idempotent re-runs
- Work on code that doesn't compile (since breaking changes already broke it)
- Distribute migration packs as standalone files or NuGet packages

## Non-Goals

- Full semantic rewrite engine (C-level) — architecture supports it, not building it now
- Running tests or CI — that's the user's responsibility
- IDE integration — CLI-first, IDE plugins are future work
- Blazor/Razor template rewriting — .cs files only in v1

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Schema generation | Auto-infer from diff, then human-editable | Diff gives 80% for free; real migrations need semantic context |
| Transformation scope | Syntax + structural rewrites (B-level) | Covers ~90% of breaking changes; architecture extensible to C-level |
| Rewrite engine | Syntax-first, optional semantic verification | Works on non-compiling code; verification catches edge cases |
| Distribution | File-first, NuGet as optional packaging | Low authoring friction; NuGet is a convenience layer |

## Architecture

### Pipeline Overview

```
Phase 1: GENERATE                Phase 2: ENRICH              Phase 3: APPLY
---                              ---                           ---
Library v6 DLL --+                                             Project *.cs files
Library v7 DLL --+               Migration schema                    |
                 |               (JSON file)                  [Roslyn SyntaxParser]
         [MultiVersionExtractor]       |                             |
                 |               [Author edits,              Per-file syntax trees
          VersionDiff             adds notes,                        |
                 |                adjusts confidence]        [RuleEngine.Apply()]
     [MigrationSchemaGenerator]        |                             |
                 |                     |                     Rewritten trees
          Draft schema -------> Final schema ----------->           |
          (auto)                (verified)                   [SyntaxWriter]
                                                                    |
                                                             Modified *.cs
                                                                    |
                                                             [SemanticVerifier]
                                                             (optional)
                                                                    |
                                                             Verification report
```

### New Assemblies

| Project | Responsibility | Dependencies |
|---------|---------------|--------------|
| `WrapGod.Migration` | Schema model, rule types, schema generator from `VersionDiff`, serialization | `WrapGod.Manifest`, `WrapGod.Extractor` |
| `WrapGod.Migration.Engine` | Roslyn syntax rewriter, rule matching, transform execution, verification | `WrapGod.Migration`, `Microsoft.CodeAnalysis.CSharp` |
| `WrapGod.Cli` (extended) | `migrate generate`, `migrate apply`, `migrate verify`, `migrate status` | Both above |

**Why two assemblies?** `WrapGod.Migration` is the data model — lightweight, no Roslyn dependency. The engine pulls in `Microsoft.CodeAnalysis`, which is heavy. Schema authoring/tooling shouldn't require the compiler.

## Migration Schema Format

**File convention:** `{library}.{from}-to-{to}.wrapgod-migration.json`

```json
{
  "schema": "wrapgod-migration/1.0",
  "library": "MudBlazor",
  "from": "6.0.0",
  "to": "7.0.0",
  "generatedFrom": "manifest-diff",
  "lastEdited": "2026-04-01T00:00:00Z",
  "rules": [
    {
      "id": "MUD-001",
      "kind": "rename-type",
      "source": "MudBlazor.MudNavMenu",
      "target": "MudBlazor.MudNavGroup",
      "confidence": "auto",
      "note": null
    },
    {
      "id": "MUD-002",
      "kind": "rename-member",
      "type": "MudBlazor.MudButton",
      "source": "DisableElevation",
      "target": "Elevation",
      "confidence": "auto"
    },
    {
      "id": "MUD-003",
      "kind": "change-signature",
      "type": "MudBlazor.MudDialog",
      "member": "Show",
      "from": { "params": ["string title", "RenderFragment content"] },
      "to": { "params": ["DialogParameters parameters", "DialogOptions options"] },
      "transform": "manual",
      "note": "Parameters restructured -- requires manual mapping of title into DialogParameters"
    }
  ]
}
```

### Schema Design Choices

- Each rule has an `id` for tracking which rules have been applied
- `confidence`: `auto` (inferred from diff), `verified` (human-reviewed), `manual` (can't auto-apply, advisory only)
- `kind` is an extensible discriminator -- new rule kinds added without schema version bump
- `note` lets authors add human context the diff can't capture

### Rule Kinds

**Syntax-level (A-level, shipping first):**

`rename-type`, `rename-member`, `rename-namespace`, `change-parameter`, `remove-member`, `add-required-parameter`, `change-type-reference`

**Structural (B-level, shipping second):**

`split-method`, `merge-methods`, `extract-parameter-object`, `property-to-method`, `rewrite-fluent-chain`, `move-member`

## Rule Engine

### Core Interfaces

```csharp
// One per rule kind -- the extension point for future C-level rules
public interface IRuleRewriter
{
    string Kind { get; }
    SyntaxNode? TryRewrite(SyntaxNode node, MigrationRule rule, RewriteContext ctx);
}

// Tracks what happened for reporting and state
public class RewriteContext
{
    public string FilePath { get; }
    public List<AppliedRewrite> Applied { get; }
    public List<SkippedRewrite> Skipped { get; }
    public void RecordApplied(MigrationRule rule, TextSpan original, TextSpan replacement);
    public void RecordSkipped(MigrationRule rule, TextSpan location, string reason);
}

// Orchestrates the pipeline
public class MigrationEngine
{
    private readonly ImmutableArray<IRuleRewriter> _rewriters;
    public MigrationResult Apply(MigrationSchema schema, IEnumerable<string> files);
    public MigrationResult DryRun(MigrationSchema schema, IEnumerable<string> files);
}
```

### Initial Rewriters

| Rewriter | Matches | Transforms |
|----------|---------|-----------|
| `RenameTypeRewriter` | `IdentifierNameSyntax`, `QualifiedNameSyntax`, `UsingDirectiveSyntax` | Replaces type name, updates usings |
| `RenameNamespaceRewriter` | `UsingDirectiveSyntax`, `QualifiedNameSyntax` | Replaces namespace segments |
| `RenameMemberRewriter` | `MemberAccessExpressionSyntax`, `IdentifierNameSyntax` | Replaces member name at call sites |
| `ChangeParameterRewriter` | `ArgumentListSyntax` on matching invocations | Reorders, renames, wraps arguments |
| `RemoveMemberRewriter` | `InvocationExpressionSyntax`, `MemberAccessExpressionSyntax` | Comments out with `// MIGRATION:` TODO |
| `AddRequiredParameterRewriter` | `ArgumentListSyntax` on matching invocations | Appends `default` or placeholder argument |
| `ChangeTypeReferenceRewriter` | `TypeSyntax` in declarations, casts, generics | Replaces type references |

### Ambiguity Handling

When a rewriter matches a node but can't determine with certainty it's the right target (e.g., two types with the same short name in different namespaces), it records a `SkippedRewrite` with a reason. The `status` command surfaces these for manual resolution.

### Trivia Preservation

All rewriters use Roslyn's `WithTriviaFrom()` to preserve comments, whitespace, and formatting. Migrations should not reformat code.

## Schema Generation from Diff

The `migrate generate` command maps existing `VersionDiff` data to migration rules.

### BreakingChange to Rule Mapping

| BreakingChangeKind | Generated rule kind | Confidence |
|--------------------|-------------------|------------|
| `TypeRemoved` | `remove-member` or `rename-type` (if similar type found) | `auto`, `verified` if name similarity > 0.85 |
| `MemberRemoved` | `remove-member` or `rename-member` (if similar member found) | Same heuristic |
| `ReturnTypeChanged` | `change-type-reference` | `auto` |
| `ParameterTypesChanged` | `change-parameter` | `auto` for reorderings, `manual` for semantic changes |

### Rename Detection

When a type/member is removed and a similar one appears (Levenshtein distance, shared prefix/suffix, same parameter shape), the generator emits a rename rule instead of remove + add. The `confidence` field reflects match strength.

### What the Diff Can't Know

- Whether a rename also changed semantics
- Whether a removed overload's callers should use overload A or B
- Whether a type change from `string` to `Uri` needs wrapping logic
- Behavioral changes behind the same signature

These are left as `confidence: "manual"` with `note` prompting author review.

## Migration State Tracking

### State File: `{schema-name}.state.json`

```json
{
  "schema": "mudblazor.6.0-to-7.0.wrapgod-migration.json",
  "schemaHash": "sha256:a1b2c3...",
  "startedAt": "2026-04-01T12:00:00Z",
  "lastRunAt": "2026-04-01T12:05:00Z",
  "summary": {
    "totalRules": 47,
    "applied": 38,
    "skipped": 6,
    "manual": 3
  },
  "applied": [
    {
      "ruleId": "MUD-001",
      "file": "src/Components/NavPanel.razor.cs",
      "line": 14,
      "originalText": "MudNavMenu",
      "replacedWith": "MudNavGroup",
      "appliedAt": "2026-04-01T12:00:01Z"
    }
  ],
  "skipped": [
    {
      "ruleId": "MUD-017",
      "file": "src/Dialogs/ConfirmDialog.cs",
      "line": 42,
      "reason": "Ambiguous: two overloads of Show() in scope"
    }
  ],
  "manual": [
    {
      "ruleId": "MUD-003",
      "note": "Parameters restructured -- requires manual mapping",
      "matchedFiles": ["src/Dialogs/ConfirmDialog.cs", "src/Dialogs/EditDialog.cs"]
    }
  ]
}
```

### Key Behaviors

- **Idempotent re-runs:** Rules already in `applied` are skipped on subsequent runs. Iterate: apply, fix manual items, apply again.
- **Schema change detection:** `schemaHash` tracks schema content. Edited schemas trigger re-evaluation of affected rules.
- **Git-friendly:** State file is designed to be committed. Teams track migration progress across branches.
- **Explicit location:** State lives next to the schema file, not in a hidden directory.

## Verification & Safety

### Semantic Verification Pass

`migrate verify` is an optional safety net that attempts to compile the rewritten project and attributes errors back to migration rules.

### Rule Attribution

The state file tracks which rules touched which lines. When a compile error lands on or near a rewritten line, the verifier correlates it to the responsible rule. "247 compile errors" becomes "38 from MUD-003, 12 from MUD-017, 197 pre-existing."

### Pre-Migration Baseline

On the first `apply`, the engine optionally snapshots the current diagnostic count. `verify` reports net-new diagnostics introduced by migration rules, filtering pre-existing noise.

### What Verify Does NOT Do

- Block `apply` -- verification is always separate and optional
- Auto-fix -- potential future C-level feature
- Run tests -- user's responsibility (status output reminds them)

## CLI Commands

```
wrap-god migrate generate   -- Generate draft schema from two library versions
wrap-god migrate apply      -- Apply a migration schema to your codebase
wrap-god migrate verify     -- Run semantic verification on applied changes
wrap-god migrate status     -- Show which rules have been applied/pending/manual
```

### Example Workflow

```bash
# 1. Generate draft migration schema from NuGet versions
wrap-god migrate generate \
  --package MudBlazor --from 6.0.0 --to 7.0.0 \
  --output mudblazor.6.0-to-7.0.wrapgod-migration.json

# 2. Author reviews/enriches the schema in a text editor

# 3. Dry-run to preview changes
wrap-god migrate apply \
  --schema mudblazor.6.0-to-7.0.wrapgod-migration.json \
  --project-dir ./src --dry-run

# 4. Apply the migration
wrap-god migrate apply \
  --schema mudblazor.6.0-to-7.0.wrapgod-migration.json \
  --project-dir ./src

# 5. Check progress
wrap-god migrate status \
  --schema mudblazor.6.0-to-7.0.wrapgod-migration.json \
  --project-dir ./src

# 6. Optional semantic verification
wrap-god migrate verify --project-dir ./src
```

## Distribution

### File-First

The core unit is a standalone `.wrapgod-migration.json` file. Drop it in your project, reference by path, or download from a URL. Zero tooling required to author.

### NuGet Packaging (Optional)

Migration packs can be wrapped in NuGet packages for discoverability (e.g., `WrapGod.Migration.MudBlazor.v6-to-v7`). The package just contains the JSON file(s). The CLI doesn't need to know how the file arrived.

### Library Authors

Library authors can ship migration schemas in their own repo or NuGet package. Convention: `migrations/` directory with versioned schema files.

## Future Work (Not in Scope)

- **C-level semantic rewrites:** Control flow changes, DI registration rewrites, async conversion
- **Razor/Blazor template rewriting:** Component tag renaming, parameter attribute changes
- **IDE integration:** VS/Rider extensions for interactive migration
- **Migration composition:** Chaining v5->v6->v7 schemas into a single v5->v7 migration
- **Community registry:** Centralized index of published migration packs
- **Multi-library orchestration:** Coordinated migration of related packages
