using System.CommandLine;

namespace WrapGod.Cli;

internal static class GenerateCommand
{
    public static Command Create()
    {
        var manifestPathArg = new Argument<FileInfo>(
            "manifest-path",
            "Path to the WrapGod manifest JSON file");

        var configOption = new Option<FileInfo?>(
            ["--config", "-c"],
            "Path to the WrapGod config JSON file");

        var outputDirOption = new Option<DirectoryInfo>(
            ["--output-dir", "-o"],
            () => new DirectoryInfo("./generated"),
            "Output directory for generated source files");

        var command = new Command("generate", "Generate wrapper source from a manifest (compile-time)")
        {
            manifestPathArg,
            configOption,
            outputDirOption
        };

        command.SetHandler(Handle, manifestPathArg, configOption, outputDirOption);
        return command;
    }

    private static void Handle(FileInfo manifestPath, FileInfo? config, DirectoryInfo outputDir)
    {
        if (!manifestPath.Exists)
        {
            Console.Error.WriteLine($"Manifest not found: {manifestPath.FullName}");
            return;
        }

        Console.WriteLine("WrapGod Generator");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine();
        Console.WriteLine("The WrapGod generator is a Roslyn incremental source generator that runs");
        Console.WriteLine("at compile time. It reads *.wrapgod.json manifests from your project and");
        Console.WriteLine("emits IWrapped{Type} interfaces and {Type}Facade classes automatically.");
        Console.WriteLine();
        Console.WriteLine("To use generation:");
        Console.WriteLine();
        Console.WriteLine("  1. Add the WrapGod.Generator package to your project:");
        Console.WriteLine("     dotnet add package WrapGod.Generator");
        Console.WriteLine();
        Console.WriteLine("  2. Place your manifest as an AdditionalFile in the project:");
        Console.WriteLine("     <AdditionalFiles Include=\"manifest.wrapgod.json\" />");
        Console.WriteLine();
        Console.WriteLine("  3. Build your project -- wrappers are generated automatically:");
        Console.WriteLine("     dotnet build");
        Console.WriteLine();
        Console.WriteLine($"Manifest:   {manifestPath.FullName}");
        if (config is not null)
            Console.WriteLine($"Config:     {config.FullName}");
        Console.WriteLine($"Output dir: {outputDir.FullName} (not used -- generation is compile-time)");
    }
}
