# Automation: Eliminating Vendor-Upgrade Tech Debt

WrapGod isn't just a code generator -- it's an automation pipeline that
turns vendor upgrades from multi-sprint projects into single-commit changes.

---

## The Problem: Vendor Upgrades Are Expensive

Picture this: your organization has 30 microservices, all using
Newtonsoft.Json 12. Version 13 ships with breaking changes to the
serialization API. Without an abstraction layer, every service needs manual
changes wherever the API surface drifted. Someone files a ticket, estimates
two sprints, and the upgrade languishes in the backlog for a year.

This isn't hypothetical. It's the default state of most codebases. The
vendor's API becomes load-bearing infrastructure, and upgrading means
touching every call site in every service.

---

## The WrapGod Automation Pipeline

WrapGod inserts a generated abstraction layer between your code and the
vendor. The entire pipeline runs at build time with no manual intervention:

```
Restore → Extract → Generate → Compile → Analyze → Fix
```

| Stage       | What Happens                                                    |
|-------------|-----------------------------------------------------------------|
| **Restore** | MSBuild resolves the NuGet package to a local path              |
| **Extract** | The extractor scans the assembly and produces a JSON manifest    |
| **Generate**| The Roslyn source generator emits `IWrapped*` interfaces and `*Facade` classes |
| **Compile** | Your project compiles against generated types, not vendor types |
| **Analyze** | Analyzers flag any direct vendor usage that bypasses wrappers   |
| **Fix**     | Code fixes offer one-click migration to the generated types     |

Every stage is incremental. If the vendor assembly hasn't changed, extraction
is skipped. If the manifest hasn't changed, generation is skipped.

---

## Setting Up Zero-Touch Automation

Two packages, a few lines of MSBuild, and you never think about it again.

### 1. Add WrapGod packages

```bash
dotnet add package WrapGod.Targets
dotnet add package WrapGod.Analyzers
```

### 2. Declare what to wrap

In your `.csproj`:

```xml
<ItemGroup>
  <WrapGodPackage Include="Newtonsoft.Json" />
</ItemGroup>
```

### 3. Build

```bash
dotnet build
```

The targets automatically restore, extract, and feed the manifest to the
source generator. Your project now compiles against `IWrappedJsonConvert`
and `JsonConvertFacade` instead of `Newtonsoft.Json.JsonConvert` directly.

### 4. Migrate existing call sites

```bash
dotnet format analyzers --diagnostics WG2001 WG2002
```

The WG2001 and WG2002 diagnostics catch direct vendor type usage and
replace it with the generated wrappers. One command, entire project migrated.

---

## The Upgrade Story

Here's what a vendor upgrade looks like with WrapGod in place.

### Current state

Your `.csproj` references Newtonsoft.Json 12. WrapGod has already generated
wrappers, and all your code programs against `IWrappedJsonConvert`.

### Step 1: Bump the version

```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

### Step 2: Build

```bash
dotnet build
```

WrapGod re-extracts the manifest from v13, regenerates the interfaces and
facades, and compiles.

### If the API is compatible

Build succeeds. You're done. Ship it. The wrapper absorbed the version
change because the public API surface your code depends on didn't drift.

### If there are breaking changes

The analyzer flags the deltas. You see diagnostics like:

```
WG2001: 'JsonConvert.DeserializeObject<T>(string)' signature changed --
        parameter 'settings' added in v13. Update wrapper usage.
```

Code fixes offer migration. The blast radius is contained to the generated
layer, not scattered across 30 services.

---

## CI Integration

Wire WrapGod into your CI pipeline to enforce wrapper discipline at the
gate.

### Analyze as a build step

```yaml
- name: WrapGod Analysis
  run: dotnet wrap-god analyze manifest.wrapgod.json --warnings-as-errors
```

### Coverage gate

Use WG2001/WG2002 diagnostics as build-breakers. Any PR that introduces
direct vendor usage fails the check:

```xml
<PropertyGroup>
  <WarningsAsErrors>WG2001;WG2002</WarningsAsErrors>
</PropertyGroup>
```

### PR checks for new direct vendor usage

The analyzer catches new call sites that bypass the wrapper. Developers get
immediate feedback, and the code fix is one click away in their IDE.

---

## The Economics

Be honest about the math.

**Without WrapGod:**

- 30 services x 15 breaking changes x 2 hours per change = **900 developer-hours**
- Plus testing, review, and rollback risk across every service
- Multiply by every vendor upgrade, forever

**With WrapGod:**

- Change one version number in one place
- Rebuild -- WrapGod re-extracts, regenerates, recompiles
- If compatible: done in minutes
- If breaking: analyzer tells you exactly what changed and where
- The wrapper is generated -- there is nothing to maintain

Your requirements didn't change. The vendor's API changed. WrapGod absorbs
that delta so your code doesn't have to.

---

## Compatibility Modes for Version Strategy

When wrapping multiple versions, WrapGod offers three compatibility modes
that control what gets generated:

| Mode         | Emits                            | Use When                                      |
|--------------|----------------------------------|-----------------------------------------------|
| **LCD**      | Only members present in ALL versions | Shipping a library that must work against any supported version |
| **Targeted** | Members present in a specific version | Pinned to one version, want full API surface  |
| **Adaptive** | Union of all versions, with runtime version checks | Supporting multiple versions simultaneously   |

Configure in your `wrapgod.config.json`:

```json
{
  "compatibilityMode": "lcd"
}
```

See [COMPATIBILITY.md](COMPATIBILITY.md) for the full reference.

---

## Real Example: The Company.Assertions Case Study

The `examples/case-studies/company-assertions-migration/` directory contains
a complete, buildable example of WrapGod automation at enterprise scale.

### The scenario

Four legacy applications, each using different assertion libraries:

| App        | Before                              |
|------------|-------------------------------------|
| LegacyApp.A | FluentAssertions 6.12.0           |
| LegacyApp.B | FluentAssertions 8.3.0 + Shouldly 4.3.0 (mixed!) |
| LegacyApp.C | Shouldly 4.1.0                    |
| LegacyApp.D | FluentAssertions 5.10.3 + custom helpers |

### What WrapGod built

A single `Company.Assertions` wrapper package backed by Shouldly 4.3.0.
WrapGod extracted the Shouldly manifest, generated facade types under the
`Company.Assertions` namespace, and every app migrated to one import:

```csharp
using Company.Assertions;

result.ShouldBe(42);
Should.Throw<Exception>(act);
```

### The result

| App        | After                              |
|------------|------------------------------------|
| Migrated.App.A | `Company.Assertions` (project ref) |
| Migrated.App.B | `Company.Assertions` (project ref) |
| Migrated.App.C | `Company.Assertions` (project ref) |
| Migrated.App.D | `Company.Assertions` (project ref) |

Four divergent assertion stacks unified into one generated facade. 76 tests
pass. Zero direct references to FluentAssertions or Shouldly in consumer
code. Future assertion library changes touch exactly one project.

See the full [MIGRATION-GUIDE.md](../examples/case-studies/company-assertions-migration/MIGRATION-GUIDE.md)
for the step-by-step walkthrough.
