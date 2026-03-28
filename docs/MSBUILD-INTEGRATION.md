# MSBuild Integration

WrapGod ships an MSBuild targets package (`WrapGod.Targets`) that automates the
extract-and-generate pipeline so consuming projects need zero manual build steps.

## Quick Start

Add the NuGet package and declare the packages you want to wrap:

```xml
<PackageReference Include="WrapGod.Targets" Version="0.1.0-alpha" PrivateAssets="all" />
```

Then declare the packages to wrap as `WrapGodPackage` items:

```xml
<ItemGroup>
  <WrapGodPackage Include="SomeVendor.Lib" />
</ItemGroup>
```

Build as normal -- the targets handle everything automatically.

## How It Works

The package injects three MSBuild targets into the build pipeline:

| Target             | Runs Before    | Purpose |
|--------------------|----------------|---------|
| `WrapGodRestore`   | `Restore`      | Resolves NuGet packages listed as `WrapGodPackage` items into the local cache. |
| `WrapGodExtract`   | `CoreCompile`  | Extracts the API manifest from resolved assemblies. Incremental -- skips if inputs are unchanged. |
| `WrapGodGenerate`  | `CoreCompile`  | Registers the manifest and config as `AdditionalFiles` so the Roslyn source generator picks them up. |

A `WrapGodClean` target also runs after `Clean` to remove cached artifacts.

## Properties

All properties have sensible defaults. Override them in your project file if needed:

| Property               | Default                                    | Description |
|------------------------|--------------------------------------------|-------------|
| `WrapGodManifestPath`  | `$(MSBuildProjectDirectory)\manifest.wrapgod.json` | Path to the generated manifest file. |
| `WrapGodConfigPath`    | `$(MSBuildProjectDirectory)\wrapgod.config.json`   | Path to the WrapGod configuration file. |
| `WrapGodCacheDir`      | `$(MSBuildProjectDirectory)\.wrapgod-cache`        | Directory for cached packages and intermediate artifacts. |
| `EnableWrapGod`        | `true`                                     | Set to `false` to disable all WrapGod targets. |

## Incremental Builds

The `WrapGodExtract` target hashes its inputs (resolved assemblies + config file)
and compares against a stored hash in the cache directory. Extraction only runs
when inputs have actually changed, keeping rebuilds fast.

## Troubleshooting

- **Targets not running?** Ensure `WrapGodPackage` items are declared and
  `EnableWrapGod` is not set to `false`.
- **Stale output?** Run `dotnet clean` to clear the WrapGod cache, then rebuild.
- **Custom tool path?** The extract step invokes `dotnet wrap-god` via the
  global tool. Ensure it is installed: `dotnet tool install -g WrapGod.Cli`.
