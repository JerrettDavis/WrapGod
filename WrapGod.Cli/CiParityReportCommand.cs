using System.CommandLine;

namespace WrapGod.Cli;

internal static class CiParityReportCommand
{
    /// <summary>
    /// Required steps that the recommended WrapGod CI workflow should contain.
    /// Each entry is a key phrase expected to appear in the workflow YAML.
    /// </summary>
    private static readonly (string Id, string Description, string SearchPattern)[] BaselineSteps =
    [
        ("checkout", "Checkout repository", "actions/checkout"),
        ("setup-dotnet", "Setup .NET SDK", "actions/setup-dotnet"),
        ("restore", "Restore NuGet packages", "dotnet restore"),
        ("build", "Build solution", "dotnet build"),
        ("test", "Run tests", "dotnet test"),
        ("coverage", "Collect code coverage", "XPlat Code Coverage"),
        ("wg-extract", "WrapGod extract step", "wrap-god extract"),
        ("wg-generate", "WrapGod generate step", "wrap-god generate"),
        ("wg-analyze", "WrapGod analyze step", "wrap-god analyze"),
    ];

    public static Command Create()
    {
        var workflowDirOption = new Option<DirectoryInfo>(
            ["--workflow-dir", "-w"],
            () => new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), ".github", "workflows")),
            "Directory containing CI workflow files");

        var command = new Command("parity", "Compare current CI config against the recommended WrapGod baseline")
        {
            workflowDirOption
        };

        command.SetHandler((DirectoryInfo workflowDir) =>
        {
            Environment.ExitCode = Handle(workflowDir);
        }, workflowDirOption);

        return command;
    }

    private static int Handle(DirectoryInfo workflowDir)
    {
        Console.WriteLine("WrapGod CI Parity Report");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine();

        if (!workflowDir.Exists)
        {
            Console.Error.WriteLine($"Workflow directory not found: {workflowDir.FullName}");
            Console.Error.WriteLine("Run 'wrap-god ci bootstrap' to generate workflow files.");
            return 1;
        }

        var yamlFiles = workflowDir.GetFiles("*.yml")
            .Concat(workflowDir.GetFiles("*.yaml"))
            .ToArray();

        if (yamlFiles.Length == 0)
        {
            Console.Error.WriteLine("No workflow files (*.yml, *.yaml) found.");
            Console.Error.WriteLine("Run 'wrap-god ci bootstrap' to generate workflow files.");
            return 1;
        }

        Console.WriteLine($"Scanning {yamlFiles.Length} workflow file(s) in {workflowDir.FullName}");
        Console.WriteLine();

        // Read all workflow content
        var allContent = string.Join(
            Environment.NewLine,
            yamlFiles.Select(f => File.ReadAllText(f.FullName)));

        var missing = new List<(string Id, string Description)>();
        var found = new List<(string Id, string Description)>();

        foreach (var (id, description, pattern) in BaselineSteps)
        {
            if (allContent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                found.Add((id, description));
            }
            else
            {
                missing.Add((id, description));
            }
        }

        // Report found steps
        Console.WriteLine("Baseline steps found:");
        if (found.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var (id, description) in found)
            {
                Console.WriteLine($"  [PASS] {description} ({id})");
            }
        }

        Console.WriteLine();

        // Report missing steps
        Console.WriteLine("Missing or outdated steps:");
        if (missing.Count == 0)
        {
            Console.WriteLine("  (none -- full parity!)");
        }
        else
        {
            foreach (var (id, description) in missing)
            {
                Console.WriteLine($"  [MISS] {description} ({id})");
            }
        }

        Console.WriteLine();

        // Summary
        var total = BaselineSteps.Length;
        var pct = found.Count * 100 / total;
        Console.WriteLine($"Parity: {found.Count}/{total} steps ({pct}%)");

        if (missing.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Run 'wrap-god ci bootstrap --force' to regenerate the recommended workflow.");
            return 2;
        }

        return 0;
    }
}
