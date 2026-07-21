using System.CommandLine;
using System.Text.Json;
using WrapGod.Extractor;
using WrapGod.Migration;
using WrapGod.Migration.Generation;

namespace WrapGod.Cli;

internal static class MigrateGenerateCommand
{
    // Plan §4.4 requires explicit CamelCase. Without an explicit policy, the JSON output
    // would rely on anonymous-type field casing as a coincidence; future field additions
    // could silently break the convention.
    private static readonly JsonSerializerOptions JsonSummaryOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static Command Create()
    {
        var packageOption = new Option<string?>("--package")
        {
            Description = "NuGet package ID (e.g. Serilog). Mutually exclusive with --from-assembly/--to-assembly."
        };

        var fromOption = new Option<string>("--from")
        {
            Description = "Source version (e.g. 2.12.0). Required in both modes.",
            Required = true
        };

        var toOption = new Option<string>("--to")
        {
            Description = "Target version (e.g. 3.1.1). Required in both modes.",
            Required = true
        };

        var fromAssemblyOption = new Option<FileInfo?>("--from-assembly")
        {
            Description = "Path to the baseline assembly DLL. Mutually exclusive with --package."
        };

        var toAssemblyOption = new Option<FileInfo?>("--to-assembly")
        {
            Description = "Path to the target assembly DLL. Mutually exclusive with --package."
        };

        var outputOption = new Option<string?>("--output", "-o")
        {
            Description = "Output path for the draft migration schema JSON. Defaults to {library}.{from}-to-{to}.wrapgod-migration.json."
        };

        var sourceOption = new Option<string?>("--source")
        {
            Description = "Private NuGet feed URL (defaults to nuget.org)."
        };

        var tfmOption = new Option<string?>("--tfm")
        {
            Description = "Target framework moniker override (e.g. net8.0)."
        };

        var ruleIdPrefixOption = new Option<string?>("--rule-id-prefix")
        {
            Description = "Prefix for generated rule IDs (e.g. 'SLG' -> 'SLG-001'). Defaults to a prefix derived from the library name."
        };

        var noRenameDetectionOption = new Option<bool>("--no-rename-detection")
        {
            Description = "Disable rename detection. Every removed type/member emits a RemoveMemberRule."
        };

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Emit the final summary as JSON to stdout instead of human-readable text."
        };

        var command = new Command("generate", "Produce a draft MigrationSchema JSON by diffing two NuGet versions or two local assemblies")
        {
            packageOption,
            fromOption,
            toOption,
            fromAssemblyOption,
            toAssemblyOption,
            outputOption,
            sourceOption,
            tfmOption,
            ruleIdPrefixOption,
            noRenameDetectionOption,
            jsonOption,
        };

        command.SetAction((parseResult, cancellationToken) =>
        {
            var package = parseResult.GetValue(packageOption);
            var from = parseResult.GetValue(fromOption)!;
            var to = parseResult.GetValue(toOption)!;
            var fromAssembly = parseResult.GetValue(fromAssemblyOption);
            var toAssembly = parseResult.GetValue(toAssemblyOption);
            var output = parseResult.GetValue(outputOption);
            var source = parseResult.GetValue(sourceOption);
            var tfm = parseResult.GetValue(tfmOption);
            var ruleIdPrefix = parseResult.GetValue(ruleIdPrefixOption);
            var noRenameDetection = parseResult.GetValue(noRenameDetectionOption);
            var json = parseResult.GetValue(jsonOption);

            return HandleAsync(
                package, from, to, fromAssembly, toAssembly,
                output, source, tfm, ruleIdPrefix, noRenameDetection, json,
                cancellationToken);
        });

        return command;
    }

    private static async Task<int> HandleAsync(
        string? package,
        string from,
        string to,
        FileInfo? fromAssembly,
        FileInfo? toAssembly,
        string? outputPath,
        string? sourceFeed,
        string? tfm,
        string? ruleIdPrefix,
        bool disableRenameDetection,
        bool jsonSummary,
        CancellationToken cancellationToken)
    {
        // ── Validate mode ────────────────────────────────────────────────────────────────────
        var hasPackage = !string.IsNullOrWhiteSpace(package);
        var hasAssemblies = fromAssembly is not null || toAssembly is not null;

        if (hasPackage && hasAssemblies)
        {
            Console.Error.WriteLine("Error: --package and --from-assembly/--to-assembly are mutually exclusive. Specify one mode only.");
            return 2;
        }

        if (!hasPackage && !hasAssemblies)
        {
            Console.Error.WriteLine("Error: Either --package (with --from and --to) or --from-assembly and --to-assembly must be specified.");
            return 2;
        }

        if (hasAssemblies && (fromAssembly is null || toAssembly is null))
        {
            Console.Error.WriteLine("Error: Both --from-assembly and --to-assembly must be specified when using assembly mode.");
            return 2;
        }

        // ── Validate versions look reasonable ────────────────────────────────────────────────
        if (!IsValidVersionString(from))
        {
            Console.Error.WriteLine($"Error: '--from {from}' does not look like a valid version string (e.g. 1.2.3 or 1.2.3-preview.1).");
            return 1;
        }

        if (!IsValidVersionString(to))
        {
            Console.Error.WriteLine($"Error: '--to {to}' does not look like a valid version string (e.g. 1.2.3 or 1.2.3-preview.1).");
            return 1;
        }

        // ── Determine library name and output path ────────────────────────────────────────────
        var library = hasPackage ? package! : Path.GetFileNameWithoutExtension(fromAssembly!.Name);

        var resolvedOutput = outputPath
            ?? $"{library}.{from}-to-{to}.wrapgod-migration.json";

        // ── Check output file doesn't already exist ───────────────────────────────────────────
        if (File.Exists(resolvedOutput))
        {
            Console.Error.WriteLine($"Error: Output file already exists: {resolvedOutput}");
            Console.Error.WriteLine("Remove it or specify a different --output path.");
            return 1;
        }

        // ── Resolve assemblies ────────────────────────────────────────────────────────────────
        MultiVersionExtractor.MultiVersionResult result;
        try
        {
            if (hasPackage)
            {
                result = await ResolveFromNuGetAsync(package!, from, to, tfm, sourceFeed, cancellationToken);
            }
            else
            {
                // Local assembly mode
                if (!fromAssembly!.Exists)
                {
                    Console.Error.WriteLine($"Error: --from-assembly file not found: {fromAssembly.FullName}");
                    return 1;
                }

                if (!toAssembly!.Exists)
                {
                    Console.Error.WriteLine($"Error: --to-assembly file not found: {toAssembly.FullName}");
                    return 1;
                }

                result = ResolveFromLocalAssemblies(fromAssembly, toAssembly, from, to);
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        // ── Build stable-ID lookup map for better type names in rules ─────────────────────────
        var stableIdToFullName = BuildStableIdMap(result.MergedManifest);

        // ── Generate migration schema ─────────────────────────────────────────────────────────
        var options = new MigrationSchemaGeneratorOptions
        {
            RuleIdPrefix = ruleIdPrefix,
            DisableRenameDetection = disableRenameDetection,
        };

        MigrationSchema schema;
        try
        {
            schema = MigrationSchemaGenerator.FromDiff(result.Diff, library, options, stableIdToFullName);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error generating schema: {ex.Message}");
            return 1;
        }

        schema.From = from;
        schema.To = to;
        schema.GeneratedFrom = "manifest-diff";

        // ── Write output (temp-then-rename so Ctrl+C never leaves a partial file) ─────────────
        var schemaJson = MigrationSchemaSerializer.Serialize(schema);
        var tempPath = resolvedOutput + ".tmp";

        try
        {
            var dir = Path.GetDirectoryName(resolvedOutput);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(tempPath, schemaJson, cancellationToken);
            // Atomic rename; throws if destination exists (we already guarded above).
            File.Move(tempPath, resolvedOutput, overwrite: false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error writing output file '{resolvedOutput}': {ex.Message}");
            return 1;
        }
        finally
        {
            // Always clean up the temp file if it still exists (cancellation, exception, etc.)
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
            }
        }

        // ── Zero-rules warning ────────────────────────────────────────────────────────────────
        if (schema.Rules.Count == 0)
        {
            Console.Error.WriteLine($"Warning: No breaking changes detected between {from} and {to}. The schema has 0 rules.");
        }

        // ── Summary output ────────────────────────────────────────────────────────────────────
        var autoCount = schema.Rules.Count(r => r.Confidence == RuleConfidence.Auto);
        var verifiedCount = schema.Rules.Count(r => r.Confidence == RuleConfidence.Verified);
        var manualCount = schema.Rules.Count(r => r.Confidence == RuleConfidence.Manual);

        if (jsonSummary)
        {
            var summary = new
            {
                library,
                from,
                to,
                outputPath = resolvedOutput,
                rules = new
                {
                    total = schema.Rules.Count,
                    byConfidence = new
                    {
                        auto = autoCount,
                        verified = verifiedCount,
                        manual = manualCount,
                    }
                }
            };
            Console.WriteLine(JsonSerializer.Serialize(summary, JsonSummaryOptions));
        }
        else
        {
            Console.WriteLine("WrapGod migrate generate");
            Console.WriteLine(new string('-', 40));
            Console.WriteLine($"Library:  {library}");
            Console.WriteLine($"From:     {from}");
            Console.WriteLine($"To:       {to}");
            Console.WriteLine($"Rules:    {schema.Rules.Count} total");
            Console.WriteLine($"  auto:      {autoCount}");
            Console.WriteLine($"  verified:  {verifiedCount}");
            Console.WriteLine($"  manual:    {manualCount}");
            Console.WriteLine($"Output:   {resolvedOutput}");
        }

        return 0;
    }

    private static async Task<MultiVersionExtractor.MultiVersionResult> ResolveFromNuGetAsync(
        string packageId,
        string fromVersion,
        string toVersion,
        string? tfm,
        string? sourceFeed,
        CancellationToken cancellationToken)
    {
        var extractor = new NuGetExtractor();
        var nugetResult = await extractor.ExtractMultiVersionAsync(
            packageId,
            [fromVersion, toVersion],
            tfm,
            sourceFeed,
            cancellationToken);

        return new MultiVersionExtractor.MultiVersionResult(nugetResult.MergedManifest, nugetResult.Diff);
    }

    private static MultiVersionExtractor.MultiVersionResult ResolveFromLocalAssemblies(
        FileInfo fromAssembly,
        FileInfo toAssembly,
        string fromVersion,
        string toVersion)
    {
        var versions = new[]
        {
            new MultiVersionExtractor.VersionInput(fromVersion, fromAssembly.FullName),
            new MultiVersionExtractor.VersionInput(toVersion, toAssembly.FullName),
        };

        return MultiVersionExtractor.Extract(versions);
    }

    private static Dictionary<string, string> BuildStableIdMap(WrapGod.Manifest.ApiManifest manifest)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in manifest.Types)
        {
            if (!string.IsNullOrEmpty(type.StableId) && !string.IsNullOrEmpty(type.FullName))
                map[type.StableId] = type.FullName;
        }
        return map;
    }

    private static bool IsValidVersionString(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        // Accept SemVer-like: digits.digits[.digits[.digits]][-prerelease][+build]
        // Must start with a digit.
        if (!char.IsDigit(version[0]))
            return false;

        // Split off pre-release/build suffix
        var coreVersion = version.Split(['-', '+'], 2)[0];
        var parts = coreVersion.Split('.');

        if (parts.Length < 2 || parts.Length > 4)
            return false;

        return parts.All(p => p.Length > 0 && p.All(char.IsDigit));
    }
}
