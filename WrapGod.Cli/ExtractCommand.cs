using System.CommandLine;
using System.Text.Json;
using WrapGod.Extractor;
using WrapGod.Manifest;

namespace WrapGod.Cli;

internal static class ExtractCommand
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public static Command Create()
    {
        var assemblyPathArg = new Argument<FileInfo?>("assembly-path", () => null, "Path to the .NET assembly to extract (optional when using --nuget or --locked)");
        var outputOption = new Option<FileInfo>(["--output", "-o"], () => new FileInfo("manifest.json"), "Output path for the generated manifest");
        var nugetOption = new Option<string[]>("--nuget", "NuGet package to extract in format <packageId>@<version>. Supports multiple flags for multi-version.")
        {
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.ZeroOrMore
        };
        var tfmOption = new Option<string?>("--tfm", "Explicit target framework moniker override (e.g. net8.0, netstandard2.0)");
        var sourceOption = new Option<string?>("--source", "Private NuGet feed URL (defaults to nuget.org)");
        var lockFileOption = new Option<FileInfo>("--lockfile", () => new FileInfo("wrapgod.lock.json"), "Path to WrapGod lockfile for write/read deterministic resolution data");
        var lockedOption = new Option<bool>("--locked", "Require lockfile-only resolution. Fails when lockfile is missing or drift is detected.");

        var command = new Command("extract", "Extract API manifest from a .NET assembly or NuGet package")
        {
            assemblyPathArg, outputOption, nugetOption, tfmOption, sourceOption, lockFileOption, lockedOption
        };

        command.SetHandler(HandleAsync, assemblyPathArg, outputOption, nugetOption, tfmOption, sourceOption, lockFileOption, lockedOption);
        return command;
    }

    private static async Task HandleAsync(FileInfo? assemblyPath, FileInfo output, string[] nugetSpecs, string? tfm, string? source, FileInfo lockFile, bool locked)
    {
        if (locked)
        {
            await HandleLockedAsync(lockFile, output);
            return;
        }

        if (nugetSpecs is { Length: > 0 })
        {
            await HandleNuGetAsync(nugetSpecs, output, tfm, source, lockFile);
            return;
        }

        if (assemblyPath is null)
        {
            Console.Error.WriteLine("Error: Either provide assembly-path, use --nuget, or run --locked with --lockfile.");
            return;
        }

        if (!assemblyPath.Exists)
        {
            Console.Error.WriteLine($"Assembly not found: {assemblyPath.FullName}");
            return;
        }

        var manifest = AssemblyExtractor.Extract(assemblyPath.FullName);
        await File.WriteAllTextAsync(output.FullName, JsonSerializer.Serialize(manifest, SerializerOptions));
        Console.WriteLine($"Manifest written to {output.FullName}");
    }

    private static async Task HandleNuGetAsync(string[] nugetSpecs, FileInfo output, string? tfm, string? source, FileInfo lockFile)
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
            var result = await extractor.ExtractFromPackageWithLockAsync(packageId, version, tfm, source);

            await File.WriteAllTextAsync(output.FullName, JsonSerializer.Serialize(result.Manifest, SerializerOptions));
            await File.WriteAllTextAsync(lockFile.FullName, LockFileSerializer.Serialize(new WrapGodLockFile
            {
                Toolchain = new LockToolchain
                {
                    Version = typeof(ExtractCommand).Assembly.GetName().Version?.ToString(),
                    Runtime = Environment.Version.ToString()
                },
                Sources =
                [
                    new LockSourceEntry
                    {
                        PackageId = result.Resolution.PackageId,
                        Version = result.Resolution.Version,
                        SourceFeed = result.Resolution.SourceFeed,
                        PackageSha256 = result.Resolution.PackageSha256,
                        TargetFramework = result.Resolution.TargetFramework,
                        DllRelativePath = result.Resolution.DllRelativePath,
                        DllSha256 = result.Resolution.DllSha256
                    }
                ]
            }));

            Console.WriteLine($"Manifest written to {output.FullName}");
            Console.WriteLine($"Lockfile written to {lockFile.FullName}");
            return;
        }

        var groups = parsed.GroupBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var result = await extractor.ExtractMultiVersionAsync(group.Key, group.Select(g => g.Version).ToList(), tfm, source);
            await File.WriteAllTextAsync(output.FullName, JsonSerializer.Serialize(result.MergedManifest, SerializerOptions));
        }
    }

    private static async Task HandleLockedAsync(FileInfo lockFilePath, FileInfo output)
    {
        if (!lockFilePath.Exists)
        {
            Console.Error.WriteLine($"Lockfile not found: {lockFilePath.FullName}");
            return;
        }

        var lockFile = LockFileSerializer.Deserialize(await File.ReadAllTextAsync(lockFilePath.FullName));
        if (lockFile is null || lockFile.Sources.Count == 0)
        {
            Console.Error.WriteLine("Invalid lockfile: no sources found.");
            return;
        }

        if (lockFile.Sources.Count > 1)
        {
            Console.Error.WriteLine("Locked extraction currently supports one source entry per run.");
            return;
        }

        try
        {
            var manifest = await new NuGetExtractor().ExtractFromLockAsync(lockFile.Sources[0]);
            await File.WriteAllTextAsync(output.FullName, JsonSerializer.Serialize(manifest, SerializerOptions));
            Console.WriteLine($"Manifest written to {output.FullName}");
            Console.WriteLine("Lockfile verification passed (no drift detected).");
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
        }
    }
}
