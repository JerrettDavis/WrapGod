#!/usr/bin/env bash
# run-migration.sh — Demonstrates the WrapGod migrate workflow for Serilog v2 to v3.
#
# Usage:
#   ./scripts/run-migration.sh           # dry-run preview (no files changed)
#   ./scripts/run-migration.sh --apply   # apply the schema to a temp copy of before/

set -euo pipefail

APPLY=false
for arg in "$@"; do
  [ "$arg" = "--apply" ] && APPLY=true
done

EXAMPLE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO_ROOT="$(cd "$EXAMPLE_ROOT/../../.." && pwd)"
CLI_PROJECT="$REPO_ROOT/WrapGod.Cli/WrapGod.Cli.csproj"
SCHEMA_FILE="$EXAMPLE_ROOT/schema/serilog.2.x-to-3.x.wrapgod-migration.json"
BEFORE_DIR="$EXAMPLE_ROOT/before"

TEMP_DIR="$(mktemp -d)"
echo "Copying before/ to temp dir: $TEMP_DIR"
cp -r "$BEFORE_DIR/." "$TEMP_DIR/"

trap 'echo "Temp dir NOT removed: $TEMP_DIR"' EXIT

# 1. Dry-run preview
echo ""
echo "[1/3] Dry-run preview"
dotnet run --project "$CLI_PROJECT" -- migrate apply \
  --schema "$SCHEMA_FILE" \
  --project-dir "$TEMP_DIR" \
  --dry-run

if [ "$APPLY" = false ]; then
  echo ""
  echo "Dry-run complete. Pass --apply to write changes."
  exit 0
fi

# 2. Apply
echo ""
echo "[2/3] Applying schema"
dotnet run --project "$CLI_PROJECT" -- migrate apply \
  --schema "$SCHEMA_FILE" \
  --project-dir "$TEMP_DIR"

# 3. Status
echo ""
echo "[3/3] Migration status"
dotnet run --project "$CLI_PROJECT" -- migrate status \
  --schema "$SCHEMA_FILE" \
  --project-dir "$TEMP_DIR"

echo ""
echo "Applied output in: $TEMP_DIR"
echo "Compare with after/:"
echo "  git diff --no-index \"$TEMP_DIR\" \"$EXAMPLE_ROOT/after\""
