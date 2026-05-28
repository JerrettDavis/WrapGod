using System.CommandLine;
using System.Globalization;
using System.Text.Json;
using WrapGod.Cli.Verification;
using WrapGod.Migration;
using WrapGod.Migration.Engine.State;

namespace WrapGod.Cli;

/// <summary>
/// Implements <c>wrap-god migrate verify [options]</c>.
///
/// Optionally invokes <c>dotnet build</c>, parses the compiler diagnostic output, and
/// attributes each diagnostic to the nearest migration rule in the state file via
/// ±3-line proximity. The command is explicitly non-gating — it never blocks
/// <c>migrate apply</c> and always exits 0 unless argument parsing fails.
///
/// Exit codes:
/// <list type="bullet">
///   <item><description>0 – verify ran (even if there are attributed errors)</description></item>
///   <item><description>1 – IO error: baseline file not found, schema not found, etc.</description></item>
///   <item><description>2 – bad arguments</description></item>
/// </list>
/// </summary>
internal static class MigrateVerifyCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = true,
    };

    // ── Command factory ──────────────────────────────────────────────────────

    public static Command Create() => Create(new DotnetBuildRunner());

    internal static Command Create(IBuildRunner buildRunner)
    {
        var schemaOption = new Option<FileInfo?>(
            ["--schema", "-s"],
            "Path to the migration schema JSON file. The state file is the sibling <schema>.state.json. " +
            "When omitted, the command searches --project-dir for *.wrapgod-migration.json.state.json files.");

        var projectDirOption = new Option<DirectoryInfo?>(
            ["--project-dir", "-p"],
            "Project root directory. Used to find the .csproj and to auto-detect state files when " +
            "--schema is omitted. Defaults to the current directory.");

        var noBuildOption = new Option<bool>(
            "--no-build",
            "Skip the dotnet build invocation. Only reports the state-file summary. " +
            "Useful in CI when the build runs as a separate job.");

        var jsonOption = new Option<bool>(
            "--json",
            "Emit output as JSON instead of human-readable text.");

        var verboseOption = new Option<bool>(
            ["--verbose", "-v"],
            "Include per-diagnostic details in human-readable mode.");

        var buildConfigOption = new Option<string>(
            "--build-config",
            () => "Debug",
            "Build configuration passed to dotnet build (Debug or Release).");

        var baselineOption = new Option<FileInfo?>(
            "--baseline",
            "Path to a pre-migration diagnostic baseline JSON file. " +
            "When provided, diagnostics present in the baseline are classified as pre-existing.");

        var command = new Command("verify", "Optionally build the project and correlate compiler diagnostics to migration rules")
        {
            schemaOption,
            projectDirOption,
            noBuildOption,
            jsonOption,
            verboseOption,
            buildConfigOption,
            baselineOption,
        };

        command.SetHandler(async (context) =>
        {
            var schema      = context.ParseResult.GetValueForOption(schemaOption);
            var projectDir  = context.ParseResult.GetValueForOption(projectDirOption);
            var noBuild     = context.ParseResult.GetValueForOption(noBuildOption);
            var json        = context.ParseResult.GetValueForOption(jsonOption);
            var verbose     = context.ParseResult.GetValueForOption(verboseOption);
            var buildConfig = context.ParseResult.GetValueForOption(buildConfigOption)!;
            var baseline    = context.ParseResult.GetValueForOption(baselineOption);

            context.ExitCode = await HandleAsync(
                schema, projectDir, noBuild, json, verbose, buildConfig, baseline,
                buildRunner, context.GetCancellationToken());
        });

        return command;
    }

    // ── Handler ─────────────────────────────────────────────────────────────

    internal static async Task<int> HandleAsync(
        FileInfo? schemaOption,
        DirectoryInfo? projectDirOption,
        bool noBuild,
        bool jsonOutput,
        bool verbose,
        string buildConfig,
        FileInfo? baselineFile,
        IBuildRunner buildRunner,
        CancellationToken cancellationToken = default)
    {
        var projectDir = projectDirOption?.FullName ?? Directory.GetCurrentDirectory();

        // ── 1. Resolve schema path ───────────────────────────────────────────
        string? schemaPath = null;
        if (schemaOption is not null)
        {
            schemaPath = schemaOption.FullName;
            if (!File.Exists(schemaPath))
            {
                Console.Error.WriteLine($"Error: schema file not found: {schemaPath}");
                return 1;
            }
        }
        else
        {
            // Auto-detect: look for *.wrapgod-migration.json.state.json in project-dir
            schemaPath = AutoDetectSchemaPath(projectDir);
            if (schemaPath is null)
            {
                // Graceful: no state file → nothing to verify.
                Console.Error.WriteLine("No migration state found. Run 'migrate apply' first.");
                return 0;
            }
        }

        // ── 2. Load state ────────────────────────────────────────────────────
        var state = MigrationStateStore.Load(schemaPath, out var wasCorrupt, out _);
        if (wasCorrupt)
        {
            Console.Error.WriteLine("Warning: migration state file was corrupt and has been archived. " +
                "Re-run 'migrate apply' to regenerate state.");
            // Degraded but non-fatal
        }

        if (state is null)
        {
            Console.Error.WriteLine("No migration state; nothing to verify.");
            return 0;
        }

        // ── 3. Schema hash check ─────────────────────────────────────────────
        bool schemaChanged = false;
        if (File.Exists(schemaPath))
        {
            try
            {
                var schemaJson = await File.ReadAllTextAsync(schemaPath, cancellationToken).ConfigureAwait(false);
                var currentHash = MigrationStateStore.ComputeSchemaHash(schemaJson);
                schemaChanged = state.SchemaHasChanged(currentHash);
            }
            catch (IOException)
            {
                // Best-effort — skip hash check
            }
        }

        // ── 4. Load baseline ─────────────────────────────────────────────────
        IReadOnlyList<Verification.CompilerDiagnostic>? baselineDiags = null;
        if (baselineFile is not null)
        {
            if (!baselineFile.Exists)
            {
                Console.Error.WriteLine($"Error: baseline file not found: {baselineFile.FullName}");
                return 1;
            }

            try
            {
                var baselineJson = await File.ReadAllTextAsync(baselineFile.FullName, cancellationToken)
                    .ConfigureAwait(false);
                baselineDiags = LoadBaselineDiagnostics(baselineJson);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading baseline file: {ex.Message}");
                return 1;
            }
        }

        // ── 5. Build step ────────────────────────────────────────────────────
        BuildResult? buildResult = null;
        IReadOnlyList<Verification.CompilerDiagnostic> diagnostics = [];

        if (!noBuild)
        {
            buildResult = await buildRunner.RunAsync(projectDir, buildConfig, cancellationToken)
                .ConfigureAwait(false);

            if (!buildResult.Launched)
            {
                Console.Error.WriteLine($"dotnet build not found; skipping verify. ({buildResult.LaunchError})");
                // Graceful degradation — exit 0 per plan §a.
                if (jsonOutput)
                    PrintJson(schemaPath, projectDir, state, schemaChanged, null, null, [], []);
                return 0;
            }

            // Parse diagnostics; log unparseable lines as warnings when verbose.
            diagnostics = ParseDiagnosticsWithWarnings(buildResult.Output, verbose);
        }

        // ── 6. Attribute diagnostics to rules ────────────────────────────────
        var attributions = RuleAttributor.Attribute(diagnostics, state.Applied, baselineDiags);

        // ── 7. Emit report ───────────────────────────────────────────────────
        if (jsonOutput)
            PrintJson(schemaPath, projectDir, state, schemaChanged, buildResult, noBuild, attributions, baselineDiags ?? []);
        else
            PrintHuman(schemaPath, projectDir, state, schemaChanged, buildResult, noBuild, attributions, baselineDiags ?? [], verbose);

        return 0;
    }

    // ── Auto-detect helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Searches <paramref name="projectDir"/> and its parent directory for
    /// <c>*.wrapgod-migration.json.state.json</c> files and returns the schema path
    /// associated with the most recently written state file.
    /// </summary>
    /// <returns>
    /// The schema path (i.e. the state path with the <c>.state.json</c> suffix
    /// stripped) when both the state and its schema sibling exist. When the schema
    /// sibling is missing, returns the state-file path itself so the caller can
    /// pass it to <see cref="MigrationStateStore.Load(string)"/> directly —
    /// <c>Load</c> takes a schema path and appends <c>.state.json</c>, so we hand
    /// it the original schema path. Returns <see langword="null"/> only when no
    /// state files are found at all.
    /// </returns>
    private static string? AutoDetectSchemaPath(string projectDir)
    {
        // Search projectDir and its parent for *.wrapgod-migration.json.state.json
        var stateFiles = new List<string>();

        var searchDirs = new[] { projectDir, Directory.GetParent(projectDir)?.FullName }
            .Where(d => d is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var dir in searchDirs)
        {
            if (dir is null) continue;
            try
            {
                stateFiles.AddRange(
                    Directory.GetFiles(dir, "*.wrapgod-migration.json.state.json",
                        SearchOption.TopDirectoryOnly));
            }
            catch (IOException)
            {
                // Skip unreadable directories
            }
        }

        if (stateFiles.Count == 0)
            return null;

        // Pick most recently written state file; strip the .state.json suffix to get the schema path.
        var mostRecent = stateFiles
            .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
            .First();

        const string stateSuffix = ".state.json";
        if (mostRecent.EndsWith(stateSuffix, StringComparison.OrdinalIgnoreCase))
        {
            // Strip the .state.json suffix to recover the schema path.
            var candidateSchema = mostRecent[..^stateSuffix.Length];

            // Return the schema path even when the schema file is missing. The state file
            // contains enough information for verify to run in state-only mode (similar to
            // --no-build). The caller's schema-hash check is wrapped in an existence guard
            // so a missing schema simply skips the schema-changed warning. Returning the
            // schema path here lets MigrationStateStore.Load() find the state sibling.
            return candidateSchema;
        }

        // Last-resort: the state file did not end with .state.json (shouldn't happen
        // given the glob filter, but defensive). Return null to fall back to the
        // "no migration state" branch.
        return null;
    }

    // ── Diagnostic helpers ───────────────────────────────────────────────────

    private static List<Verification.CompilerDiagnostic> ParseDiagnosticsWithWarnings(
        string buildOutput, bool verbose)
    {
        var parsed = new List<Verification.CompilerDiagnostic>();
        var regex  = Verification.DiagnosticParser.Parse(buildOutput);
        parsed.AddRange(regex);

        if (verbose)
        {
            // Log lines that matched nothing but look like they could be diagnostics.
            foreach (var line in buildOutput.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                if (trimmed.Contains("): error") || trimmed.Contains("): warning"))
                {
                    // Check if this line produced a parsed diagnostic
                    var alreadyParsed = parsed.Any(d => d.RawLine == line.TrimEnd('\r'));
                    if (!alreadyParsed)
                        Console.Error.WriteLine($"[verify] Could not parse diagnostic line: {trimmed}");
                }
            }
        }

        return parsed;
    }

    private static readonly JsonSerializerOptions BaselineDeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static List<Verification.CompilerDiagnostic> LoadBaselineDiagnostics(string json)
    {
        // Baseline is a JSON array of objects with properties matching CompilerDiagnostic.
        var items = JsonSerializer.Deserialize<BaselineDiagnosticDto[]>(
            json,
            BaselineDeserializeOptions);

        if (items is null) return [];

        return items.Select(d => new Verification.CompilerDiagnostic
        {
            FilePath = d.FilePath,
            Line     = d.Line,
            Column   = d.Column,
            Severity = Enum.TryParse<Verification.DiagnosticSeverity>(d.Severity, ignoreCase: true, out var sev)
                ? sev : Verification.DiagnosticSeverity.Error,
            Code     = d.Code ?? string.Empty,
            Message  = d.Message ?? string.Empty,
            RawLine  = string.Empty,
        }).ToList();
    }

    // ── Human-readable output ────────────────────────────────────────────────

    private static void PrintHuman(
        string schemaPath,
        string projectDir,
        MigrationState state,
        bool schemaChanged,
        BuildResult? buildResult,
        bool noBuild,
        IReadOnlyList<DiagnosticAttribution> attributions,
        IReadOnlyList<Verification.CompilerDiagnostic> baseline,
        bool verbose)
    {
        const string header = "WrapGod migrate verify";
        Console.WriteLine(header);
        Console.WriteLine(new string('-', header.Length));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Project:  {0}", projectDir));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Schema:   {0}", Path.GetFileName(schemaPath)));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Baseline: {0}", baseline.Count > 0 ? $"{baseline.Count} pre-migration diagnostics" : "(none)"));
        Console.WriteLine();

        if (schemaChanged)
            Console.WriteLine("WARNING: Schema has changed since last apply. Attribution may be inaccurate.");

        if (noBuild)
        {
            Console.WriteLine("Build:    SKIPPED (--no-build)");
            PrintStateSummary(state);
            return;
        }

        if (buildResult is null)
        {
            Console.WriteLine("Build:    NOT RUN");
            return;
        }

        var errors   = attributions.Count(a => a.Diagnostic.Severity == Verification.DiagnosticSeverity.Error);
        var warnings = attributions.Count(a => a.Diagnostic.Severity == Verification.DiagnosticSeverity.Warning);
        var buildStatus = buildResult.ExitCode == 0 ? "SUCCEEDED" : "FAILED";
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "Build:    {0} ({1} error(s), {2} warning(s))", buildStatus, errors, warnings));
        Console.WriteLine();

        // Attribution section
        var attributed = attributions
            .Where(a => a.AttributedRuleId is not null && !a.IsPreExisting)
            .ToList();
        var unattributed = attributions
            .Where(a => a.AttributedRuleId is null && !a.IsPreExisting)
            .ToList();
        var preExisting = attributions
            .Where(a => a.IsPreExisting)
            .ToList();

        Console.WriteLine("Attribution:");

        if (attributed.Count == 0 && unattributed.Count == 0 && preExisting.Count == 0)
        {
            Console.WriteLine("  No errors or warnings.");
            return;
        }

        // Group attributed by rule id
        var byRule = attributed
            .GroupBy(a => a.AttributedRuleId!, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in byRule)
        {
            var errorCount = group.Count(a => a.Diagnostic.Severity == Verification.DiagnosticSeverity.Error);
            var warnCount  = group.Count(a => a.Diagnostic.Severity == Verification.DiagnosticSeverity.Warning);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  {0}   {1} error(s), {2} warning(s)", group.Key, errorCount, warnCount));

            if (verbose)
            {
                foreach (var a in group.OrderBy(a => a.Diagnostic.FilePath, StringComparer.OrdinalIgnoreCase)
                                       .ThenBy(a => a.Diagnostic.Line))
                {
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                        "    {0}:{1}  {2} {3}",
                        a.Diagnostic.FilePath, a.Diagnostic.Line,
                        a.Diagnostic.Code, a.Diagnostic.Message));
                }
            }
        }

        var unattributedErrors   = unattributed.Count(a => a.Diagnostic.Severity == Verification.DiagnosticSeverity.Error);
        var unattributedWarnings = unattributed.Count(a => a.Diagnostic.Severity == Verification.DiagnosticSeverity.Warning);
        if (unattributed.Count > 0)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  Unattributed: {0} error(s), {1} warning(s) (likely pre-existing or unrelated)",
                unattributedErrors, unattributedWarnings));
        }

        if (preExisting.Count > 0)
        {
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  Pre-existing (from baseline): {0} diagnostic(s)", preExisting.Count));
        }
    }

    private static void PrintStateSummary(MigrationState state)
    {
        Console.WriteLine();
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "State:  {0} applied, {1} skipped, {2} manual",
            state.Summary.Applied, state.Summary.Skipped, state.Summary.Manual));
    }

    // ── JSON output ──────────────────────────────────────────────────────────

    private static void PrintJson(
        string schemaPath,
        string projectDir,
        MigrationState state,
        bool schemaChanged,
        BuildResult? buildResult,
        bool? noBuild,
        IReadOnlyList<DiagnosticAttribution> attributions,
        IReadOnlyList<Verification.CompilerDiagnostic> baseline)
    {
        var attribution = attributions
            .Where(a => a.AttributedRuleId is not null && !a.IsPreExisting)
            .GroupBy(a => a.AttributedRuleId!, StringComparer.Ordinal)
            .Select(g => new
            {
                ruleId   = g.Key,
                errors   = g.Count(a => a.Diagnostic.Severity == Verification.DiagnosticSeverity.Error),
                warnings = g.Count(a => a.Diagnostic.Severity == Verification.DiagnosticSeverity.Warning),
                diagnostics = g.Select(a => new
                {
                    file     = a.Diagnostic.FilePath,
                    line     = a.Diagnostic.Line,
                    code     = a.Diagnostic.Code,
                    message  = a.Diagnostic.Message,
                    severity = a.Diagnostic.Severity.ToString().ToLowerInvariant(),
                }).ToList(),
            })
            .OrderBy(e => e.ruleId, StringComparer.Ordinal)
            .ToList();

        var unattributed = attributions
            .Where(a => a.AttributedRuleId is null && !a.IsPreExisting)
            .Select(a => new
            {
                file     = a.Diagnostic.FilePath,
                line     = a.Diagnostic.Line,
                code     = a.Diagnostic.Code,
                message  = a.Diagnostic.Message,
                severity = a.Diagnostic.Severity.ToString().ToLowerInvariant(),
            })
            .ToList();

        // Build a structured sentinel for the `build` key so consumers never have to
        // null-deref. When --no-build is set OR the build runner failed to launch,
        // `skipped` is true and `exitCode` is null; otherwise it carries the real
        // exit code + diagnostic counts.
        var skipped     = noBuild == true || buildResult is null;
        var skipReason  = noBuild == true
            ? "--no-build flag set"
            : buildResult is null
                ? "build was not invoked"
                : null;

        object buildBlock = skipped
            ? new
            {
                skipped     = true,
                reason      = skipReason,
                exitCode    = (int?)null,
                launched    = (bool?)null,
                errors      = 0,
                warnings    = 0,
            }
            : new
            {
                skipped     = false,
                reason      = (string?)null,
                exitCode    = (int?)buildResult!.ExitCode,
                launched    = (bool?)buildResult!.Launched,
                errors      = attributions.Count(a => a.Diagnostic.Severity == Verification.DiagnosticSeverity.Error),
                warnings    = attributions.Count(a => a.Diagnostic.Severity == Verification.DiagnosticSeverity.Warning),
            };

        var output = new
        {
            schema        = Path.GetFileName(schemaPath),
            projectDir,
            schemaChanged,
            noBuild       = noBuild ?? false,
            build         = buildBlock,
            baselineDiagnosticsLoaded = baseline.Count,
            state = new
            {
                applied = state.Summary.Applied,
                skipped = state.Summary.Skipped,
                manual  = state.Summary.Manual,
            },
            attribution,
            unattributed,
        };

        Console.WriteLine(JsonSerializer.Serialize(output, JsonOptions));
    }

    // ── Private DTOs ─────────────────────────────────────────────────────────

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private sealed class BaselineDiagnosticDto
    {
        public string? FilePath { get; init; }
        public int Line { get; init; }
        public int Column { get; init; }
        public string? Severity { get; init; }
        public string? Code { get; init; }
        public string? Message { get; init; }
    }
}
