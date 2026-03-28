using System.CommandLine;
using System.Diagnostics;
using System.Text.Json;
using WrapGod.Manifest;

namespace WrapGod.Cli;

internal static class DoctorCommand
{
    public static Command Create()
    {
        var projectDirOption = new Option<DirectoryInfo>(
            ["--project-dir", "-p"],
            () => new DirectoryInfo(Directory.GetCurrentDirectory()),
            "Project directory to check (defaults to current directory)");

        var command = new Command("doctor", "Validate environment setup and project health")
        {
            projectDirOption
        };

        command.SetHandler(Handle, projectDirOption);
        return command;
    }

    private static void Handle(DirectoryInfo projectDir)
    {
        Console.WriteLine("WrapGod Doctor");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine();

        var passed = 0;
        var failed = 0;

        // 1. Check .NET SDK version
        CheckDotNetSdk(ref passed, ref failed);

        // 2. Check config file exists
        CheckConfigFile(projectDir, ref passed, ref failed);

        // 3. Check manifest file validity
        CheckManifestFile(projectDir, ref passed, ref failed);

        // 4. Check generator reference
        CheckGeneratorReference(projectDir, ref passed, ref failed);

        // 5. Check cache directory
        CheckCacheDirectory(projectDir, ref passed, ref failed);

        Console.WriteLine();
        Console.WriteLine(new string('-', 40));
        Console.WriteLine($"Results: {passed} passed, {failed} failed");

        if (failed > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Fix the issues above and run 'wrap-god doctor' again.");
        }
        else
        {
            Console.WriteLine("All checks passed.");
        }
    }

    private static void CheckDotNetSdk(ref int passed, ref int failed)
    {
        try
        {
            var psi = new ProcessStartInfo("dotnet", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            var version = process?.StandardOutput.ReadToEnd().Trim() ?? "";
            process?.WaitForExit();

            if (!string.IsNullOrEmpty(version))
            {
                ReportPass($".NET SDK installed: {version}");
                passed++;
            }
            else
            {
                ReportFail(".NET SDK not detected");
                ReportFix("Install the .NET SDK from https://dot.net/download");
                failed++;
            }
        }
        catch
        {
            ReportFail(".NET SDK not detected");
            ReportFix("Install the .NET SDK from https://dot.net/download");
            failed++;
        }
    }

    private static void CheckConfigFile(DirectoryInfo projectDir, ref int passed, ref int failed)
    {
        var configPath = Path.Combine(projectDir.FullName, "wrapgod.config.json");
        if (File.Exists(configPath))
        {
            // Try to parse it
            try
            {
                var json = File.ReadAllText(configPath);
                JsonDocument.Parse(json);
                ReportPass($"Config file valid: wrapgod.config.json");
                passed++;
            }
            catch (JsonException ex)
            {
                ReportFail($"Config file invalid JSON: {ex.Message}");
                ReportFix("Fix the JSON syntax in wrapgod.config.json");
                failed++;
            }
        }
        else
        {
            ReportFail("Config file not found: wrapgod.config.json");
            ReportFix("Run 'wrap-god init' to create a config file");
            failed++;
        }
    }

    private static void CheckManifestFile(DirectoryInfo projectDir, ref int passed, ref int failed)
    {
        var manifestFiles = Directory.GetFiles(projectDir.FullName, "*.wrapgod.json");
        if (manifestFiles.Length == 0)
        {
            ReportFail("No manifest files found (*.wrapgod.json)");
            ReportFix("Run 'wrap-god extract <assembly>' to generate a manifest");
            failed++;
            return;
        }

        foreach (var manifestFile in manifestFiles)
        {
            try
            {
                var json = File.ReadAllText(manifestFile);
                var manifest = JsonSerializer.Deserialize<ApiManifest>(json);
                if (manifest is not null)
                {
                    ReportPass($"Manifest valid: {Path.GetFileName(manifestFile)} ({manifest.Types.Count} types)");
                    passed++;
                }
                else
                {
                    ReportFail($"Manifest deserialized to null: {Path.GetFileName(manifestFile)}");
                    ReportFix("Re-run extraction to regenerate the manifest");
                    failed++;
                }
            }
            catch (JsonException ex)
            {
                ReportFail($"Manifest invalid: {Path.GetFileName(manifestFile)} -- {ex.Message}");
                ReportFix("Re-run extraction to regenerate the manifest");
                failed++;
            }
        }
    }

    private static void CheckGeneratorReference(DirectoryInfo projectDir, ref int passed, ref int failed)
    {
        var csprojFiles = Directory.GetFiles(projectDir.FullName, "*.csproj");
        if (csprojFiles.Length == 0)
        {
            ReportFail("No .csproj file found in project directory");
            ReportFix("Run from a directory containing a .NET project");
            failed++;
            return;
        }

        var hasGenerator = false;
        foreach (var csproj in csprojFiles)
        {
            var content = File.ReadAllText(csproj);
            if (content.Contains("WrapGod.Generator", StringComparison.OrdinalIgnoreCase))
            {
                hasGenerator = true;
                ReportPass($"Generator reference found in {Path.GetFileName(csproj)}");
                passed++;
                break;
            }
        }

        if (!hasGenerator)
        {
            ReportFail("WrapGod.Generator not referenced in any project file");
            ReportFix("Add the generator: dotnet add package WrapGod.Generator");
            failed++;
        }
    }

    private static void CheckCacheDirectory(DirectoryInfo projectDir, ref int passed, ref int failed)
    {
        var cacheDir = Path.Combine(projectDir.FullName, ".wrapgod-cache");
        if (Directory.Exists(cacheDir))
        {
            ReportPass("Cache directory exists: .wrapgod-cache/");
            passed++;
        }
        else
        {
            ReportFail("Cache directory missing: .wrapgod-cache/");
            ReportFix("Run 'wrap-god init' or create .wrapgod-cache/ manually");
            failed++;
        }
    }

    private static void ReportPass(string message) =>
        Console.WriteLine($"  [PASS] {message}");

    private static void ReportFail(string message) =>
        Console.WriteLine($"  [FAIL] {message}");

    private static void ReportFix(string message) =>
        Console.WriteLine($"         Fix: {message}");
}
