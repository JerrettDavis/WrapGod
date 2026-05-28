using System.CommandLine;
using System.Text;
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

    /// <summary>
    /// Synthetic rule id used by <see cref="StatefulMigrationEngine"/> when a corrupt
    /// state file is recovered. We surface this prominently to the user.
    /// </summary>
    private const string StateRecoveryRuleId = "<state>";

    /// <summary>
    /// Maximum diff lines printed inline per file during <c>--dry-run</c>. When the
    /// per-file diff exceeds this value, the remaining lines are dumped to a side file
    /// under <c>.wrapgod/dryrun-&lt;timestamp&gt;.diff</c>.
    /// </summary>
    private const int MaxInlineDiffLinesPerFile = 20;

    // ── Command factory ──────────────────────────────────────────────────────────────────────

    public static Command Create()
    {
        // NOTE: --schema is NOT declared IsRequired = true here. System.CommandLine's
        // default behavior on a missing required option is to emit a parse error and
        // exit with code 1 (which we cannot easily intercept from inside a sub-command
        // handler without rebuilding the parent parse pipeline).  Instead, the handler
        // checks for null and returns exit code 2 itself, matching the plan §4.3 contract.
        var schemaOption = new Option<FileInfo?>(
            ["--schema", "-s"],
            "Path to the migration schema JSON produced by 'migrate generate'.");

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
        // Plan §4.3: missing required flag → exit 2 (bad args).
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

        // ── 6. Capture pre-run file contents (only needed for dry-run diff) ─────────────────
        Dictionary<string, string>? preRunContents = null;
        if (dryRun)
        {
            preRunContents = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var f in files)
            {
                try { preRunContents[f] = File.ReadAllText(f); }
                catch { /* best-effort — file may not be readable; diff will skip */ }
            }
        }

        // ── 7. Build engine ─────────────────────────────────────────────────────────────────
        var engine        = MigrationEngine.CreateDefault();
        var statefulEngine = new StatefulMigrationEngine(engine);

        // ── 8. Run ──────────────────────────────────────────────────────────────────────────
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

        // ── 9. Detect state recovery ────────────────────────────────────────────────────────
        var stateRecovery = ExtractStateRecovery(result);

        // ── 10. Compute dry-run diff (truncated inline; full diff to dump file) ─────────────
        DryRunDiff? dryRunDiff = null;
        if (dryRun && result.RewrittenFiles.Count > 0 && preRunContents is not null)
        {
            dryRunDiff = BuildDryRunDiff(projectDir, result.RewrittenFiles, preRunContents);
        }

        // ── 11. Assemble summary ────────────────────────────────────────────────────────────
        // Exclude the synthetic <state> recovery skip from the regular skipped details —
        // it's surfaced as its own banner / field instead.
        var realSkipped = result.Skipped
            .Where(s => s.RuleId != StateRecoveryRuleId)
            .ToList();

        // Group applied rewrites by ruleId for the appliedByRule JSON view.
        var appliedByRule = result.Applied
            .GroupBy(a => a.RuleId, StringComparer.Ordinal)
            .Select(g => new AppliedByRuleEntry
            {
                RuleId    = g.Key,
                Kind      = ResolveRuleKind(schema, g.Key),
                FileCount = g.Select(a => a.File).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                Count     = g.Count(),
            })
            .OrderBy(e => e.RuleId, StringComparer.Ordinal)
            .ToList();

        var summary = new ApplySummary
        {
            DryRun         = dryRun,
            FilesScanned   = files.Count,
            FilesModified  = result.RewrittenFiles.Count,
            Applied        = result.AppliedCount,
            // Reported "skipped" count excludes the state-recovery synthetic entry.
            Skipped        = realSkipped.Count,
            Manual         = result.ManualCount,
            StateRecovery  = stateRecovery,
            SkippedEntries = realSkipped.Select(s => new SkippedEntry
            {
                RuleId  = s.RuleId,
                File    = s.File,
                Line    = s.Line,
                Reason  = s.Reason,
            }).ToList(),
            ManualEntries  = result.Manual.Select(m => new ManualEntry
            {
                RuleId        = m.RuleId,
                Note          = m.Note,
                MatchedFiles  = [.. m.MatchedFiles],
            }).ToList(),
            AppliedByRule  = appliedByRule,
            DryRunDiff     = dryRunDiff,
        };

        if (jsonOutput)
            PrintJsonSummary(summary);
        else
            PrintHumanSummary(summary, schema, schemaFile.Name, dryRun);

        return 0;
    }

    // ── State-recovery helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Looks for the synthetic <c>&lt;state&gt;</c> skip emitted by
    /// <see cref="StatefulMigrationEngine"/> when a corrupt state file is archived.
    /// Returns a structured <see cref="StateRecoveryInfo"/> when found, else <see langword="null"/>.
    /// </summary>
    private static StateRecoveryInfo? ExtractStateRecovery(MigrationResult result)
    {
        var recovery = result.Skipped.FirstOrDefault(s => s.RuleId == StateRecoveryRuleId);
        if (recovery is null) return null;

        // The reason message produced by StatefulMigrationEngine.LoadAndHash looks like:
        //   "State file was corrupt and archived to {backupPath}. Re-evaluating all rules."
        // OR (if archive failed):
        //   "State file was corrupt; archive failed, leaving file in place. Re-evaluating all rules."
        string? archivedTo = null;
        const string marker = "archived to ";
        var idx = recovery.Reason.IndexOf(marker, StringComparison.Ordinal);
        if (idx >= 0)
        {
            var rest = recovery.Reason[(idx + marker.Length)..];
            var dot  = rest.IndexOf('.');
            archivedTo = dot >= 0 ? rest[..dot] : rest;
        }

        return new StateRecoveryInfo
        {
            ArchivedTo = archivedTo,
            Note       = recovery.Reason,
        };
    }

    /// <summary>Looks up a rule's kind by id (best-effort; null when not found).</summary>
    private static string? ResolveRuleKind(MigrationSchema schema, string ruleId)
    {
        var rule = schema.Rules.FirstOrDefault(r => r.Id == ruleId);
        if (rule is null) return null;
        var s = rule.Kind.ToString();
        return char.ToLowerInvariant(s[0]) + s[1..];
    }

    // ── Dry-run diff helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a unified-style diff for every rewritten file. The full diff is written to
    /// <c>{projectDir}/.wrapgod/dryrun-&lt;timestamp&gt;.diff</c>; the inline preview returned
    /// in <see cref="DryRunDiff.InlinePerFile"/> is truncated at
    /// <see cref="MaxInlineDiffLinesPerFile"/> lines per file.
    /// </summary>
    private static DryRunDiff BuildDryRunDiff(
        string projectDir,
        IReadOnlyDictionary<string, string> rewrittenFiles,
        Dictionary<string, string> preRunContents)
    {
        var full = new StringBuilder();
        var inline = new Dictionary<string, string>(StringComparer.Ordinal);
        var truncated = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (path, newText) in rewrittenFiles)
        {
            if (!preRunContents.TryGetValue(path, out var oldText))
                continue;

            var rel  = TryMakeRelative(projectDir, path);
            var diff = ComputeBasicUnifiedDiff(rel, oldText, newText);
            full.Append(diff);

            // Truncate for inline display.
            var lines = diff.Split('\n');
            if (lines.Length > MaxInlineDiffLinesPerFile)
            {
                inline[path] = string.Join('\n', lines.Take(MaxInlineDiffLinesPerFile));
                truncated[path] = lines.Length - MaxInlineDiffLinesPerFile;
            }
            else
            {
                inline[path] = diff;
            }
        }

        // Write the full diff to a side file.
        string? dumpPath = null;
        try
        {
            var dumpDir = Path.Combine(projectDir, ".wrapgod");
            Directory.CreateDirectory(dumpDir);
            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", System.Globalization.CultureInfo.InvariantCulture);
            dumpPath = Path.Combine(dumpDir, $"dryrun-{stamp}.diff");
            File.WriteAllText(dumpPath, full.ToString());
        }
        catch
        {
            dumpPath = null; // best-effort
        }

        return new DryRunDiff
        {
            DumpFilePath  = dumpPath,
            InlinePerFile = inline,
            TruncatedLinesPerFile = truncated,
        };
    }

    /// <summary>
    /// Best-effort relative path of <paramref name="full"/> rooted at <paramref name="baseDir"/>,
    /// falling back to the absolute path on failure.
    /// </summary>
    private static string TryMakeRelative(string baseDir, string full)
    {
        try { return Path.GetRelativePath(baseDir, full).Replace('\\', '/'); }
        catch { return full; }
    }

    /// <summary>
    /// Produces a minimal unified-diff-style text. This is intentionally simple — full
    /// Myers-style hunking is out of scope for this CLI; CI consumers should use the dump
    /// file path for high-fidelity diffs. The output is sufficient for humans to spot the
    /// nature of the change.
    /// </summary>
    /// <remarks>
    /// Format:
    /// <code>
    /// --- a/path/to/file.cs
    /// +++ b/path/to/file.cs
    /// -old line 1
    /// -old line 2
    /// +new line 1
    /// +new line 2
    /// </code>
    /// A proper Myers/LCS hunked diff is filed as a follow-up (see docs/migration/applying.md).
    /// </remarks>
    private static string ComputeBasicUnifiedDiff(string relPath, string oldText, string newText)
    {
        var sb = new StringBuilder();
        sb.Append("--- a/").Append(relPath).Append('\n');
        sb.Append("+++ b/").Append(relPath).Append('\n');

        var oldLines = oldText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var newLines = newText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        // Skip identical leading/trailing lines so the diff is not noisy.
        var prefix = 0;
        var max    = Math.Min(oldLines.Length, newLines.Length);
        while (prefix < max && oldLines[prefix] == newLines[prefix]) prefix++;

        var suffix = 0;
        while (suffix < (max - prefix) &&
               oldLines[oldLines.Length - 1 - suffix] == newLines[newLines.Length - 1 - suffix])
        {
            suffix++;
        }

        for (var i = prefix; i < oldLines.Length - suffix; i++)
            sb.Append('-').Append(oldLines[i]).Append('\n');
        for (var i = prefix; i < newLines.Length - suffix; i++)
            sb.Append('+').Append(newLines[i]).Append('\n');

        return sb.ToString();
    }

    // ── Output helpers ────────────────────────────────────────────────────────────────────────

    private static void PrintHumanSummary(
        ApplySummary summary,
        MigrationSchema schema,
        string schemaName,
        bool dryRun)
    {
        // ── State-recovery banner (must be IMPOSSIBLE to miss) ────────────────────────────
        if (summary.StateRecovery is not null)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("WARNING: Prior state file was corrupt.");
            if (!string.IsNullOrEmpty(summary.StateRecovery.ArchivedTo))
                Console.WriteLine($"  Archived to: {summary.StateRecovery.ArchivedTo}");
            Console.WriteLine("  Re-evaluating all rules from scratch.");
            Console.WriteLine("============================================================");
            Console.WriteLine();
        }

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

        if (summary.SkippedEntries.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Skipped:");
            foreach (var s in summary.SkippedEntries)
                Console.WriteLine($"  {s.RuleId} {s.File}:{s.Line}  {s.Reason}");
        }

        if (summary.ManualEntries.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Manual:");
            foreach (var m in summary.ManualEntries)
            {
                var matched = m.MatchedFiles.Count > 0
                    ? string.Join(", ", m.MatchedFiles)
                    : "(no files matched)";
                Console.WriteLine($"  {m.RuleId} {m.Note}");
                Console.WriteLine($"    matched in: {matched}");
            }
        }

        // ── Dry-run unified-diff preview ───────────────────────────────────────────────────
        if (dryRun && summary.DryRunDiff is not null && summary.DryRunDiff.InlinePerFile.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Preview (per file, truncated):");
            foreach (var (path, diff) in summary.DryRunDiff.InlinePerFile)
            {
                Console.WriteLine();
                Console.Write(diff);
                if (summary.DryRunDiff.TruncatedLinesPerFile.TryGetValue(path, out var more) && more > 0)
                {
                    var dumpHint = summary.DryRunDiff.DumpFilePath is null
                        ? "see dump file"
                        : $"see {summary.DryRunDiff.DumpFilePath}";
                    Console.WriteLine($"... ({more} more diff lines truncated; {dumpHint} for full diff)");
                }
            }

            if (summary.DryRunDiff.DumpFilePath is not null)
            {
                Console.WriteLine();
                Console.WriteLine($"Full diff dumped to: {summary.DryRunDiff.DumpFilePath}");
            }
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
            dryRun         = summary.DryRun,
            filesScanned   = summary.FilesScanned,
            filesModified  = summary.FilesModified,
            applied        = summary.Applied,
            skipped        = summary.Skipped,
            manual         = summary.Manual,
            message        = summary.Message,
            stateRecovered = summary.StateRecovery is null
                ? null
                : (object)new
                {
                    archivedTo = summary.StateRecovery.ArchivedTo,
                    note       = summary.StateRecovery.Note,
                },
            skippedDetails = summary.SkippedEntries.Select(s => new
            {
                ruleId = s.RuleId,
                file   = s.File,
                line   = s.Line,
                reason = s.Reason,
            }).ToList(),
            manualDetails = summary.ManualEntries.Select(m => new
            {
                ruleId       = m.RuleId,
                note         = m.Note,
                matchedFiles = m.MatchedFiles,
            }).ToList(),
            appliedByRule = summary.AppliedByRule.Select(a => new
            {
                ruleId    = a.RuleId,
                kind      = a.Kind,
                fileCount = a.FileCount,
                count     = a.Count,
            }).ToList(),
            dryRunDiff = summary.DryRunDiff is null
                ? null
                : (object)new
                {
                    dumpFilePath  = summary.DryRunDiff.DumpFilePath,
                    inlinePerFile = summary.DryRunDiff.InlinePerFile.Select(kvp => new
                    {
                        file = kvp.Key,
                        diff = kvp.Value,
                    }).ToList(),
                },
        };
        Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
    }

    // ── Internal data ─────────────────────────────────────────────────────────────────────────
    // The following private DTO classes are pure data carriers (init-only properties, no
    // logic).  They are exercised by every JSON/human assertion in MigrateApplyCliTests but
    // Coverlet attributes setter-default expressions ("= string.Empty;") as separate lines
    // that report as untouched.  Excluded from coverage per Coverlet convention for plain
    // data-holder types.

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private sealed class ApplySummary
    {
        public bool DryRun { get; init; }
        public int FilesScanned { get; init; }
        public int FilesModified { get; init; }
        public int Applied { get; init; }
        public int Skipped { get; init; }
        public int Manual { get; init; }
        public string? Message { get; init; }
        public StateRecoveryInfo? StateRecovery { get; init; }
        public List<SkippedEntry> SkippedEntries { get; init; } = [];
        public List<ManualEntry> ManualEntries { get; init; } = [];
        public List<AppliedByRuleEntry> AppliedByRule { get; init; } = [];
        public DryRunDiff? DryRunDiff { get; init; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private sealed class StateRecoveryInfo
    {
        public string? ArchivedTo { get; init; }
        public string Note { get; init; } = string.Empty;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private sealed class SkippedEntry
    {
        public string RuleId { get; init; } = string.Empty;
        public string File { get; init; } = string.Empty;
        public int Line { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private sealed class ManualEntry
    {
        public string RuleId { get; init; } = string.Empty;
        public string Note { get; init; } = string.Empty;
        public List<string> MatchedFiles { get; init; } = [];
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private sealed class AppliedByRuleEntry
    {
        public string RuleId { get; init; } = string.Empty;
        public string? Kind { get; init; }
        public int FileCount { get; init; }
        public int Count { get; init; }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private sealed class DryRunDiff
    {
        public string? DumpFilePath { get; init; }
        public Dictionary<string, string> InlinePerFile { get; init; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> TruncatedLinesPerFile { get; init; } = new(StringComparer.Ordinal);
    }
}
