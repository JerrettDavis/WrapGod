using System.CommandLine;

namespace WrapGod.Cli;

internal static class CiBootstrapCommand
{
    public static Command Create()
    {
        var outputDirOption = new Option<DirectoryInfo>(
            ["--output", "-o"],
            () => new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), ".github", "workflows")),
            "Output directory for generated workflow files");

        var forceOption = new Option<bool>(
            "--force",
            "Overwrite existing workflow files");

        var command = new Command("bootstrap", "Generate recommended CI workflow files for a WrapGod project")
        {
            outputDirOption,
            forceOption
        };

        command.SetHandler((DirectoryInfo outputDir, bool force) =>
        {
            Environment.ExitCode = Handle(outputDir, force);
        }, outputDirOption, forceOption);

        return command;
    }

    private static int Handle(DirectoryInfo outputDir, bool force)
    {
        Console.WriteLine("WrapGod CI Bootstrap");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine();

        if (!outputDir.Exists)
        {
            outputDir.Create();
            Console.WriteLine($"Created directory: {outputDir.FullName}");
        }

        var workflowPath = Path.Combine(outputDir.FullName, "wrapgod-ci.yml");

        if (File.Exists(workflowPath) && !force)
        {
            Console.Error.WriteLine($"Workflow file already exists: {workflowPath}");
            Console.Error.WriteLine("Use --force to overwrite.");
            return 1;
        }

        var yaml = GenerateWorkflowYaml();
        File.WriteAllText(workflowPath, yaml);

        Console.WriteLine($"Generated: {workflowPath}");
        Console.WriteLine();
        Console.WriteLine("The workflow includes:");
        Console.WriteLine("  - Build and test with coverage");
        Console.WriteLine("  - WrapGod extract step");
        Console.WriteLine("  - WrapGod generate step");
        Console.WriteLine("  - WrapGod analyze step (diagnostic gate)");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine("  1. Review the generated workflow file");
        Console.WriteLine("  2. Commit and push to enable CI");
        Console.WriteLine("  3. Run 'wrap-god ci parity' to verify alignment");

        return 0;
    }

    internal static string GenerateWorkflowYaml()
    {
        return """
               name: WrapGod CI

               on:
                 push:
                   branches: [ main ]
                 pull_request:
                   branches: [ main ]

               jobs:
                 build-test:
                   runs-on: ubuntu-latest

                   steps:
                     - name: Checkout
                       uses: actions/checkout@v4

                     - name: Setup .NET
                       uses: actions/setup-dotnet@v4
                       with:
                         dotnet-version: '10.0.x'

                     - name: Restore
                       run: dotnet restore

                     - name: Build
                       run: dotnet build --configuration Release --no-restore

                     - name: Test with Coverage
                       run: |
                         dotnet test --configuration Release --no-build \
                           --collect:"XPlat Code Coverage" \
                           --results-directory TestResults

                     - name: WrapGod Extract
                       run: dotnet wrap-god extract --output manifest.wrapgod.json

                     - name: WrapGod Generate
                       run: dotnet wrap-god generate manifest.wrapgod.json

                     - name: WrapGod Analyze
                       run: dotnet wrap-god analyze manifest.wrapgod.json --warnings-as-errors

                     - name: Upload Coverage
                       uses: actions/upload-artifact@v4
                       if: always()
                       with:
                         name: coverage-report
                         path: TestResults/**/coverage.cobertura.xml
                         retention-days: 14
               """;
    }
}
