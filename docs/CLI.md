# CLI Reference

`wrap-god` is the .NET tool front-end for common WrapGod workflows.

## Command surface

- `wrap-god init` – scaffold `wrapgod.config.json` and `.wrapgod-cache/`
- `wrap-god extract` – extract a manifest from an assembly/NuGet/self source
- `wrap-god generate` – generate wrappers from a manifest/config pair
- `wrap-god analyze` – summarize manifest health and apply diagnostics gate policy
- `wrap-god doctor` – environment + project health checks
- `wrap-god explain` – explain compatibility mode behavior for a manifest
- `wrap-god migrate init` – analyze project usage and bootstrap migration plan assets
- `wrap-god ci bootstrap` – CI helper setup
- `wrap-god ci parity` – CI parity report generation

## Diagnostics gate exit codes (`analyze`)

- `0` success (no effective errors)
- `1` command/runtime failure
- `2` diagnostics gate failed on `error`
- `3` diagnostics gate failed on `warning` when `--warnings-as-errors` is enabled

## Common examples

```bash
# Scaffold config in current directory
wrap-god init --source @self

# Extract manifest from local assembly
wrap-god extract .\lib\Vendor.Lib.dll -o vendor-lib.wrapgod.json

# Analyze with strict warning gate
wrap-god analyze vendor-lib.wrapgod.json --warnings-as-errors

# Verify local project setup
wrap-god doctor --project-dir .
```

## Troubleshooting

- **`Manifest not found`**: verify path and current working directory.
- **`Config file already exists` on `init`**: pass `--output` with a new file name or remove the existing config.
- **`doctor` reports missing generator**: add `WrapGod.Generator` package to consuming project.
- **Gate exit code `3`**: rerun without `--warnings-as-errors` to inspect warnings without failing CI.
