using System.CommandLine;
using System.Text;

namespace WrapGod.Cli;

internal static class InitCommand
{
    public static Command Create()
    {
        var targetDirectoryArg = new Argument<DirectoryInfo>(
            "target-directory",
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Directory to initialize with WrapGod baseline files.");

        var dryRunOption = new Option<bool>("--dry-run", "Print planned changes without writing files.");
        var includeSamplesOption = new Option<bool>("--include-samples", "Include sample type mapping seeds.");
        var includeCiOption = new Option<bool>("--include-ci", "Include a starter GitHub Actions workflow.");

        var command = new Command("init", "Bootstrap zero-config WrapGod baseline files")
        {
            targetDirectoryArg,
            dryRunOption,
            includeSamplesOption,
            includeCiOption,
        };

        command.SetHandler(
            (DirectoryInfo targetDirectory, bool dryRun, bool includeSamples, bool includeCi) =>
            {
                var options = new InitScaffoldOptions(dryRun, includeSamples, includeCi);
                var result = InitScaffolder.Scaffold(targetDirectory.FullName, options);
                Console.WriteLine(result.ToConsoleOutput());
            },
            targetDirectoryArg,
            dryRunOption,
            includeSamplesOption,
            includeCiOption);

        return command;
    }
}

internal sealed record InitScaffoldOptions(bool DryRun, bool IncludeSamples, bool IncludeCi);

internal sealed record InitScaffoldResult(
    string TargetDirectory,
    int CreatedCount,
    int SkippedCount,
    IReadOnlyList<string> CreatedFiles,
    IReadOnlyList<string> SkippedFiles,
    bool DryRun)
{
    public string ToConsoleOutput()
    {
        var sb = new StringBuilder();
        sb.AppendLine("WrapGod init");
        sb.AppendLine(new string('-', 40));
        sb.AppendLine($"Target: {TargetDirectory}");
        sb.AppendLine($"Mode:   {(DryRun ? "dry-run" : "write")}");
        sb.AppendLine();

        if (CreatedFiles.Count > 0)
        {
            sb.AppendLine(DryRun ? "Would create:" : "Created:");
            foreach (var file in CreatedFiles)
            {
                sb.AppendLine($"  + {file}");
            }

            sb.AppendLine();
        }

        if (SkippedFiles.Count > 0)
        {
            sb.AppendLine("Skipped (already exists):");
            foreach (var file in SkippedFiles)
            {
                sb.AppendLine($"  = {file}");
            }

            sb.AppendLine();
        }

        sb.AppendLine($"Summary: {CreatedCount} {(DryRun ? "planned" : "created")}, {SkippedCount} skipped.");
        return sb.ToString();
    }
}

internal static class InitScaffolder
{
    private sealed record ScaffoldFile(string RelativePath, string Content);

    public static InitScaffoldResult Scaffold(string targetDirectory, InitScaffoldOptions options)
    {
        Directory.CreateDirectory(targetDirectory);

        var createdFiles = new List<string>();
        var skippedFiles = new List<string>();

        foreach (var file in BuildScaffoldPlan(options))
        {
            var fullPath = Path.Combine(targetDirectory, file.RelativePath);
            var relativePath = Normalize(file.RelativePath);

            if (File.Exists(fullPath))
            {
                skippedFiles.Add(relativePath);
                continue;
            }

            createdFiles.Add(relativePath);

            if (options.DryRun)
            {
                continue;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, file.Content.Replace("`n", Environment.NewLine), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        return new InitScaffoldResult(
            targetDirectory,
            createdFiles.Count,
            skippedFiles.Count,
            createdFiles,
            skippedFiles,
            options.DryRun);
    }

    private static IReadOnlyList<ScaffoldFile> BuildScaffoldPlan(InitScaffoldOptions options)
    {
        var files = new List<ScaffoldFile>
        {
            new("wrapgod.root.json", """
{
  "$schema": "https://raw.githubusercontent.com/JerrettDavis/WrapGod/main/schemas/wrapgod.root.schema.json",
  "version": 1,
  "projectConfig": "wrapgod.project.json",
  "manifest": "manifest.wrapgod.json",
  "typeMappings": "wrapgod-types.txt"
}
"""),
            new("wrapgod.project.json", """
{
  "$schema": "https://raw.githubusercontent.com/JerrettDavis/WrapGod/main/schemas/wrapgod.project.schema.json",
  "compatibilityMode": "lcd",
  "types": []
}
"""),
            new("wrapgod-types.txt", """
# Format:
# Source.Type.FullName -> IWrappedType, WrappedTypeFacade
"""),
            new("docs/wrapgod-init.md", """
# WrapGod Bootstrap

Generated by `wrap-god init`.

## Suggested next steps

1. Extract a manifest:

   ```bash
   wrap-god extract path/to/Vendor.dll --output manifest.wrapgod.json
   ```

2. Update `wrapgod.project.json` with the wrapped types you want to generate.
3. Add `manifest.wrapgod.json` and `wrapgod-types.txt` as `AdditionalFiles` in your project.
4. Run analyzers to detect direct usage:

   ```bash
   wrap-god analyze manifest.wrapgod.json
   ```
"""),
        };

        if (options.IncludeSamples)
        {
            files.Add(new ScaffoldFile("wrapgod-types.sample.txt", """
# Sample seed mappings
Vendor.Lib.HttpClient -> IHttpClient, HttpClientFacade
Vendor.Lib.Logger -> ILogger, LoggerFacade
"""));
        }

        if (options.IncludeCi)
        {
            files.Add(new ScaffoldFile(".github/workflows/wrapgod-starter.yml", """
name: wrapgod-starter

on:
  pull_request:
  push:
    branches: [ main ]

jobs:
  wrapgod:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --configuration Release --no-restore
      - name: Analyze manifest gate
        run: dotnet run --project WrapGod.Cli -- analyze manifest.wrapgod.json --warnings-as-errors
"""));
        }

        return files;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
