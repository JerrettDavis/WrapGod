# Rewriters

`WrapGod.Migration.Engine` ships eleven concrete `IRuleRewriter` implementations
organized in two levels. All rewriters use syntax-only analysis (no `SemanticModel`
required) and can safely operate on code that does not compile.

## Rewriter Catalogue

### A-Level (Syntactic)

| Class | `Kind` | Rule type | What it transforms |
|---|---|---|---|
| `RenameTypeRewriter` | `renameType` | `RenameTypeRule` | Identifier nodes, qualified names |
| `RenameNamespaceRewriter` | `renameNamespace` | `RenameNamespaceRule` | `using` directives, qualified names |
| `RenameMemberRewriter` | `renameMember` | `RenameMemberRule` | Member-access expressions |
| `ChangeParameterRewriter` | `changeParameter` | `ChangeParameterRule` | Named-argument labels in invocations |
| `RemoveMemberRewriter` | `removeMember` | `RemoveMemberRule` | Call-site expression statements |
| `AddRequiredParameterRewriter` | `addRequiredParameter` | `AddRequiredParameterRule` | Argument lists |
| `ChangeTypeReferenceRewriter` | `changeTypeReference` | `ChangeTypeReferenceRule` | Type-position syntax nodes |

### B-Level (Structural)

| Class | `Kind` | Rule type | What it transforms |
|---|---|---|---|
| `SplitMethodRewriter` | `splitMethod` | `SplitMethodRule` | One call → N sequential statements with MIGRATION comment |
| `ExtractParameterObjectRewriter` | `extractParameterObject` | `ExtractParameterObjectRule` | Named args → `new OptionsType { … }` argument |
| `PropertyToMethodRewriter` | `propertyToMethod` | `PropertyToMethodRule` | Property reads/writes → method invocations |
| `MoveMemberRewriter` | `moveMember` | `MoveMemberRule` | Static-style `OldType.Member` → `NewType.Member` |

---

## Universal Contracts

Every rewriter guarantees:

- **Trivia preserved.** Every replacement node copies trivia from the original via
  `WithTriviaFrom` or `TriviaPreservation.WithReplacedToken`. Whitespace and comments
  are never corrupted.
- **Return `null` on no-match.** When nothing in the tree matches the rule, the
  rewriter returns `null` so the orchestrator can skip file-write.
- **Ambiguous → `SkippedRewrite`.** When a match is syntactically uncertain, a
  `SkippedRewrite` entry is recorded on the `RewriteContext` and the node is left
  unchanged. Wrong rewrites are never applied.
- **No `SemanticModel`.** All rewriters work on the raw syntax tree, so they handle
  broken or partially-compiled code.

---

## RenameTypeRewriter

Renames a type by its short name (unqualified identifier) wherever it appears in
identifier positions and as fully-qualified names.

```csharp
// Rule:
//   OldName = "Foo.OldWidget"
//   NewName = "Foo.NewWidget"

// Before
Foo.OldWidget w = new Foo.OldWidget();

// After
Foo.NewWidget w = new Foo.NewWidget();
```

**Ambiguity:** Generic arguments and identifiers that are part of the right-hand side
of a member access are NOT rewritten by this rewriter (the parent qualified name
handles them).

---

## RenameNamespaceRewriter

Rewrites `using` directives and qualified names whose namespace starts with
`OldNamespace`. Only exact prefix matches (followed by `.` or end-of-name) are
updated; unrelated identifiers that share a prefix are left alone.

```csharp
// Rule:
//   OldNamespace = "Acme.Legacy"
//   NewNamespace = "Acme.Modern"

// Before
using Acme.Legacy.Widgets;

// After
using Acme.Modern.Widgets;
```

---

## RenameMemberRewriter

Renames a method or property on a specific declaring type at every `member.Access`
expression. The receiver type is inferred via syntax-only heuristics (local variable
declarations and parameter types visible in the enclosing method body). When the
receiver type cannot be determined, a `SkippedRewrite` is recorded.

```csharp
// Rule:
//   TypeName = "MyService"
//   OldMemberName = "Execute"
//   NewMemberName = "Run"

// Before
MyService svc = new MyService();
svc.Execute();

// After
MyService svc = new MyService();
svc.Run();
```

**Receiver inference scope:** searches in order — (1) local variables and parameters in
the enclosing method/constructor/property body, (2) field and property declarations on
the enclosing type, (3) the receiver identifier itself as a type-of-name reference (for
static class member calls). Falls back to `SkippedRewrite` when the receiver is a
chained-call result or any expression whose type cannot be inferred syntactically.

---

## ChangeParameterRewriter

Rewrites the named-argument label of a parameter that was renamed. Also performs
**receiver-type disambiguation** — when the receiver type can be inferred (via
`RewriterHelpers.TryInferReceiverTypeName`) and does not match the rule's declaring
type, the invocation is skipped to avoid rewriting unrelated methods that share a name.
For parameter **type** changes on positional arguments, a `SkippedRewrite` with reason
`"type change requires semantic conversion"` is recorded because a syntax-only
rewriter cannot safely convert argument values.

```csharp
// Rule:
//   TypeName = "Builder"
//   MethodName = "Build"
//   OldParameterName = "size"
//   NewParameterName = "buttonSize"

// Before
builder.Build(size: 42);

// After
builder.Build(buttonSize: 42);
```

---

## RemoveMemberRewriter

When a member has been completely removed from the API, every call site is removed
entirely and its location is annotated with a comment that preserves the original text
for manual inspection. The comment is attached as trivia to the next sibling statement
(or the closing brace if the removed call was the last statement), so no orphan `;`
is left in the output. Each removal is recorded as an `AppliedRewrite`.

```csharp
// Rule:
//   MemberName = "Deprecated"
//   Note = "Use NewApi.Process() instead."

// Before
void M(OldApi obj)
{
    obj.Deprecated();
    var x = 1;
}

// After
void M(OldApi obj)
{
    // MIGRATION: DEL-001 removed — Use NewApi.Process() instead.: obj.Deprecated();
    var x = 1;
}
```

---

## AddRequiredParameterRewriter

Inserts a placeholder expression at the specified zero-based position in every matching
invocation's argument list, annotated with a `TODO MIGRATION` comment so developers
know to supply a real value. The placeholder is `null` when the parameter type is
syntactically a reference type (nullable annotation, array, `string`/`object`, or an
interface following the `I + UpperCamelCase` convention) and `default` otherwise. The
insertion is recorded as an `AppliedRewrite`.

```csharp
// Rule:
//   MethodName = "Apply"
//   ParameterName = "theme"
//   ParameterType = "MudTheme"
//   Position = 0

// Before
provider.Apply();

// After
provider.Apply( default /* TODO MIGRATION: ARP-001 required arg 'theme' (MudTheme) added */);
```

---

## ChangeTypeReferenceRewriter

Replaces a type reference (by short name or fully-qualified name) wherever it appears
in a syntactic **type position**: field declarations, method return types, parameter
types, cast expressions, `typeof()`, generic type arguments, base lists, and object
creation expressions. The rewriter also handles generic type names (e.g., `IList<T>`).

```csharp
// Rule:
//   OldType = "IList"
//   NewType = "IReadOnlyList"

// Before
IList<string> items;
var t = typeof(IList<int>);

// After
IReadOnlyList<string> items;
var t = typeof(IReadOnlyList<int>);
```

---

## B-Level Structural Rewriters

B-level rewriters handle structural breaking changes that go beyond simple identifier
replacement. They share the same contracts as A-level rewriters but reason about the
surrounding statement or expression context.

---

### SplitMethodRewriter

One call site is replaced with multiple sequential calls, with the original call
commented-out as a `MIGRATION` annotation.

**Statement context only.** Calls whose return value is consumed (e.g., `var x = Foo()`)
or chained invocations (e.g., `Foo().Bar()`) are skipped with a descriptive reason.

```csharp
// Rule:
//   TypeName = "Panel"
//   OldMethodName = "Refresh"
//   NewMethodNames = ["RefreshLayout", "RefreshContent"]

// Before
panel.Refresh();

// After
// MIGRATION: SM-001 split-method — original: panel.Refresh();
panel.RefreshLayout();
panel.RefreshContent();
```

**Skipped cases:**

```csharp
// Return value consumed → SkippedRewrite
var html = panel.Refresh();

// Chained call → SkippedRewrite
panel.Refresh().ToString();
```

---

### ExtractParameterObjectRewriter

Collapses a set of individually-named arguments into a new parameter object literal.
Named arguments matching the rule's `ExtractedParameters` list are extracted into an
object initializer. Non-extracted arguments are preserved in the argument list alongside
the new object. Positional-only calls where arguments cannot be unambiguously mapped are
skipped.

```csharp
// Rule:
//   TypeName = "Dialog"
//   MethodName = "Show"
//   ParameterObjectType = "ShowOptions"
//   ExtractedParameters = ["title", "message"]

// Before
dlg.Show(title: "Hello", message: "World");

// After
dlg.Show(new ShowOptions { Title = "Hello", Message = "World" });
```

Extra arguments not in `ExtractedParameters` are preserved:

```csharp
// Before
dlg.Show(title: "Hello", message: "World", callback: cb);

// After
dlg.Show(callback: cb, new ShowOptions { Title = "Hello", Message = "World" });
```

**Skipped cases:**

```csharp
// More positional args than extracted params → SkippedRewrite
dlg.Show("Hello", "World", cb);
```

---

### PropertyToMethodRewriter

Property reads become no-argument method calls; property writes become single-argument
method calls. The `NewMethodName` in the rule is used verbatim for both contexts.

```csharp
// Rule:
//   TypeName = "Button"
//   OldPropertyName = "Disabled"
//   NewMethodName = "SetDisabled"

// Write context: Before
btn.Disabled = true;

// Write context: After
btn.SetDisabled(true);

// Read context (with NewMethodName = "GetDisabled")
// Before
var b = btn.Disabled;

// After
var b = btn.GetDisabled();
```

**Skipped cases:**

```csharp
// Compound-assignment → SkippedRewrite
btn.Disabled++;
btn.Disabled += 1;
```

---

### MoveMemberRewriter

Redirects a static-style member access from one type to another. The receiver must match
the old type's short name syntactically (or the fully-qualified old type name). Instance
calls where the receiver is inferred to be a variable of a different type are skipped.

```csharp
// Rule:
//   OldTypeName = "LegacyHelper"
//   NewTypeName = "ModernHelper"
//   MemberName = "ComputeHash"

// Before
var h = LegacyHelper.ComputeHash();

// After
var h = ModernHelper.ComputeHash();
```

**Skipped cases:**

```csharp
// Receiver inferred as a different type → SkippedRewrite
SomeOtherType legacyHelper = ...;
legacyHelper.ComputeHash();    // skipped: instance call cannot be moved syntactically

// Lowercase unresolvable identifier → SkippedRewrite (assumed instance variable)
legacyHelper.ComputeHash();    // skipped
```

---

## See Also

- [Migration Engine](engine.md) — scaffold contracts (`IRuleRewriter`, `RewriteContext`, `MigrationResult`)
- [Migration Schema](schema.md) — rule kinds and JSON schema reference
