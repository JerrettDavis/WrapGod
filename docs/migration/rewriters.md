# A-Level Syntax Rewriters

`WrapGod.Migration.Engine` ships seven concrete `IRuleRewriter` implementations that
handle the most common breaking changes using syntax-only analysis (no `SemanticModel`
required). Each rewriter handles one `MigrationRuleKind` and can safely operate on
code that does not compile.

## Rewriter Catalogue

| Class | `Kind` | Rule type | What it transforms |
|---|---|---|---|
| `RenameTypeRewriter` | `renameType` | `RenameTypeRule` | Identifier nodes, qualified names |
| `RenameNamespaceRewriter` | `renameNamespace` | `RenameNamespaceRule` | `using` directives, qualified names |
| `RenameMemberRewriter` | `renameMember` | `RenameMemberRule` | Member-access expressions |
| `ChangeParameterRewriter` | `changeParameter` | `ChangeParameterRule` | Named-argument labels in invocations |
| `RemoveMemberRewriter` | `removeMember` | `RemoveMemberRule` | Call-site expression statements |
| `AddRequiredParameterRewriter` | `addRequiredParameter` | `AddRequiredParameterRule` | Argument lists |
| `ChangeTypeReferenceRewriter` | `changeTypeReference` | `ChangeTypeReferenceRule` | Type-position syntax nodes |

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

**Ambiguity heuristic:** If the receiver is declared as `object`, a generic parameter,
or an interface the rewriter cannot resolve, a `SkippedRewrite` with reason
`"ambiguous: cannot determine receiver type"` is recorded and the call site is left
unchanged for manual review.

---

## ChangeParameterRewriter

Rewrites the named-argument label of a parameter that was renamed. For parameter
**type** changes on positional arguments, a `SkippedRewrite` with reason
`"type change requires semantic conversion"` is recorded because a syntax-only
rewriter cannot safely convert argument values.

```csharp
// Rule:
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

When a member has been completely removed from the API, every call site is replaced
with a comment that preserves the original text for manual inspection. The replacement
is recorded as an `AppliedRewrite` so the audit trail shows where human action is
needed.

```csharp
// Rule:
//   MemberName = "Deprecated"
//   Note = "Use NewApi.Process() instead."

// Before
obj.Deprecated();

// After
// MIGRATION: DEL-001 removed — Use NewApi.Process() instead.: obj.Deprecated();
```

---

## AddRequiredParameterRewriter

Inserts a `default` expression at the specified zero-based position in every matching
invocation's argument list, annotated with a `TODO MIGRATION` comment so developers
know to supply a real value. The insertion is recorded as an `AppliedRewrite`.

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

## See Also

- [Migration Engine](engine.md) — scaffold contracts (`IRuleRewriter`, `RewriteContext`, `MigrationResult`)
- [Migration Schema](schema.md) — rule kinds and JSON schema reference
