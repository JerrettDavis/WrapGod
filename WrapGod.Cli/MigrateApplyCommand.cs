using System.CommandLine;
using System.Text.Json;
using WrapGod.Cli.Globbing;
using WrapGod.Migration;
using WrapGod.Migration.Engine;
using WrapGod.Migration.Engine.State;

namespace WrapGod.Cli;

/// <summary>
/// Implements <c>wrap-god migrate apply [options]</c>.
///
/// Loads a migration schema, discovers matching source files via glob patterns,
/// runs <see cref="StatefulMigrationEngine"/> (honouring prior state for idempotence),
/// prints a structured summary, and persists the updated state file.
///
/// Exit codes (per §4.3 of the migration engine plan):
/// <list type="bullet">
///   <item><description>0 – success</description></item>
///   <item><description>1 – runtime error (schema missing, IO error, JSON parse failure)</description></item>
///   <item><description>2 – bad arguments (required flag absent)</description></item>
/// </list>
/// </summary>
internal static class MigrateApplyCommand
{
    // ── JSON output options (camelCase, indented — mirrors MigrateGenerateCommand) ──────────
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    // ── Command factory ──────────────────────────────────────────────────────────────────────

    public static Command Create()
    {
        var schemaOption = new Option<FileInfo?>(
            ["--schema", "-s"],
            "Path to the migration schema JSON produced by 'migrate generate'.")
        {
            IsRequired = true,
        };

        var projectDirOption = new Option<DirectoryInfo?>(
            ["--project-dir", "-p"],
            "Project root directory for glob resolution and state file location. Defaults to the current directory.");

        var includeOption = new Option<string[]>(
            "--include",
            "Glob pattern for files to include. Can be specified multiple times. Default: **/*.cs")
        {
            AllowMultipleArgumentsPerToken = false,
            Arity = ArgumentArity.ZeroOrMore,
        };

        var excludeOption = new Option<string[]>(
            "--exclude",
            "Glob pattern for files to exclude. Can be specified multiple times. Defaults: **/bin/**, **/obj/**, **/.wrapgod/**")
        {
            AllowMultipleArgumentsPerToken = false,
            Arity = ArgumentArity.ZeroOrMore,
        };

        var dryRunOption = new Option<bool>(
            "--dry-run",
            "Preview changes without modifying files or persisting state.");

        var jsonOption = new Option<bool>(
            "--json",
            "Emit the summary as JSON to stdout instead of human-readable text.");

        var verboseOption = new Option<bool>(
            ["--verbose", "-v"],
            "Print extra diagnostic information during the run.");

        var command = new Command("apply", "Apply a migration schema to a codebase")
        {
            schemaOption,
            projectDirOption,
            includeOption,
            excludeOption,
            dryRunOption,
            jsonOption,
            verboseOption,
        };

        command.SetHandler((context) =>
        {
            var schema       = context.ParseResult.GetValueForOption(schemaOption);
            var projectDir   = context.ParseResult.GetValueForOption(projectDirOption);
            var includes     = context.ParseResult.GetValueForOption(includeOption) ?? [];
            var excludes     = context.ParseResult.GetValueForOption(excludeOption) ?? [];
            var dryRun       = context.ParseResult.GetValueForOption(dryRunOption);
            var json         = context.ParseResult.GetValueForOption(jsonOption);
            var verbose      = context.ParseResult.GetValueForOption(verboseOption);

            var exitCode = Handle(
                schema, projectDir, includes, excludes, dryRun, json, verbose);

            context.ExitCode = exitCode;
            return Task.CompletedTask;
        });

        return command;
    }

    // ── Handler ──────────────────────────────────────────────────────────────────────────────

    private static int Handle(
        FileInfo? schemaFile,
        DirectoryInfo? projectDirInfo,
        string[] includes,
        string[] excludes,
        bool dryRun,
        bool jsonOutput,
        bool verbose)
    {
        // ── 1. Validate schema path ─────────────────────────────────────────────────────────
        if (schemaFile is null)
        {
            Console.Error.WriteLine("Error: --schema is required.");
            return 2;
        }

        if (!schemaFile.Exists)
        {
            Console.Error.WriteLine($"Error: Schema file not found: {schemaFile.FullName}");
            return 1;
        }

        // ── 2. Validate project directory ───────────────────────────────────────────────────
        var projectDir = projectDirInfo?.FullName ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(projectDir))
        {
            Console.Error.WriteLine($"Error: Project directory not found: {projectDir}");
            return 1;
        }

        // ── 3. Load schema ──────────────────────────────────────────────────────────────────
        string schemaJson;
        try
        {
            schemaJson = File.ReadAllText(schemaFile.FullName);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Error reading schema file: {ex.Message}");
            return 1;
        }

        MigrationSchema? schema;
        try
        {
            schema = MigrationSchemaSerializer.Deserialize(schemaJson);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing schema JSON: {ex.Message}");
            return 1;
        }

        if (schema is null)
        {
            Console.Error.WriteLine("Error: Schema file is empty or could not be parsed.");
            return 1;
        }

        // ── 4. Short-circuit: no rules ──────────────────────────────────────────────────────
        if (schema.Rules.Count == 0)
        {
            if (jsonOutput)
            {
                PrintJsonSummary(new ApplySummary
                {
                    DryRun         = dryRun,
                    FilesScanned   = 0,
                    FilesModified  = 0,
                    Applied        = 0,
                    Skipped        = 0,
                    Manual         = 0,
                    Message        = "Schema has no rules.",
                });
            }
            else
            {
                Console.WriteLine("No rules to apply. Schema has 0 migration rules.");
            }

            return 0;
        }

        // ── 5. Discover files ───────────────────────────────────────────────────────────────
        IReadOnlyList<string> files;
        try
        {
            files = FileMatcherHelper.GetMatchingFiles(projectDir, includes, excludes);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error scanning project directory: {ex.Message}");
            return 1;
        }

        if (verbose)
            Console.Error.WriteLine($"[verbose] Discovered {files.Count} files under '{projectDir}'.");

        // ── 6. Build engine ─────────────────────────────────────────────────────────────────
        var engine        = MigrationEngine.CreateDefault();
        var statefulEngine = new StatefulMigrationEngine(engine);

        // ── 7. Run ──────────────────────────────────────────────────────────────────────────
        MigrationResult result;
        try
        {
            result = dryRun
                ? statefulEngine.DryRunWithState(schemaFile.FullName, schema, files)
                : statefulEngine.ApplyWithState(schemaFile.FullName, schema, files);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during migration: {ex.Message}");
            return 1;
        }

        // ── 8. Report ───────────────────────────────────────────────────────────────────────
        var summary = new ApplySummary
        {
            DryRun        = dryRun,
            FilesScanned  = files.Count,
            FilesModified = result.RewrittenFiles.Count,
            Applied       = result.AppliedCount,
            Skipped       = result.SkippedCount,
            Manual        = result.ManualCount,
            SkippedDetails = result.Skipped
                .Select(s => $"  {s.RuleId} {s.File}:{s.Line}  {s.Reason}")
                .ToList(),
            ManualDetails = result.Manual
                .Select(m =>
                {
                    var files2 = m.MatchedFiles.Count > 0
                        ? string.Join(", ", m.MatchedFiles)
                        : "(no files matched)";
                    return $"  {m.RuleId} {m.Note}\n    matched in: {files2}";
                })
                .ToList(),
        };

        if (jsonOutput)
            PrintJsonSummary(summary);
        else
            PrintHumanSummary(summary, schema, schemaFile.Name, dryRun);

        return 0;
    }

    // ── Output helpers ────────────────────────────────────────────────────────────────────────

    private static void PrintHumanSummary(
        ApplySummary summary,
        MigrationSchema schema,
        string schemaName,
        bool dryRun)
    {
        var header = dryRun
            ? "WrapGod migrate apply [DRY-RUN]"
            : "WrapGod migrate apply";

        Console.WriteLine(header);
        Console.WriteLine(new string('-', header.Length));
        Console.WriteLine($"Schema:    {schemaName} ({schema.Rules.Count} rules)");
        Console.WriteLine($"Files:     {summary.FilesScanned} scanned, {summary.FilesModified} modified");
        Console.WriteLine($"Applied:   {summary.Applied} rewrites");
        Console.WriteLine($"Skipped:   {summary.Skipped}");
        Console.WriteLine($"Manual:    {summary.Manual} rules require human intervention");

        if (!string.IsNullOrEmpty(summary.Message))
            Console.WriteLine(summary.Message);

        if (summary.SkippedDetails.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Skipped:");
            foreach (var line in summary.SkippedDetails)
                Console.WriteLine(line);
        }

        if (summary.ManualDetails.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Manual:");
            foreach (var line in summary.ManualDetails)
                Console.WriteLine(line);
        }

        if (dryRun)
        {
            Console.WriteLine();
            Console.WriteLine("(no files were modified)");
        }
    }

    private static void PrintJsonSummary(ApplySummary summary)
    {
        var output = new
        {
            dryRun        = summary.DryRun,
            filesScanned  = summary.FilesScanned,
            filesModified = summary.FilesModified,
            applied       = summary.Applied,
            skipped       = summary.Skipped,
            manual        = summary.Manual,
            message       = summary.Message,
        };
        Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
    }

    // ── Internal data ─────────────────────────────────────────────────────────────────────────

    private sealed class ApplySummary
    {
        public bool DryRun { get; init; }
        public int FilesScanned { get; init; }
        public int FilesModified { get; init; }
        public int Applied { get; init; }
        public int Skipped { get; init; }
        public int Manual { get; init; }
        public string? Message { get; init; }
        public List<string> SkippedDetails { get; init; } = [];
        public List<string> ManualDetails { get; init; } = [];
    }
}
