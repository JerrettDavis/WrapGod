using System.CommandLine;
using System.Text.Json;
using WrapGod.Extractor;

namespace WrapGod.Cli;

internal static class ExtractCommand
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static Command Create()
    {
        var assemblyPathArg = new Argument<FileInfo>(
            "assembly-path",
            "Path to the .NET assembly to extract");

        var outputOption = new Option<FileInfo>(
            ["--output", "-o"],
            () => new FileInfo("manifest.json"),
            "Output path for the generated manifest");

        var command = new Command("extract", "Extract API manifest from a .NET assembly")
        {
            assemblyPathArg,
            outputOption
        };

        command.SetHandler(HandleAsync, assemblyPathArg, outputOption);
        return command;
    }

    private static async Task HandleAsync(FileInfo assemblyPath, FileInfo output)
    {
        if (!assemblyPath.Exists)
        {
            Console.Error.WriteLine($"Assembly not found: {assemblyPath.FullName}");
            return;
        }

        Console.WriteLine($"Extracting manifest from {assemblyPath.Name}...");

        var manifest = AssemblyExtractor.Extract(assemblyPath.FullName);

        var json = JsonSerializer.Serialize(manifest, SerializerOptions);

        await File.WriteAllTextAsync(output.FullName, json);
        Console.WriteLine($"Manifest written to {output.FullName}");
        Console.WriteLine($"  Types: {manifest.Types.Count}");
        Console.WriteLine($"  Members: {manifest.Types.Sum(t => t.Members.Count)}");
    }
}
