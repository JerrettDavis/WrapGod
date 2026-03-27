using System.CommandLine;
using System.Text.Json;
using WrapGod.Extractor;

namespace WrapGod.Cli;

internal static class ExtractCommand
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static Command Create()
    {
        var assemblyPathArg = new Argument<FileInfo?>(
            "assembly-path",
            () => null,
            "Path to the .NET assembly to extract (optional when using --nuget)");

        var outputOption = new Option<FileInfo>(
            ["--output", "-o"],
            () => new FileInfo("manifest.json"),
            "Output path for the generated manifest");

        var nugetOption = new Option<string[]>(
            "--nuget",
            "NuGet package to extract in format <packageId>@<version>. Supports multiple flags for multi-version.")
        {
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.ZeroOrMore
        };

        var tfmOption = new Option<string?>(
            "--tfm",
            "Explicit target framework moniker override (e.g. net8.0, netstandard2.0)");

        var sourceOption = new Option<string?>(
            "--source",
            "Private NuGet feed URL (defaults to nuget.org)");

        var command = new Command("extract", "Extract API manifest from a .NET assembly or NuGet package")
        {
            assemblyPathArg,
            outputOption,
            nugetOption,
            tfmOption,
            sourceOption
        };

        command.SetHandler(HandleAsync, assemblyPathArg, outputOption, nugetOption, tfmOption, sourceOption);
        return command;
    }

    private static async Task HandleAsync(
        FileInfo? assemblyPath,
        FileInfo output,
        string[] nugetSpecs,
        string? tfm,
        string? source)
    {
        if (nugetSpecs is { Length: > 0 })
        {
            await HandleNuGetAsync(nugetSpecs, output, tfm, source);
            return;
        }

        if (assemblyPath is null)
        {
            Console.Error.WriteLine("Error: Either provide an assembly-path argument or use --nuget <packageId>@<version>.");
            return;
        }

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

    private static async Task HandleNuGetAsync(
        string[] nugetSpecs,
        FileInfo output,
        string? tfm,
        string? source)
    {
        var parsed = new List<(string PackageId, string Version)>();

        foreach (var spec in nugetSpecs)
        {
            var atIndex = spec.LastIndexOf('@');
            if (atIndex <= 0 || atIndex >= spec.Length - 1)
            {
                Console.Error.WriteLine($"Invalid --nuget format: '{spec}'. Expected <packageId>@<version>.");
                return;
            }

            parsed.Add((spec[..atIndex], spec[(atIndex + 1)..]));
        }

        var extractor = new NuGetExtractor();

        if (parsed.Count == 1)
        {
            var (packageId, version) = parsed[0];
            Console.WriteLine($"Resolving NuGet package {packageId}@{version}...");

            var manifest = await extractor.ExtractFromPackageAsync(packageId, version, tfm, source);

            var json = JsonSerializer.Serialize(manifest, SerializerOptions);
            await File.WriteAllTextAsync(output.FullName, json);

            Console.WriteLine($"Manifest written to {output.FullName}");
            Console.WriteLine($"  Types: {manifest.Types.Count}");
            Console.WriteLine($"  Members: {manifest.Types.Sum(t => t.Members.Count)}");
        }
        else
        {
            // Multi-version: group by package ID.
            var groups = parsed.GroupBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups)
            {
                var packageId = group.Key;
                var versions = group.Select(g => g.Version).ToList();

                Console.WriteLine($"Resolving NuGet package {packageId} versions: {string.Join(", ", versions)}...");

                var result = await extractor.ExtractMultiVersionAsync(packageId, versions, tfm, source);

                var json = JsonSerializer.Serialize(result.MergedManifest, SerializerOptions);
                await File.WriteAllTextAsync(output.FullName, json);

                Console.WriteLine($"Merged manifest written to {output.FullName}");
                Console.WriteLine($"  Types: {result.MergedManifest.Types.Count}");
                Console.WriteLine($"  Members: {result.MergedManifest.Types.Sum(t => t.Members.Count)}");
                Console.WriteLine($"  Breaking changes: {result.Diff.BreakingChanges.Count}");
            }
        }
    }
}
