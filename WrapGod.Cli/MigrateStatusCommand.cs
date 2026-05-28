using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using WrapGod.Migration;
using WrapGod.Migration.Engine.State;

namespace WrapGod.Cli;

/// <summary>
/// Implements <c>wrap-god migrate status</c> — reads the state file sibling to a schema
/// and reports progress without running any migration.
/// </summary>
internal static class MigrateStatusCommand
{
    private static readonly JsonSerializerOptions JsonOutputOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // NOTE: progressPct, library, from, to may be null and MUST be emitted as
        // explicit JSON null so downstream tooling can distinguish "missing" from
        // "not applicable" (e.g. progressPct == null means 0-rules schema).
    };

    public static Command Create()
    {
        var schemaOption = new Option<string>(
            ["--schema", "-s"],
            "Path to the migration schema JSON file. The state file is the sibling <schema>.state.json.")
        {
            IsRequired = true,
        };

        var projectDirOption = new Option<string?>(
            ["--project-dir", "-p"],
            "Project directory used to resolve a relative --schema path (default: current directory).");

        var jsonOption = new Option<bool>(
            "--json",
            "Emit output as JSON instead of human-readable text.");

        var verboseOption = new Option<bool>(
            ["--verbose", "-v"],
            "Include per-rule details and per-file applied lists in human-readable mode.");

        var command = new Command("status", "Report migration progress from the state file without running any migration")
        {
            schemaOption,
            projectDirOption,
            jsonOption,
            verboseOption,
        };

        command.SetHandler((context) =>
        {
            var schema = context.ParseResult.GetValueForOption(schemaOption)!;
            var projectDir = context.ParseResult.GetValueForOption(projectDirOption);
            var json = context.ParseResult.GetValueForOption(jsonOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);

            context.ExitCode = Handle(schema, projectDir, json, verbose);
        });

        return command;
    }

    private static int Handle(
        string schemaArg,
        string? projectDir,
        bool jsonOutput,
        bool verbose)
    {
        // ── Resolve schema path ──────────────────────────────────────────────────────────────
        var baseDir = string.IsNullOrWhiteSpace(projectDir)
            ? Directory.GetCurrentDirectory()
            : projectDir;

        var schemaPath = Path.IsPathRooted(schemaArg)
            ? schemaArg
            : Path.GetFullPath(Path.Combine(baseDir, schemaArg));

        if (!File.Exists(schemaPath))
        {
            Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Error: schema file not found: {0}", schemaPath));
            return 1;
        }

        // ── Load schema (for library/from/to metadata + total rule count) ───────────────────
        MigrationSchema? schema = null;
        string? currentHash = null;
        try
        {
            var schemaJson = File.ReadAllText(schemaPath, System.Text.Encoding.UTF8);
            currentHash = MigrationStateStore.ComputeSchemaHash(schemaJson);
            try
            {
                schema = MigrationSchemaSerializer.Deserialize(schemaJson);
            }
            catch (JsonException)
            {
                // Schema couldn't be parsed — non-fatal, we just don't show library/from/to or totalRules
                schema = null;
            }
        }
        catch (IOException)
        {
            // Non-fatal — schema read failed; skip hash and metadata
        }

        // ── Load state ───────────────────────────────────────────────────────────────────────
        var state = MigrationStateStore.Load(schemaPath, out var wasCorrupt, out var backupPath);

        if (wasCorrupt)
        {
            var bakMsg = backupPath is not null
                ? string.Format(CultureInfo.InvariantCulture, "Corrupt state file was archived to: {0}", backupPath)
                : "Corrupt state file could not be archived (file may be locked).";

            Console.Error.WriteLine("Error: The migration state file is corrupt and could not be parsed.");
            Console.Error.WriteLine(bakMsg);
            return 1;
        }

        if (state is null)
        {
            // State file simply doesn't exist — info-only, not an error.
            if (jsonOutput)
            {
                var sentinel = new
                {
                    status = "no-runs-recorded",
                    library = schema?.Library,
                    from = schema?.From,
                    to = schema?.To,
                };
                Console.WriteLine(JsonSerializer.Serialize(sentinel, JsonOutputOptions));
            }
            else
            {
                Console.WriteLine("No migration runs recorded for this schema.");
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "Run `wrap-god migrate apply --schema \"{0}\"` to apply migrations.",
                    Path.GetFileName(schemaPath)));
            }
            return 0;
        }

        var schemaChanged = currentHash is not null && state.SchemaHasChanged(currentHash);

        // ── Detect synthetic <state> SkippedRewrite (corruption recovery marker from #197) ──
        var hasStateRecoveryEntry = state.Skipped.Any(s =>
            string.Equals(s.RuleId, "<state>", StringComparison.Ordinal));

        // ── Progress calculation ─────────────────────────────────────────────────────────────
        // totalRules from the schema (source of truth); appliedRules = distinct rule IDs in state.Applied
        var totalRules = schema?.Rules.Count ?? 0;
        var appliedRules = state.Applied
            .Select(a => a.RuleId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        double? progressPct = totalRules == 0
            ? (double?)null
            : (double)appliedRules / totalRules;

        // ── Emit output ──────────────────────────────────────────────────────────────────────
        if (jsonOutput)
        {
            return EmitJson(state, schema, schemaChanged, currentHash, hasStateRecoveryEntry,
                totalRules, appliedRules, progressPct);
        }
        else
        {
            return EmitHumanReadable(state, schema, schemaChanged, hasStateRecoveryEntry, verbose,
                totalRules, appliedRules, progressPct);
        }
    }

    // ── JSON output ──────────────────────────────────────────────────────────────────────────

    private static int EmitJson(
        MigrationState state,
        MigrationSchema? schema,
        bool schemaChanged,
        string? currentHash,
        bool hasStateRecoveryEntry,
        int totalRules,
        int appliedRules,
        double? progressPct)
    {
        var appliedByRule = state.Applied
            .GroupBy(a => a.RuleId, StringComparer.Ordinal)
            .Select(g => new
            {
                ruleId = g.Key,
                fileCount = g.Select(a => a.File).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            })
            .ToList();

        var skippedByReason = state.Skipped
            .Where(s => !string.Equals(s.RuleId, "<state>", StringComparison.Ordinal))
            .GroupBy(s => s.Reason, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { reason = g.Key, count = g.Count() })
            .ToList();

        var manualEntries = state.Manual
            .Select(m => new
            {
                ruleId = m.RuleId,
                note = m.Note,
                matchedFiles = m.MatchedFiles.Count == 0
                    ? (object)"(no files matched yet)"
                    : m.MatchedFiles,
            })
            .ToList();

        var output = new
        {
            library = schema?.Library,
            from = schema?.From,
            to = schema?.To,
            schema = state.Schema,
            schemaHash = state.SchemaHash,
            schemaChanged,
            currentSchemaHash = currentHash,
            startedAt = state.StartedAt,
            lastRunAt = state.LastRunAt,
            totalRules,
            appliedRules,
            progressPct,
            summary = new
            {
                total = state.Summary.TotalRules,
                applied = state.Summary.Applied,
                skipped = state.Summary.Skipped,
                manual = state.Summary.Manual,
            },
            applied = appliedByRule,
            skipped = skippedByReason,
            manual = manualEntries,
            stateRecoveryOccurred = hasStateRecoveryEntry,
        };

        Console.WriteLine(JsonSerializer.Serialize(output, JsonOutputOptions));
        return state.Manual.Count > 0 ? 2 : 0;
    }

    // ── Human-readable output ────────────────────────────────────────────────────────────────

    private static int EmitHumanReadable(
        MigrationState state,
        MigrationSchema? schema,
        bool schemaChanged,
        bool hasStateRecoveryEntry,
        bool verbose,
        int totalRules,
        int appliedRules,
        double? progressPct)
    {
        Console.WriteLine("WrapGod migrate status");
        Console.WriteLine(new string('-', 40));

        // Migration metadata header (library + from → to)
        if (schema is not null && !string.IsNullOrEmpty(schema.Library))
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Migration: {0} {1} -> {2}", schema.Library, schema.From, schema.To));
        }

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Schema:     {0}", state.Schema));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Started:    {0:yyyy-MM-dd HH:mm:ss} UTC", state.StartedAt.UtcDateTime));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Last run:   {0:yyyy-MM-dd HH:mm:ss} UTC", state.LastRunAt.UtcDateTime));
        Console.WriteLine();

        // Progress ratio line
        if (totalRules > 0 && progressPct.HasValue)
        {
            var pctInt = (int)Math.Round(progressPct.Value * 100.0, MidpointRounding.AwayFromZero);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Progress: {0} / {1} rules applied ({2}%)",
                appliedRules, totalRules, pctInt));
        }
        else
        {
            // Zero-rules edge case — display "n/a"
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Progress: {0} / {1} rules applied (n/a)",
                appliedRules, totalRules));
        }
        Console.WriteLine();

        // Schema hash line
        if (schemaChanged)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Schema hash: {0} (WARNING: schema has changed since last apply)", state.SchemaHash));
        }
        else if (!string.IsNullOrEmpty(state.SchemaHash))
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "Schema hash: {0} (matches current schema)", state.SchemaHash));
        }

        Console.WriteLine();

        // State recovery highlight
        if (hasStateRecoveryEntry)
        {
            Console.WriteLine("  [!] State recovery occurred — a previous state was corrupt and this run started fresh.");
            Console.WriteLine();
        }

        // Counts
        var appliedFiles = state.Applied
            .Select(a => a.File)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Applied:  {0,5}   (across {1} file(s))", state.Summary.Applied, appliedFiles));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Skipped:  {0,5}", state.Summary.Skipped));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Manual:   {0,5}", state.Summary.Manual));

        // ── Verbose: per-rule applied breakdown ─────────────────────────────────────
        if (verbose && state.Applied.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Applied rules:");
            foreach (var group in state.Applied
                .GroupBy(a => a.RuleId, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var fileCount = group
                    .Select(a => a.File)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}: {1} file(s)", group.Key, fileCount));
            }
        }

        // ── Skipped rules ────────────────────────────────────────────────────────────
        var visibleSkipped = state.Skipped
            .Where(s => !string.Equals(s.RuleId, "<state>", StringComparison.Ordinal))
            .ToList();

        if (visibleSkipped.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Skipped rules:");
            if (verbose)
            {
                foreach (var skip in visibleSkipped.OrderBy(s => s.RuleId, StringComparer.Ordinal))
                {
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  {0}  {1}:{2}  {3}", skip.RuleId, skip.File, skip.Line, skip.Reason));
                }
            }
            else
            {
                // Group by reason
                foreach (var group in visibleSkipped
                    .GroupBy(s => s.Reason, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Count()))
                {
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "  {0}: {1}", group.Key, group.Count()));
                }
            }
        }

        // ── Manual rules ─────────────────────────────────────────────────────────────
        if (state.Manual.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Manual rules (require human intervention):");
            foreach (var manual in state.Manual.OrderBy(m => m.RuleId, StringComparer.Ordinal))
            {
                Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "  {0}  {1}", manual.RuleId, manual.Note));
                if (verbose)
                {
                    if (manual.MatchedFiles.Count == 0)
                    {
                        Console.WriteLine("    (no files matched yet)");
                    }
                    else
                    {
                        foreach (var file in manual.MatchedFiles)
                        {
                            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                                "    {0}", file));
                        }
                    }
                }
            }
        }

        return state.Manual.Count > 0 ? 2 : 0;
    }
}
