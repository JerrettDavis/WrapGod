# WrapGod Basic Example

This example demonstrates the core WrapGod pipeline: taking a third-party
library and generating type-safe wrappers around it.

## Project structure

```
BasicExample/
  VendorLib/          # Simulated third-party library
    HttpClient.cs     # HTTP client class to wrap
    Logger.cs         # Logger class + LogLevel enum to wrap
  MyApp/              # Consuming application
    wrapgod.json      # WrapGod manifest — defines which types/members to wrap
    Program.cs        # Runs the full pipeline end-to-end
  BasicExample.slnx   # Solution file
```

## What it shows

1. **JSON config loading** — `wrapgod.json` declares which vendor types to
   include, which members to expose, and how to rename them.
2. **TypeMappingPlanner** — Builds a `TypeMappingPlan` from the config,
   supporting object mappings, enum casts, and member renames.
3. **TypeMapperEmitter** — Generates C# static mapper classes with `Map()`
   methods that convert from vendor types to your wrapper types.
4. **Fluent DSL** — Shows the equivalent configuration built programmatically
   with the `WrapGodConfiguration` fluent API.

## Running the example

```bash
# From the repository root
dotnet run --project examples/BasicExample/MyApp/MyApp.csproj

# Or from this directory
dotnet run --project MyApp/MyApp.csproj
```

## Expected output

The program prints the loaded config summary, the generated mapper source code,
and the equivalent fluent DSL configuration to the console.
