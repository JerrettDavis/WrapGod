#Requires -Version 7.0
<#
.SYNOPSIS
    Demonstrates the full WrapGod migrate workflow for Serilog v2 to v3.

.DESCRIPTION
    Runs all five migration CLI steps on a temp copy of before/ so the original
    fixture is never modified. Pass -Apply to persist changes; omit it for dry-run
    preview only.

.PARAMETER Apply
    When set, actually writes changes to disk (default is dry-run only).

.EXAMPLE
    # Preview what the engine would change (no files modified)
    ./scripts/run-migration.ps1

    # Apply the schema and write changes to the temp copy
    ./scripts/run-migration.ps1 -Apply
#>
[CmdletBinding()]
param(
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExampleRoot = Split-Path $PSScriptRoot -Parent
$RepoRoot    = Split-Path (Split-Path (Split-Path $ExampleRoot -Parent) -Parent) -Parent
$CliProject  = Join-Path $RepoRoot "WrapGod.Cli" "WrapGod.Cli.csproj"
$SchemaFile  = Join-Path $ExampleRoot "schema" "serilog.2.x-to-3.x.wrapgod-migration.json"
$BeforeDir   = Join-Path $ExampleRoot "before"

# Create a temp copy so the fixture is never modified
$TempDir = Join-Path ([System.IO.Path]::GetTempPath()) "wrapgod-serilog-$(New-Guid)"
Write-Host "Copying before/ to temp dir: $TempDir" -ForegroundColor Cyan
Copy-Item -Recurse $BeforeDir $TempDir

try {
    # 1. Generate a schema draft (informational — the committed schema was hand-authored)
    Write-Host "`n[1/4] Showing committed schema (skipping generate — schema already hand-authored)" -ForegroundColor Cyan
    Get-Content $SchemaFile | Write-Host

    # 2. Dry-run preview
    Write-Host "`n[2/4] Dry-run preview" -ForegroundColor Cyan
    dotnet run --project $CliProject -- migrate apply `
        --schema $SchemaFile `
        --project-dir $TempDir `
        --dry-run

    if (-not $Apply) {
        Write-Host "`nDry-run complete. Pass -Apply to write changes." -ForegroundColor Yellow
        return
    }

    # 3. Apply
    Write-Host "`n[3/4] Applying schema" -ForegroundColor Cyan
    dotnet run --project $CliProject -- migrate apply `
        --schema $SchemaFile `
        --project-dir $TempDir

    # 4. Status
    Write-Host "`n[4/4] Migration status" -ForegroundColor Cyan
    dotnet run --project $CliProject -- migrate status `
        --schema $SchemaFile `
        --project-dir $TempDir

    Write-Host "`nApplied output in: $TempDir" -ForegroundColor Green
    Write-Host "Compare with after/ using: git diff --no-index $TempDir $((Join-Path $ExampleRoot 'after'))" -ForegroundColor Green
}
finally {
    # Uncomment to auto-clean:
    # Remove-Item -Recurse -Force $TempDir
    Write-Host "`nTemp dir NOT removed: $TempDir" -ForegroundColor DarkGray
}
