# Migration Playbook

A practical guide to planning and executing library migrations with WrapGod.

## Table of contents

1. [Choosing a migration strategy](#choosing-a-migration-strategy)
2. [Authoring mappings](#authoring-mappings)
3. [Running the pipeline](#running-the-pipeline)
4. [Safety model](#safety-model)
5. [Version divergence handling](#version-divergence-handling)
6. [Validation checklist](#validation-checklist)
7. [Troubleshooting FAQ](#troubleshooting-faq)

---

## Choosing a migration strategy

WrapGod supports three compatibility modes. Choose the one that matches your
migration scenario:

### LCD (Lowest Common Denominator)

Generate wrappers that work across **all** extracted versions. Only members
present in every version appear in the wrapper interface.

**When to use:** You maintain a library consumed by projects targeting different
versions of a dependency, and you need a single wrapper that works everywhere.

### Targeted

Generate wrappers for a **single specific version**. The wrapper surface matches
that version exactly.

**When to use:** You are migrating from one library version to another (or to a
different library entirely) and only need to support the target version.

### Adaptive

Generate wrappers with **runtime version guards**. Members present in some but
not all versions are conditionally available, with the facade checking at
runtime which version is loaded.

**When to use:** You need maximum API coverage while supporting multiple
versions simultaneously during a gradual rollout.

---

## Authoring mappings

WrapGod supports three configuration surfaces for defining how source types and
members map to target equivalents. They can be combined with configurable merge
precedence.

### JSON configuration files

Create a `*.wrapgod.config.json` file:

```json
{
  "migrationName": "LibA-to-LibB",
  "sourceLibrary": "LibA",
  "targetLibrary": "LibB",
  "mappings": [
    {
      "source": ".MethodA({args})",
      "target": ".MethodB({args})",
      "safety": "safe"
    }
  ]
}
```

**Strengths:** Portable, version-controllable, works without code changes.

### C# attributes

Annotate types and members with `[WrapType]` and `[WrapMember]`:

```csharp
[WrapType(typeof(SourceLib.Client))]
public interface IWrappedClient
{
    [WrapMember(nameof(SourceLib.Client.Send))]
    void Transmit(string data);
}
```

**Strengths:** Co-located with code, compiler-verified, refactoring-safe.

### Fluent DSL

Use the fluent configuration API:

```csharp
WrapGodConfig.For<SourceLib.Client>()
    .MapMember(c => c.Send, "Transmit")
    .WithSafety(Safety.Safe);
```

**Strengths:** Strongly typed, IDE-friendly, composable.

### Merge precedence

When multiple surfaces define mappings for the same member, precedence
determines which wins (default order):

1. Attributes (highest)
2. Fluent DSL
3. JSON config (lowest)

Override in config: `"mergePrecedence": ["json", "fluent", "attributes"]`

---

## Running the pipeline

A typical WrapGod migration follows four stages:

### 1. Extract

Produce a manifest from the source assembly:

```bash
wrap-god extract path/to/Source.dll --output manifest.wrapgod.json
```

Or programmatically:

```csharp
var manifest = AssemblyExtractor.Extract("path/to/Source.dll");
```

The manifest captures every public type and member with stable identifiers.

### 2. Configure

Author your mapping rules (JSON, attributes, or fluent DSL) defining how
source APIs translate to target APIs. Mark each mapping with a safety level.

### 3. Generate

Add the manifest and WrapGod.Generator to your project:

```xml
<ItemGroup>
  <AdditionalFiles Include="manifest.wrapgod.json" />
</ItemGroup>
```

Build to generate wrapper interfaces and facade classes automatically via the
Roslyn incremental source generator.

### 4. Analyze and fix

Add WrapGod.Analyzers to detect direct usage of wrapped types:

- **WG2001**: Direct use of a wrapped type (should use `IWrapped{Type}`)
- **WG2002**: Direct call to a wrapped member (should use facade)

Code fixes are available for safe mappings. Guided mappings produce warnings
with suggested rewrites.

---

## Safety model

Every mapping carries a safety classification that determines how WrapGod
handles it:

### Safe (auto-fix)

Direct 1:1 replacements where the semantics are identical. WrapGod applies
these automatically via code fixes.

Examples:
- `It.IsAny<T>()` -> `Arg.Any<T>()`
- `.Should().BeTrue()` -> `.ShouldBeTrue()`

### Guided (semi-automatic)

Structural rewrites where the transformation is well-defined but requires
verification. WrapGod flags these as warnings and suggests the rewrite.

Examples:
- `mock.Setup(x => x.Foo()).Returns(bar)` -> `sub.Foo().Returns(bar)`
- `act.Should().Throw<T>()` -> `Should.Throw<T>(act)`

### Manual (human required)

Patterns with no safe automated equivalent. WrapGod reports a diagnostic but
does not offer a code fix.

Examples:
- `mock.VerifyAll()` (no NSubstitute equivalent)
- `Arg.Do<T>()` (no Moq equivalent)

---

## Version divergence handling

When migrating between versions of the same library (or libraries that have
evolved), WrapGod provides tools for managing API differences:

### Multi-version extraction

```csharp
var manifests = new[]
{
    AssemblyExtractor.Extract("Source.v1.dll"),
    AssemblyExtractor.Extract("Source.v2.dll"),
};
var merged = MultiVersionExtractor.Merge(manifests);
```

The merged manifest annotates each type and member with version presence
metadata (`IntroducedIn`, `RemovedIn`, `ChangedIn`).

### Breaking change detection

Members that exist in one version but not another are flagged. The compatibility
mode you choose determines how these are handled:

- **LCD**: Omitted from the wrapper (safe but limited)
- **Targeted**: Present based on the chosen version
- **Adaptive**: Wrapped with runtime guards

### Migration config versioning

Pin your config to specific source/target versions:

```json
{
  "sourceVersion": "4.20.72",
  "targetVersion": "5.3.0",
  "mappings": [...]
}
```

---

## Validation checklist

Before considering a migration complete:

- [ ] All example projects build (`dotnet build`)
- [ ] All tests pass (`dotnet test`)
- [ ] No WG2001/WG2002 diagnostics remain (or are explicitly suppressed)
- [ ] All `guided` mappings have been reviewed by a human
- [ ] All `manual` mappings have been addressed with handwritten code
- [ ] Config matches the exact source and target library versions in use
- [ ] README or migration notes document any behavioral differences
- [ ] CI pipeline validates the migrated projects build and test successfully
- [ ] Integration or smoke tests exercise the migrated code paths
- [ ] Rollback plan documented (keep both dependencies until verified)

---

## Troubleshooting FAQ

### The extractor fails with "Assembly file not found"

Ensure you are pointing to the compiled DLL, not the NuGet package or project
file. The assembly must exist on disk. Build the project first if needed.

### The generator does not emit any files

Check that:
1. The manifest file is included as `<AdditionalFiles>` (not `<None>` or `<Content>`)
2. The manifest file extension is `.wrapgod.json`
3. WrapGod.Generator is referenced as an analyzer: the `.csproj` should include
   `OutputItemType="Analyzer"` on the project reference

### Analyzer reports WG2001 but I want to use the original type

Suppress the diagnostic for intentional direct usage:

```csharp
#pragma warning disable WG2001
var client = new OriginalClient();
#pragma warning restore WG2001
```

Or in `.editorconfig`:
```
[*.cs]
dotnet_diagnostic.WG2001.severity = none
```

### Config mappings are not being applied

Check merge precedence. If attributes define a mapping for the same member,
they take priority over JSON config by default. Either remove the attribute
or adjust `mergePrecedence` in your config.

### Guided mapping produces unexpected code

Guided mappings are suggestions, not guaranteed-correct transformations. Always
review the generated code. If the suggestion is wrong, mark the mapping as
`manual` and write the replacement by hand.

### Build succeeds but tests fail after migration

Common causes:
1. **Behavioral differences** between source and target libraries (e.g.,
   Moq strict mode vs NSubstitute's default leniency)
2. **Argument matcher semantics** that differ subtly between frameworks
3. **Exception types** that changed between libraries
4. **Ordering guarantees** that one library provides but the other does not

Review the `notes` field in your migration config for known behavioral
differences.
