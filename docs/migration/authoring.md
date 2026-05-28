# Authoring a Migration Schema

This guide is for **library maintainers** who need to write a `MigrationSchema` so their users can upgrade between breaking versions — for example, providing a migration pack that helps consumers move from `MyLibrary 2.x` to `3.x`.

> **Just consuming a migration?** See [Applying Migrations](applying.md) instead.

---

## When to Author a Pack (vs. Relying on `migrate generate`)

`migrate generate` automatically builds a draft schema from two NuGet versions:

```bash
wrap-god migrate generate \
  --package MyLibrary \
  --from 2.0.0 --to 3.0.0
```

This covers the **boring 80%** — type renames, namespace moves, simple member renames. It is always your starting point.

You need to author (or hand-tune) rules when:

| Situation | What to do |
|-----------|-----------|
| A method was split into two methods | Add a `splitMethod` rule (cannot be generated) |
| Parameters were restructured into a new options object | Add an `extractParameterObject` rule |
| A property became a method | Add a `propertyToMethod` rule |
| A member moved to a different type | Add a `moveMember` rule |
| Auto-generated confidence is wrong | Promote/demote `confidence` by hand |
| A removal needs a human note | Set the `note` field on `removeMember` |
| You want to suppress a noisy auto-rule | Delete it from the JSON |

---

## Schema File Structure

A migration schema is a UTF-8 JSON file. The recommended filename convention is:

```
{library}.{fromVersion}-to-{toVersion}.wrapgod-migration.json
```

Example: `mudblazor.6.0.0-to-7.0.0.wrapgod-migration.json`

### Top-level fields

```json
{
  "schema": "wrapgod-migration/1.0",
  "library": "MudBlazor",
  "from": "6.0.0",
  "to": "7.0.0",
  "generatedFrom": "manifest-diff",
  "lastEdited": "2026-04-01T00:00:00Z",
  "rules": [ /* array of rule objects — see below */ ]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `schema` | Yes | Always `"wrapgod-migration/1.0"`. Enables forward-compatible parsing. |
| `library` | Yes | NuGet package ID, e.g. `"MudBlazor"`. |
| `from` | Yes | Old (source) version string, e.g. `"6.0.0"`. |
| `to` | Yes | New (target) version string, e.g. `"7.0.0"`. |
| `generatedFrom` | No | Informational provenance: `"manifest-diff"`, `"manual"`, etc. |
| `lastEdited` | No | ISO-8601 timestamp of last manual edit. |
| `rules` | Yes | Array of rule objects. May be empty; the schema is still valid. |

### Rule object skeleton

Every rule shares a common set of fields:

| Field | Required | Description |
|-------|----------|-------------|
| `id` | Yes | Unique identifier within the schema, e.g. `"MUD-001"`. Stable across edits — never reuse a retired ID. |
| `kind` | Yes | Rule discriminator (camelCase). Determines the rule's shape and which rewriter handles it. |
| `confidence` | No | `"auto"`, `"verified"`, or `"manual"`. Defaults to `"auto"` when omitted. See [Rule Confidence](#rule-confidence). |
| `note` | No | Human-readable description of why this rule exists or what a `manual` rule requires. Shown in `migrate apply` output. |

Additional fields depend on `kind` — documented for each kind below.

### JSON comments

The serializer accepts `//`-style line comments:

```jsonc
{
  "schema": "wrapgod-migration/1.0",
  "library": "MudBlazor",
  // Bump after every manual edit
  "lastEdited": "2026-04-15T10:00:00Z",
  "rules": [
    // Renamed in MudBlazor 7 RC1
    {
      "id": "MUD-001",
      "kind": "renameType",
      "oldName": "MudBlazor.Button",
      "newName": "MudBlazor.MudButton"
    }
  ]
}
```

---

## Rule Kinds

### `renameType`

A type was renamed (optionally including a namespace change). The rewriter rewrites identifier nodes and fully-qualified names wherever the old name appears in a type position.

**JSON shape:**

```json
{
  "id": "MUD-001",
  "kind": "renameType",
  "confidence": "auto",
  "oldName": "MudBlazor.Button",
  "newName": "MudBlazor.MudButton"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `oldName` | Yes | Fully-qualified old type name. |
| `newName` | Yes | Fully-qualified new type name. |

**Before / After:**

```csharp
// Before
Button btn = new Button();
Button[] btns = GetButtons();

// After
MudButton btn = new MudButton();
MudButton[] btns = GetButtons();
```

**When to use:** Type was renamed or moved to a different namespace. If an entire namespace moved (multiple types), prefer `renameNamespace` to avoid generating dozens of individual rules.

**Confidence guideline:** `auto` for generator-detected renames (similarity ≥ 0.65). `verified` if you have manually confirmed the rename is 1-to-1. `manual` only if human review is needed at each call site.

---

### `renameNamespace`

A group of types moved from one namespace to another. The rewriter updates `using` directives and qualified names that have `OldNamespace` as a prefix.

**JSON shape:**

```json
{
  "id": "MUD-002",
  "kind": "renameNamespace",
  "confidence": "auto",
  "oldNamespace": "MudBlazor.Components",
  "newNamespace": "MudBlazor"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `oldNamespace` | Yes | Old namespace prefix. Only exact-prefix matches are rewritten (followed by `.` or end-of-name). |
| `newNamespace` | Yes | Replacement namespace prefix. |

**Before / After:**

```csharp
// Before
using MudBlazor.Components;
using MudBlazor.Components.Dialog;

// After
using MudBlazor;
using MudBlazor.Dialog;
```

**When to use:** When 2+ types relocated from one namespace tree to another. The generator collapses per-type renames into a single namespace rule when ≥2 types share the same relocation pattern. Prefer this over many individual `renameType` rules for bulk relocations.

**Confidence guideline:** `auto` when generated from a diff showing ≥2 type moves in the same namespace. `verified` after checking that no unrelated types share the old namespace prefix.

---

### `renameMember`

A method, property, field, or event was renamed on its declaring type.

**JSON shape:**

```json
{
  "id": "MUD-003",
  "kind": "renameMember",
  "confidence": "verified",
  "typeName": "MudBlazor.MudButton",
  "oldMemberName": "Color",
  "newMemberName": "ButtonColor"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `typeName` | Yes | Declaring type (short name or fully-qualified). |
| `oldMemberName` | Yes | Old member name. |
| `newMemberName` | Yes | New member name. |

**Before / After:**

```csharp
// Before
MudButton btn = new MudButton();
btn.Color = MudColor.Primary;
var c = btn.Color;

// After
MudButton btn = new MudButton();
btn.ButtonColor = MudColor.Primary;
var c = btn.ButtonColor;
```

**When to use:** A method or property on a specific type was renamed. The rewriter infers the receiver type syntactically from local variable declarations and parameter types. When the receiver type cannot be determined (e.g., a chained call result), the rewrite is skipped and recorded as a `SkippedRewrite`.

**Confidence guideline:** `auto` for high-similarity generator matches. `verified` after manual confirmation the rename is safe. `manual` if receiver type inference might produce false positives in your library's usage patterns.

---

### `changeParameter`

A method parameter was renamed, or its type changed in a way that only the label needs updating (named argument site).

**JSON shape:**

```json
{
  "id": "MUD-004",
  "kind": "changeParameter",
  "confidence": "auto",
  "typeName": "MudBlazor.MudButton",
  "methodName": "SetSize",
  "oldParameterName": "size",
  "newParameterName": "buttonSize",
  "oldParameterType": "int",
  "newParameterType": "MudBlazor.Size"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `typeName` | Yes | Declaring type. |
| `methodName` | Yes | Method whose parameter changed. |
| `oldParameterName` | Yes | Old parameter name. |
| `newParameterName` | Yes | New parameter name. |
| `oldParameterType` | No | Old parameter type (informational). |
| `newParameterType` | No | New parameter type (informational). |

**Before / After (named argument rename):**

```csharp
// Before
btn.SetSize(size: 42);

// After
btn.SetSize(buttonSize: 42);
```

**When to use:** When a named argument label changed. The rewriter only updates the label — it does NOT convert the argument value. For type conversions (e.g., `int` → `MyEnum`), the rule records a `SkippedRewrite` because value conversion is semantic, not syntactic. Use `confidence: "manual"` when the type also changed and human conversion is needed.

**Confidence guideline:** `auto` for pure renames (same type, different name). `manual` when the parameter type changes and the argument value needs conversion.

---

### `removeMember`

A member was removed with no direct automated replacement. Every call site is annotated with a `MIGRATION` comment containing the original text and the rule's `note`.

**JSON shape:**

```json
{
  "id": "MUD-005",
  "kind": "removeMember",
  "confidence": "manual",
  "note": "Use NewApi.Process() instead.",
  "typeName": "MudBlazor.MudButton",
  "memberName": "Deprecated"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `typeName` | Yes | Declaring type. |
| `memberName` | Yes | Removed member name. |
| `note` | No (but strongly recommended) | Human-readable guidance for what to do instead. Shown in the comment and in `migrate apply` output. |

**Before / After:**

```csharp
// Before
void M(MudButton btn)
{
    btn.Deprecated();
    var x = 1;
}

// After
void M(MudButton btn)
{
    // MIGRATION: MUD-005 removed — Use NewApi.Process() instead.: btn.Deprecated();
    var x = 1;
}
```

**When to use:** Member was removed with no safe automated replacement. The rewriter always removes the call statement and adds an annotated comment so developers know where to look. Set `confidence: "manual"` to prevent auto-application — the default for generated `removeMember` rules.

**Confidence guideline:** `manual` in almost all cases. Use `auto` only if deleting the call site is definitively safe (e.g., a no-op method being removed).

---

### `addRequiredParameter`

A required parameter was added to an existing method. The rewriter inserts a placeholder (`null` for reference types, `default` for value types) with a `TODO MIGRATION` comment.

**JSON shape:**

```json
{
  "id": "MUD-006",
  "kind": "addRequiredParameter",
  "confidence": "manual",
  "typeName": "MudBlazor.MudThemeProvider",
  "methodName": "Apply",
  "parameterName": "theme",
  "parameterType": "MudBlazor.MudTheme",
  "position": 0
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `typeName` | Yes | Declaring type. |
| `methodName` | Yes | Method that gained the parameter. |
| `parameterName` | Yes | New parameter name (used in the TODO comment). |
| `parameterType` | Yes | New parameter type (determines `null` vs `default` placeholder). |
| `position` | Yes | Zero-based insertion index. |

**Before / After:**

```csharp
// Before
provider.Apply();

// After (reference type → null placeholder)
provider.Apply(null /* TODO MIGRATION: MUD-006 required arg 'theme' (MudTheme) added */);

// After (value type → default placeholder)
provider.Apply(default /* TODO MIGRATION: MUD-006 required arg 'size' (int) added */);
```

**When to use:** A new required parameter was added. The inserted placeholder ensures the code compiles after the migration; developers must replace it with the real value.

**Confidence guideline:** `manual` almost always — the correct value to pass depends on context that the rewriter cannot infer.

---

### `changeTypeReference`

A type reference (short name or fully-qualified) was replaced across the API surface. The rewriter targets syntactic type positions: field declarations, method return types, parameter types, cast expressions, `typeof()`, generic type arguments, base lists, and object creation expressions.

**JSON shape:**

```json
{
  "id": "MUD-007",
  "kind": "changeTypeReference",
  "confidence": "auto",
  "oldType": "System.Collections.Generic.IList`1",
  "newType": "System.Collections.Generic.IReadOnlyList`1"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `oldType` | Yes | Old type name (short or fully-qualified). Use the backtick-arity suffix for generics (e.g., `IList\`1`). |
| `newType` | Yes | New type name. |

**Before / After:**

```csharp
// Before
IList<string> items;
var t = typeof(IList<int>);

// After
IReadOnlyList<string> items;
var t = typeof(IReadOnlyList<int>);
```

**When to use:** An interface or class was renamed at the type-reference level (rather than the identifier level, which `renameType` handles). Also used when a library switched from one base type to another across its API surface (e.g., `List<T>` → `IReadOnlyList<T>` in return types).

**Confidence guideline:** `auto` when the replacement is always safe. `verified` when the new type is a drop-in replacement that you have confirmed. `manual` if not all usages can be safely replaced without semantic analysis.

---

### `splitMethod`

One method was replaced by two or more methods. The rewriter substitutes the original call statement with the sequence of new calls, leaving the original as a `MIGRATION` comment.

**JSON shape:**

```json
{
  "id": "MUD-008",
  "kind": "splitMethod",
  "confidence": "manual",
  "typeName": "MudBlazor.MudCard",
  "oldMethodName": "Render",
  "newMethodNames": ["RenderHeader", "RenderBody", "RenderFooter"]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `typeName` | Yes | Declaring type. |
| `oldMethodName` | Yes | Method being replaced. |
| `newMethodNames` | Yes | Ordered list of replacement method names. Must have ≥2 entries. |

**Before / After:**

```csharp
// Before
card.Render();

// After
// MIGRATION: MUD-008 split-method — original: card.Render();
card.RenderHeader();
card.RenderBody();
card.RenderFooter();
```

**Skipped cases:** The rewriter only handles statement-level calls. Call sites where the return value is consumed or where the call is chained are skipped with a descriptive reason:

```csharp
// Return value consumed → SkippedRewrite
var html = card.Render();

// Chained call → SkippedRewrite
card.Render().ToString();
```

**Confidence guideline:** `manual` in most cases — the rewriter cannot verify that the sequence of new calls is equivalent to the old call for all inputs.

---

### `extractParameterObject`

Several individually-named parameters were consolidated into a parameter object. The rewriter collapses matching named arguments into a `new {ParameterObjectType} { ... }` argument.

**JSON shape:**

```json
{
  "id": "MUD-009",
  "kind": "extractParameterObject",
  "confidence": "manual",
  "typeName": "MudBlazor.MudDialog",
  "methodName": "ShowAsync",
  "parameterObjectType": "MudBlazor.DialogParameters",
  "extractedParameters": ["title", "content"]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `typeName` | Yes | Declaring type. |
| `methodName` | Yes | Method whose parameters were consolidated. |
| `parameterObjectType` | Yes | New parameter object type name. |
| `extractedParameters` | Yes | Names of the parameters that moved into the object. Order within the object is alphabetical by property name. |

**Before / After:**

```csharp
// Before (named args matching extractedParameters)
dlg.ShowAsync(title: "Hello", content: "World");

// After
dlg.ShowAsync(new DialogParameters { Title = "Hello", Content = "World" });
```

Non-extracted arguments are preserved alongside the new object:

```csharp
// Before
dlg.ShowAsync(title: "Hello", content: "World", callback: cb);

// After
dlg.ShowAsync(callback: cb, new DialogParameters { Title = "Hello", Content = "World" });
```

**Skipped cases:** Positional-only call sites where arguments cannot be unambiguously mapped to named parameters are skipped.

**Confidence guideline:** `manual` — the property name casing convention (`Title` vs `title`) may vary.

---

### `propertyToMethod`

A property was converted to a method (reads → no-arg call; writes → single-arg call).

**JSON shape:**

```json
{
  "id": "MUD-010",
  "kind": "propertyToMethod",
  "confidence": "auto",
  "typeName": "MudBlazor.MudButton",
  "oldPropertyName": "Disabled",
  "newMethodName": "SetDisabled"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `typeName` | Yes | Declaring type. |
| `oldPropertyName` | Yes | Old property name. |
| `newMethodName` | Yes | Replacement method name. Used for both read and write contexts. |

**Before / After (write):**

```csharp
// Before
btn.Disabled = true;

// After
btn.SetDisabled(true);
```

**Before / After (read):**

```csharp
// Before
var b = btn.Disabled;

// After
var b = btn.SetDisabled();  // same NewMethodName used in both contexts
```

> **Note — single `NewMethodName` field:** `PropertyToMethodRule` has only one `NewMethodName` field, which the rewriter uses for both read and write contexts. The rewriter dispatches based on whether the property appears on the left-hand side of an assignment (write) or elsewhere (read), but it does NOT pick a different method name per context.
>
> **Need separate getter and setter method names?** Define **two rules** — one with the getter name and one with the setter name — both targeting the same property. The rewriter applies them in schema order:
>
> ```json
> // Rule 1 — handles the write context (assignment LHS)
> {
>   "id": "MUD-010a",
>   "kind": "propertyToMethod",
>   "typeName": "MudBlazor.MudButton",
>   "oldPropertyName": "Disabled",
>   "newMethodName": "SetDisabled"
> },
> // Rule 2 — handles the read context (every other usage)
> {
>   "id": "MUD-010b",
>   "kind": "propertyToMethod",
>   "typeName": "MudBlazor.MudButton",
>   "oldPropertyName": "Disabled",
>   "newMethodName": "GetDisabled"
> }
> ```
>
> Because the rewriter rewrites writes to `Set...(value)` and reads to `Set...()` for a single rule, achieving distinct getter/setter names today requires deciding which name should appear in each context. A future rule kind may add separate `ReadMethodName`/`WriteMethodName` fields.

**Skipped cases:** Compound-assignment expressions (`btn.Disabled++`, `btn.Disabled += 1`) are skipped.

**Confidence guideline:** `auto` when the read→write transformation is a simple substitution. `manual` if side-effects differ between the old property and the new method.

---

### `moveMember`

A static member was relocated from one type to another. The rewriter redirects `OldType.Member` references to `NewType.Member`.

**JSON shape:**

```json
{
  "id": "MUD-011",
  "kind": "moveMember",
  "confidence": "verified",
  "oldTypeName": "MudBlazor.Utilities",
  "newTypeName": "MudBlazor.MudHelpers",
  "memberName": "GetColor"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `oldTypeName` | Yes | Type being moved from (short name or fully-qualified). |
| `newTypeName` | Yes | Type to move to. |
| `memberName` | Yes | Member name (must be the same in both types). |

**Before / After:**

```csharp
// Before
var h = Utilities.GetColor();

// After
var h = MudHelpers.GetColor();
```

**Skipped cases:** Instance calls where the receiver is inferred to be a variable of a different type, and lowercase unresolvable identifiers (assumed to be instance variables), are skipped.

**Confidence guideline:** `verified` if you have confirmed the moved member has identical semantics at the new location. `manual` if the behavior changed.

---

## Rule Confidence

| Value | Applied automatically? | Meaning |
|-------|------------------------|---------|
| `auto` | Yes | The rewriter will apply this rule without prompting. Use when the transformation is syntactically unambiguous and semantically safe. |
| `verified` | Yes | Same as `auto` — applied automatically. The label signals that a human has manually reviewed and approved the rule. |
| `manual` | No | The engine performs a read-only scan to find matching files, but never applies the change. The output shows matched files so developers know where to look. |

When in doubt, prefer `manual` over `auto`. A migration that leaves `manual` annotations is safer than one that applies a wrong automated change.

### Escalation path

The generator assigns initial confidence. You can escalate or de-escalate:

- `auto` → `verified`: You have tested the rule on a representative codebase and it is correct.
- `auto` → `manual`: The rule is risky in your library's context (e.g., a member with a common name appears on many unrelated types).
- `manual` → `auto`: You have added enough type qualification to make the rule safe.

---

## Similarity Tuning for Generation

`migrate generate` uses Jaro-Winkler similarity to detect renames:

| Threshold | Default | Effect |
|-----------|---------|--------|
| `--rename-similarity-threshold` | `0.65` | Minimum similarity for a remove+add pair to be treated as a rename |
| `--verified-similarity-threshold` | `0.85` | Similarity at which confidence is promoted from `auto` to `verified` |

Increase `--rename-similarity-threshold` if the draft schema contains spurious renames (unrelated types with similar names). Decrease it if genuine renames are being emitted as `manual` removals.

```bash
# Stricter rename detection
wrap-god migrate generate \
  --package MudBlazor --from 6.0.0 --to 7.0.0 \
  --rename-similarity-threshold 0.80

# Disable rename detection entirely (every removal becomes manual)
wrap-god migrate generate \
  --package MudBlazor --from 6.0.0 --to 7.0.0 \
  --no-rename-detection
```

---

## The Authoring Workflow

The recommended process for shipping a migration pack:

### Step 1 — Generate a draft

```bash
wrap-god migrate generate \
  --package MyLibrary \
  --from 2.0.0 --to 3.0.0 \
  --rule-id-prefix MYLIB \
  --output mylib.2.0-to-3.0.wrapgod-migration.json
```

Open the output JSON. Expect:
- `auto` rules for straightforward renames and namespace moves
- `manual` rules for removals and complex restructurings

### Step 2 — Review and enrich

Work through each `manual` rule:

- Is the removal actually safe to automate? → change to `auto` and pick the right kind
- Is there a good replacement pattern? → replace the `removeMember` rule with a more specific rule kind
- Is human intervention genuinely required? → write a clear `note` explaining what the developer must do

Promote `auto` rules to `verified` once you have tested them on at least one real consumer project.

### Step 3 — Add hand-authored rules

For B-level structural changes (split methods, extracted parameter objects, moved members), add rules manually. The generator cannot produce these from a diff.

```json
{
  "id": "MYLIB-042",
  "kind": "splitMethod",
  "confidence": "manual",
  "note": "Process() split into ProcessSync() and ProcessAsync(). Choose the appropriate variant.",
  "typeName": "MyLibrary.Processor",
  "oldMethodName": "Process",
  "newMethodNames": ["ProcessSync", "ProcessAsync"]
}
```

### Step 4 — Test on a sample project

Use the synthetic before/after fixture pattern:

1. Create a `Before.cs` with representative usages of the old API.
2. Run `migrate apply --dry-run` and inspect the diff.
3. Verify the diff matches your expected `After.cs`.
4. Commit both fixtures alongside the schema as integration test evidence.

See the [examples directory](../migration/examples/) for a Serilog v2→v3 example that follows this pattern.

### Step 5 — Publish

Distribute the schema alongside your library. Options:

| Distribution method | When to use |
|--------------------|-------------|
| **Standalone file** (GitHub release attachment, documentation page download) | Simplest; works for all consumers |
| **NuGet content package** (content files shipped in the `.nupkg`) | Enables tooling integrations to discover the schema automatically from the package |
| **In-repo** (checked into the library repo, linked from the changelog) | Best for open-source libraries; the schema evolves with the library |

For the NuGet content package approach, include the schema file in your `.csproj`:

```xml
<ItemGroup>
  <Content Include="migration/*.wrapgod-migration.json">
    <PackagePath>contentFiles/any/any/migration/</PackagePath>
  </Content>
</ItemGroup>
```

---

## Common Gotchas

### Ambiguous receivers

`renameMember` and `changeParameter` rely on syntactic receiver-type inference. If your library has members with common names (e.g., `ToString`, `Equals`, `Name`) that also exist on many unrelated types, the rewriter will skip many call sites as ambiguous. To reduce false positives:

- Use `confidence: "manual"` so the rule lists matched files without applying.
- Add a `note` field explaining the disambiguation step.
- Consider using `moveMember` instead when the rename is on a static or extension method.

### Cross-namespace renames

When a type moves AND is renamed simultaneously (e.g., `Acme.Legacy.FooWidget` → `Acme.Modern.BarWidget`), use a single `renameType` rule with the fully-qualified old and new names. Do NOT pair a `renameNamespace` rule with a `renameType` rule for the same type — the sequential rewriter pipeline will apply both rules and produce incorrect output.

### Rule ID stability

Once you publish a schema, never reuse a rule ID — consumers may have state files that reference it. If you retire a rule, delete it from the `rules` array but keep a comment or changelog noting what it was.

### Rule ordering

Rules are applied in the order they appear in the `rules` array. Place more-specific rules (targeting a specific type) before broader rules (targeting all types in a namespace) to avoid the broader rule interfering with the specific one.

### Parameter position indices

`addRequiredParameter` uses zero-based `position` indices. Verify the position against the new method signature, not the old one.

---

## Testing Your Schema

### Dry-run verification

```bash
wrap-god migrate apply \
  --schema mylib.2.0-to-3.0.wrapgod-migration.json \
  --project-dir ./test-project \
  --dry-run
```

Review the diff output. Check that:
- Expected files appear in the modified list
- `SkippedRewrite` entries are expected (not unexpected false-negatives)
- `manual` rule matches reference the correct files

### Synthetic fixture pattern

The integration tests in `WrapGod.Tests` use a pattern of paired `Before.cs` / `After.cs` fixtures. You can replicate this:

1. Create a `fixtures/before/` folder with representative old API usage.
2. Run `migrate apply` targeting that folder (save a copy first).
3. Diff the result against a `fixtures/after/` folder you pre-authored.
4. Any deviation is a bug in your schema.

### Docs coverage test

The `WrapGod.Tests.MigrationDocsCoverageTests` class includes tests that verify every `MigrationRuleKind` enum value appears in `authoring.md` — catching schema-model drift early. If you add a new rule kind to the engine, add the corresponding section to this guide.

---

## See Also

- [Migration Schema Reference](schema.md) — canonical JSON format and serialization API
- [Schema Generation](schema-generation.md) — `MigrationSchemaGenerator.FromDiff` API
- [Rewriters](rewriters.md) — how each rule kind is implemented by the engine
- [Applying Migrations](applying.md) — consumer workflow
- [Migration Engine](engine.md) — engine architecture and extension points
- [CLI Reference: migrate generate](../guide/cli.md#migrate-generate) — full flag reference
- [Back to Migration index](./index.md)
