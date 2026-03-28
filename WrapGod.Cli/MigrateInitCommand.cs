using System.CommandLine;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WrapGod.Cli;

internal static class MigrateInitCommand
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static Command Create()
    {
        var projectDirOption = new Option<DirectoryInfo>(
            ["--project-dir", "-p"],
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Project directory to analyze");

        var manifestOption = new Option<FileInfo?>(
            ["--manifest", "-m"],
            "Path to the WrapGod manifest JSON file (auto-detects *.wrapgod.json)");

        var outputOption = new Option<string>(
            ["--output", "-o"],
            () => "migration-plan.json",
            "Output path for the migration plan");

        var command = new Command("init", "Analyze a project and generate a migration plan")
        {
            projectDirOption,
            manifestOption,
            outputOption
        };

        command.SetHandler(HandleAsync, projectDirOption, manifestOption, outputOption);

        var migrateCommand = new Command("migrate", "Migration tools for adopting WrapGod wrappers")
        {
            command
        };

        return migrateCommand;
    }

    private static async Task HandleAsync(DirectoryInfo projectDir, FileInfo? manifestFile, string outputPath)
    {
        Console.WriteLine("WrapGod Migration Wizard");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine();

        if (!projectDir.Exists)
        {
            Console.Error.WriteLine($"Project directory not found: {projectDir.FullName}");
            return;
        }

        // Auto-detect manifest
        if (manifestFile is null)
        {
            var found = Directory.GetFiles(projectDir.FullName, "*.wrapgod.json");
            if (found.Length > 0)
            {
                manifestFile = new FileInfo(found[0]);
                Console.WriteLine($"Auto-detected manifest: {manifestFile.Name}");
            }
        }

        // Load known wrapped types from manifest
        var wrappedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (manifestFile is not null && manifestFile.Exists)
        {
            var manifestJson = await File.ReadAllTextAsync(manifestFile.FullName);
            using var doc = JsonDocument.Parse(manifestJson);
            if (doc.RootElement.TryGetProperty("Types", out var typesElement) ||
                doc.RootElement.TryGetProperty("types", out typesElement))
            {
                foreach (var type in typesElement.EnumerateArray())
                {
                    var fullName = type.TryGetProperty("FullName", out var fn) ? fn.GetString()
                        : type.TryGetProperty("fullName", out fn) ? fn.GetString()
                        : null;
                    var name = type.TryGetProperty("Name", out var n) ? n.GetString()
                        : type.TryGetProperty("name", out n) ? n.GetString()
                        : null;

                    if (fullName is not null) wrappedTypes.Add(fullName);
                    if (name is not null) wrappedTypes.Add(name);
                }
            }

            Console.WriteLine($"Loaded {wrappedTypes.Count / 2} types from manifest.");
        }
        else
        {
            Console.WriteLine("No manifest found -- analyzing source files for common third-party usage patterns.");
        }

        Console.WriteLine($"Scanning project: {projectDir.FullName}");
        Console.WriteLine();

        // Scan C# source files for direct third-party type usage
        var csFiles = Directory.GetFiles(projectDir.FullName, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj" + Path.DirectorySeparatorChar) &&
                        !f.Contains("bin" + Path.DirectorySeparatorChar))
            .ToList();

        Console.WriteLine($"Found {csFiles.Count} source files to analyze.");

        var actions = new List<MigrationAction>();
        var typeUsageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in csFiles)
        {
            var content = await File.ReadAllTextAsync(file);
            var relativePath = Path.GetRelativePath(projectDir.FullName, file);

            foreach (var typeName in wrappedTypes)
            {
                if (typeName.Contains('.'))
                    continue; // Skip full names, just use short names for scanning

                // Simple regex-based detection of type usage
                var pattern = $@"\b{Regex.Escape(typeName)}\b";
                var matches = Regex.Matches(content, pattern);

                if (matches.Count > 0)
                {
                    typeUsageCounts.TryGetValue(typeName, out var count);
                    typeUsageCounts[typeName] = count + matches.Count;

                    actions.Add(new MigrationAction
                    {
                        TypeName = typeName,
                        File = relativePath,
                        Occurrences = matches.Count,
                        Category = ClassifyAction(typeName, content, matches),
                        Suggestion = GetSuggestion(typeName)
                    });
                }
            }
        }

        // Deduplicate by file+type
        var deduplicated = actions
            .GroupBy(a => $"{a.File}|{a.TypeName}")
            .Select(g => g.First())
            .ToList();

        var safeCount = deduplicated.Count(a => a.Category == "safe");
        var assistedCount = deduplicated.Count(a => a.Category == "assisted");
        var manualCount = deduplicated.Count(a => a.Category == "manual");
        var distinctTypes = deduplicated.Select(a => a.TypeName).Distinct().Count();

        // Build migration plan
        var plan = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            projectDirectory = projectDir.FullName,
            manifest = manifestFile?.FullName,
            summary = new
            {
                typesToWrap = distinctTypes,
                safeAutoFixes = safeCount,
                assistedFixes = assistedCount,
                manualReview = manualCount,
                totalActions = deduplicated.Count,
                filesAnalyzed = csFiles.Count
            },
            actions = deduplicated.Select(a => new
            {
                typeName = a.TypeName,
                file = a.File,
                occurrences = a.Occurrences,
                category = a.Category,
                suggestion = a.Suggestion
            })
        };

        var planJson = JsonSerializer.Serialize(plan, SerializerOptions);
        var planPath = Path.IsPathRooted(outputPath)
            ? outputPath
            : Path.Combine(projectDir.FullName, outputPath);

        await File.WriteAllTextAsync(planPath, planJson);

        Console.WriteLine();
        Console.WriteLine("Migration Plan Summary");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine($"  Types to wrap:       {distinctTypes}");
        Console.WriteLine($"  Safe auto-fixes:     {safeCount}");
        Console.WriteLine($"  Assisted fixes:      {assistedCount}");
        Console.WriteLine($"  Manual review needed: {manualCount}");
        Console.WriteLine($"  Total actions:       {deduplicated.Count}");
        Console.WriteLine();
        Console.WriteLine($"Plan written to: {planPath}");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine("  1. Review migration-plan.json for categorized actions");
        Console.WriteLine("  2. Add WrapGod.Analyzers to your project for code fix suggestions");
        Console.WriteLine("  3. Run 'dotnet build' to see WG2001/WG2002 diagnostics");
    }

    private static string ClassifyAction(string typeName, string content, MatchCollection matches)
    {
        // Heuristic classification:
        // "safe" -- simple variable declarations, parameter types, field types
        // "assisted" -- used in generic constraints, inheritance, complex expressions
        // "manual" -- reflection, dynamic, typeof, or deeply nested usage

        if (content.Contains($"typeof({typeName})") || content.Contains("reflection", StringComparison.OrdinalIgnoreCase))
            return "manual";

        if (content.Contains($": {typeName}") || content.Contains($"where") && content.Contains(typeName))
            return "assisted";

        return "safe";
    }

    private static string GetSuggestion(string typeName)
    {
        return $"Replace direct usage of {typeName} with IWrapped{typeName} interface";
    }

    private sealed class MigrationAction
    {
        public string TypeName { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public int Occurrences { get; set; }
        public string Category { get; set; } = "safe";
        public string Suggestion { get; set; } = string.Empty;
    }
}
