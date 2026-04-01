# WrapGod Examples

Runnable example projects demonstrating every WrapGod capability -- from basic extraction to enterprise-scale library migrations.

## Which Example Should I Start With?

| If you want to... | Start here |
|---|---|
| Understand the core WrapGod pipeline | [BasicExample](#basicexample) |
| See the full automated workflow (extract → generate → analyze → fix) | [WorkflowDemo](#workflowdemo) |
| Upgrade a library to a new major version | [Version-Matrix Packs](#version-matrix-migrations) |
| Switch from one library to another | [Bidirectional Migration Packs](#bidirectional-library-swaps) |
| Roll out WrapGod across an organization | [Company.Assertions Case Study](#enterprise-case-studies) |

---

## Getting Started

### BasicExample

**Path:** [`BasicExample/`](BasicExample/)

Demonstrates the core WrapGod pipeline: extracting an API manifest from a third-party library and generating type-safe wrappers. This is the minimal "hello world" for WrapGod.

**Key takeaway:** One `extract` command produces a manifest; one `dotnet build` produces wrappers.

### WorkflowDemo

**Path:** [`WrapGod.WorkflowDemo/`](WrapGod.WorkflowDemo/)

An end-to-end console app that executes the full WrapGod flow against the included [`Acme.Lib`](Acme.Lib/) sample library:

1. **Extract** -- builds Acme.Lib and extracts `acme.wrapgod.json`
2. **Config** -- writes `acme.wrapgod.config.json`
3. **Generate** -- runs the `WrapGodIncrementalGenerator`
4. **Analyze** -- reports `WG2001`/`WG2002` diagnostics
5. **Fix** -- applies `UseWrapperCodeFixProvider` for `WG2001`

All artifacts are written to `examples/output/`.

**Key takeaway:** Shows how extract, generate, analyze, and fix chain together in a single automated pipeline.

### Acme.Lib

**Path:** [`Acme.Lib/`](Acme.Lib/)

A tiny sample library used as the extraction target by BasicExample and WorkflowDemo. Not a standalone example -- it exists to support the others.

---

## Version-Matrix Migrations

**Path:** [`migrations/nuget-version-matrix/`](migrations/nuget-version-matrix/)

These packs demonstrate how WrapGod handles **major version upgrades** of the same NuGet package. Each pack extracts multiple versions, merges them into a single manifest, and reports breaking changes with compatibility matrices.

| Pack | Versions | What it shows |
|------|----------|--------------|
| [FluentAssertions](migrations/nuget-version-matrix/fluent-assertions/) | 6.x → 7.x | Breaking API removals between major versions |
| [Moq](migrations/nuget-version-matrix/moq/) | 4.x series | Subtle API surface changes across point releases |
| [Serilog](migrations/nuget-version-matrix/serilog/) | 3.x → 4.x | Configuration API restructuring |
| [MediatR](migrations/nuget-version-matrix/mediatr/) | 11.x → 12.x | Handler registration pattern changes |

**Key takeaway:** Multi-version extraction catches breaking changes that would otherwise surface as runtime failures after a `dotnet update`.

---

## Bidirectional Library Swaps

These packs demonstrate migrating **between two different libraries** that serve the same purpose. Each pack includes both directions (A→B and B→A) with parity tests proving behavioral equivalence.

### Mocking Frameworks

| Pack | Direction | Path |
|------|-----------|------|
| [Moq → NSubstitute](MoqToNSubstitute/) | Forward | `MoqToNSubstitute/` |
| [NSubstitute → Moq](NSubstituteToMoq/) | Reverse | `NSubstituteToMoq/` |

**Key takeaway:** WrapGod translates mock setup patterns (`Setup`/`Returns` ↔ `Arg.Any`/`Returns`) while preserving test intent.

### Test Frameworks

| Pack | Direction | Path |
|------|-----------|------|
| [xUnit → NUnit](XUnitToNUnit/) | Forward | `XUnitToNUnit/` |
| [NUnit → xUnit](NUnitToXUnit/) | Reverse | `NUnitToXUnit/` |

**Key takeaway:** Attribute mappings (`[Fact]` ↔ `[Test]`), assertion translations, and lifecycle differences are all captured in the wrapper layer.

### Assertion Libraries

| Pack | Direction | Path |
|------|-----------|------|
| [FluentAssertions → Shouldly](FluentAssertionsToShouldly/) | Forward | `FluentAssertionsToShouldly/` |

**Key takeaway:** Fluent chain syntax (`.Should().Be()`) maps to extension-method syntax (`.ShouldBe()`) through generated wrappers.

### Logging

| Pack | Direction | Path |
|------|-----------|------|
| [Serilog ↔ NLog](migrations/serilog-nlog-bidirectional/) | Bidirectional | `migrations/serilog-nlog-bidirectional/` |

Covers structured-property behavior and sink/target equivalence with parity tests.

**Key takeaway:** Structured logging semantics (message templates, property enrichment) are preserved across frameworks.

### Object Mapping

| Pack | Direction | Path |
|------|-----------|------|
| [AutoMapper ↔ Mapster](migrations/automapper-mapster-bidirectional/) | Bidirectional | `migrations/automapper-mapster-bidirectional/` |

Covers profile-based mapping configurations and object-graph parity tests.

**Key takeaway:** Complex mapping profiles (nested objects, custom resolvers) translate cleanly between mapping libraries.

### Data Access

| Pack | Direction | Path |
|------|-----------|------|
| [EF Core ↔ Dapper](migrations/efcore-dapper-bidirectional/) | Bidirectional | `migrations/efcore-dapper-bidirectional/` |

Demonstrates service-boundary migration patterns -- not a 1:1 API swap, but a pragmatic approach to moving between ORMs at the repository boundary.

**Key takeaway:** WrapGod wraps at the service boundary, letting teams migrate one repository method at a time.

### Mediator / Message Dispatch

| Pack | Direction | Path |
|------|-----------|------|
| [MediatR ↔ MassTransit Mediator](migrations/mediatr-masstransit-mediator-bidirectional/) | Bidirectional | `migrations/mediatr-masstransit-mediator-bidirectional/` |

Covers request/notification/pipeline behavior parity between in-process mediator implementations.

**Key takeaway:** Handler registration and pipeline behavior (pre/post processors) map between mediator libraries.

### Job Scheduling

| Pack | Direction | Path |
|------|-----------|------|
| [Hangfire ↔ Quartz.NET](migrations/hangfire-quartz-bidirectional/) | Bidirectional | `migrations/hangfire-quartz-bidirectional/` |

Covers fire-and-forget, delayed, recurring, and cron-based job scheduling across both frameworks.

**Key takeaway:** Scheduling semantics (cron expressions, job persistence, retry policies) are normalized through the wrapper layer.

---

## Enterprise Case Studies

### Company.Assertions Migration

**Path:** [`case-studies/company-assertions-migration/`](case-studies/company-assertions-migration/)

A realistic enterprise scenario: four legacy applications each using different assertion libraries (FluentAssertions v5, v6, v8; Shouldly 4.1, 4.3; or a mix) consolidate onto a single `Company.Assertions` wrapper backed by Shouldly.

| App | Before | After |
|-----|--------|-------|
| LegacyApp.A | FluentAssertions 6.12.0 | Company.Assertions |
| LegacyApp.B | FluentAssertions 8.3.0 + Shouldly 4.3.0 (mixed) | Company.Assertions |
| LegacyApp.C | Shouldly 4.1.0 | Company.Assertions |
| LegacyApp.D | FluentAssertions 5.10.3 + custom helpers | Company.Assertions |

Includes a full [migration guide](case-studies/company-assertions-migration/MIGRATION-GUIDE.md) with before/after project structures.

**Key takeaway:** WrapGod scales to org-wide rollouts -- extract once, configure the wrapper package, then migrate each app independently against the same interface.

---

## Complete Example Index

| Directory | Category | Description |
|-----------|----------|-------------|
| `Acme.Lib/` | Support | Sample library for BasicExample and WorkflowDemo |
| `BasicExample/` | Getting Started | Core extract → generate → build pipeline |
| `WrapGod.WorkflowDemo/` | Getting Started | Full automated pipeline (extract → config → generate → analyze → fix) |
| `MoqToNSubstitute/` | Library Swap | Moq → NSubstitute migration |
| `NSubstituteToMoq/` | Library Swap | NSubstitute → Moq reverse migration |
| `NUnitToXUnit/` | Library Swap | NUnit → xUnit migration |
| `XUnitToNUnit/` | Library Swap | xUnit → NUnit migration |
| `FluentAssertionsToShouldly/` | Library Swap | FluentAssertions → Shouldly migration |
| `migrations/serilog-nlog-bidirectional/` | Library Swap | Serilog ↔ NLog with parity tests |
| `migrations/automapper-mapster-bidirectional/` | Library Swap | AutoMapper ↔ Mapster with parity tests |
| `migrations/efcore-dapper-bidirectional/` | Library Swap | EF Core ↔ Dapper service-boundary migration |
| `migrations/mediatr-masstransit-mediator-bidirectional/` | Library Swap | MediatR ↔ MassTransit Mediator with parity tests |
| `migrations/hangfire-quartz-bidirectional/` | Library Swap | Hangfire ↔ Quartz.NET with parity tests |
| `migrations/nuget-version-matrix/fluent-assertions/` | Version Matrix | FluentAssertions 6.x → 7.x breaking changes |
| `migrations/nuget-version-matrix/moq/` | Version Matrix | Moq 4.x series surface changes |
| `migrations/nuget-version-matrix/serilog/` | Version Matrix | Serilog 3.x → 4.x API restructuring |
| `migrations/nuget-version-matrix/mediatr/` | Version Matrix | MediatR 11.x → 12.x handler changes |
| `case-studies/company-assertions-migration/` | Enterprise | Multi-app assertion library consolidation |
