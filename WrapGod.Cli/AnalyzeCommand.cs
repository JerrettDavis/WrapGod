using System.CommandLine;
using System.Text.Json;
using WrapGod.Manifest;

namespace WrapGod.Cli;

internal static class AnalyzeCommand
{
    public static Command Create()
    {
        var manifestPathArg = new Argument<FileInfo>(
            "manifest-path",
            "Path to the WrapGod manifest JSON file");

        var configOption = new Option<FileInfo?>(
            ["--config", "-c"],
            "Path to the WrapGod config JSON file");

        var command = new Command("analyze", "Analyze a manifest and report diagnostic information")
        {
            manifestPathArg,
            configOption
        };

        command.SetHandler(HandleAsync, manifestPathArg, configOption);
        return command;
    }

    private static async Task HandleAsync(FileInfo manifestPath, FileInfo? config)
    {
        if (!manifestPath.Exists)
        {
            Console.Error.WriteLine($"Manifest not found: {manifestPath.FullName}");
            return;
        }

        Console.WriteLine("WrapGod Analyzer");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine();

        var json = await File.ReadAllTextAsync(manifestPath.FullName);
        var manifest = JsonSerializer.Deserialize<ApiManifest>(json);

        if (manifest is null)
        {
            Console.Error.WriteLine("Failed to deserialize manifest.");
            return;
        }

        Console.WriteLine($"Assembly:    {manifest.Assembly.Name}");
        Console.WriteLine($"Version:     {manifest.Assembly.Version}");
        Console.WriteLine($"Types:       {manifest.Types.Count}");

        var totalMembers = manifest.Types.Sum(t => t.Members.Count);
        Console.WriteLine($"Members:     {totalMembers}");
        Console.WriteLine();

        Console.WriteLine("Type breakdown:");
        foreach (var type in manifest.Types)
        {
            Console.WriteLine($"  {type.FullName}");
            Console.WriteLine($"    Kind: {type.Kind}, Members: {type.Members.Count}");
            foreach (var member in type.Members)
            {
                Console.WriteLine($"      - {member.Name} ({member.Kind})");
            }
        }

        if (config is not null)
        {
            Console.WriteLine();
            if (config.Exists)
                Console.WriteLine($"Config loaded: {config.FullName}");
            else
                Console.Error.WriteLine($"Config not found: {config.FullName}");
        }

        Console.WriteLine();
        Console.WriteLine("For full analysis with Roslyn diagnostics (WG2001, WG2002), add");
        Console.WriteLine("WrapGod.Analyzers to your project and build:");
        Console.WriteLine("  dotnet add package WrapGod.Analyzers");
        Console.WriteLine("  dotnet build");
    }
}
