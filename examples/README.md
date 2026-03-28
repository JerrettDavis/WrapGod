# WrapGod Examples

This folder contains a runnable, end-to-end workflow demo for issue #43.

## Example projects

- `Acme.Lib` — tiny third-party library we will wrap
- `WrapGod.WorkflowDemo` — console app that executes the full WrapGod flow
- `migrations/serilog-nlog-bidirectional` — bidirectional Serilog <-> NLog integration pack with parity tests
- `migrations/nuget-version-matrix` — version-divergence example packs (FluentAssertions/Moq/Serilog) with compatibility reports and CI drift guards

## What the demo does

`WrapGod.WorkflowDemo` performs these steps in one run:

1. **Extract** — builds `Acme.Lib` and extracts `acme.wrapgod.json`
2. **Config** — writes `acme.wrapgod.config.json` (rename + exclude)
3. **Generate** — runs `WrapGodIncrementalGenerator` and emits generated sources
4. **Analyze** — runs `DirectUsageAnalyzer` and reports `WG2001/WG2002`
5. **Fix** — applies `UseWrapperCodeFixProvider` for `WG2001`

Artifacts are written to `examples/output/`:

- `acme.wrapgod.json`
- `acme.wrapgod.config.json`
- `acme.wrapgod-types.txt`
- `diagnostics.txt`
- `Consumer.fixed.cs`
- `generated/*.g.cs`

## Run it

From repository root:

```bash
dotnet run --project examples/WrapGod.WorkflowDemo/WrapGod.WorkflowDemo.csproj
```

Expected summary includes:

- `WG2001 present: True`
- `WG2002 present: True`
- `Generated BetterFoo interface: True`
- `Generated BarClient wrapper (expected false): False`

## Validate locally

```bash
# Build example solution
dotnet build examples/WrapGod.Examples.slnx -c Release

# Run the workflow demo
dotnet run --project examples/WrapGod.WorkflowDemo/WrapGod.WorkflowDemo.csproj -c Release
```
