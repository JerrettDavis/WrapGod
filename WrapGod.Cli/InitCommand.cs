using System.CommandLine;
using System.Text.Json;

namespace WrapGod.Cli;

internal static class InitCommand
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static Command Create()
    {
        var sourceOption = new Option<string?>(
            ["--source", "-s"],
            "Assembly source: local file path, NuGet package (<id>@<version>), or @self");

        var outputOption = new Option<string>(
            ["--output", "-o"],
            () => "wrapgod.config.json",
            "Output config file name");

        var command = new Command("init", "Bootstrap a new WrapGod project in the current directory")
        {
            sourceOption,
            outputOption
        };

        command.SetHandler(HandleAsync, sourceOption, outputOption);
        return command;
    }

    private static async Task HandleAsync(string? source, string outputFileName)
    {
        Console.WriteLine("WrapGod Init");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine();

        // Prompt for source if not provided
        if (string.IsNullOrWhiteSpace(source))
        {
            Console.WriteLine("Select assembly source:");
            Console.WriteLine("  1) Local assembly path");
            Console.WriteLine("  2) NuGet package (e.g. Newtonsoft.Json@13.0.3)");
            Console.WriteLine("  3) @self (wrap in-project types)");
            Console.Write("Choice [1/2/3]: ");

            var choice = Console.ReadLine()?.Trim();
            source = choice switch
            {
                "1" => PromptForInput("Assembly path: "),
                "2" => PromptForInput("NuGet package (<id>@<version>): "),
                "3" => "@self",
                _ => "@self"
            };
        }

        var configPath = Path.GetFullPath(outputFileName);

        if (File.Exists(configPath))
        {
            Console.Error.WriteLine($"Config file already exists: {configPath}");
            Console.Error.WriteLine("Remove it or use a different --output name.");
            return;
        }

        // Build minimal config template
        var config = new
        {
            source,
            types = new[]
            {
                new
                {
                    sourceType = "ExampleNamespace.ExampleType",
                    include = true,
                    targetName = (string?)null,
                    members = Array.Empty<object>()
                }
            }
        };

        var json = JsonSerializer.Serialize(config, SerializerOptions);
        await File.WriteAllTextAsync(configPath, json);
        Console.WriteLine($"Created config: {configPath}");

        // Create cache directory
        var cacheDir = Path.Combine(Path.GetDirectoryName(configPath)!, ".wrapgod-cache");
        Directory.CreateDirectory(cacheDir);
        Console.WriteLine($"Created cache directory: {cacheDir}");

        // Write .gitignore for cache
        var gitignorePath = Path.Combine(cacheDir, ".gitignore");
        if (!File.Exists(gitignorePath))
        {
            await File.WriteAllTextAsync(gitignorePath, "*\n");
        }

        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine("  1. Edit wrapgod.config.json to configure your types");
        Console.WriteLine("  2. Run: wrap-god extract <assembly> -o manifest.wrapgod.json");
        Console.WriteLine("  3. Add WrapGod.Generator to your project:");
        Console.WriteLine("     dotnet add package WrapGod.Generator");
        Console.WriteLine("  4. Build to generate wrappers: dotnet build");
    }

    private static string PromptForInput(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }
}
