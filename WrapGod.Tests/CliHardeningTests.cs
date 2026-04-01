using System.CommandLine;
using System.Text.Json;
using WrapGod.Cli;

namespace WrapGod.Tests;

[Collection("CLI")]
public sealed class CliHardeningTests
{
    [Fact]
    public async Task Doctor_ProjectDirectoryMissing_ReturnsExitCode1()
    {
        var command = DoctorCommand.Create();
        var missingPath = Path.Combine(Path.GetTempPath(), $"wrapgod-missing-{Guid.NewGuid():N}");

        var result = await InvokeAsync(command, $"--project-dir \"{missingPath}\"");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Project directory not found", result.StdOut);
    }

    [Fact]
    public async Task Doctor_ProjectPathIsFile_ReturnsExitCode1()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var filePath = Path.Combine(tempRoot, "project.txt");
            await File.WriteAllTextAsync(filePath, "x");

            var result = await InvokeAsync(DoctorCommand.Create(), $"--project-dir \"{filePath}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("points to a file", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task MigrateInit_ProjectDirectoryMissing_ReturnsExitCode1()
    {
        var command = MigrateInitCommand.Create();
        var missingPath = Path.Combine(Path.GetTempPath(), $"wrapgod-missing-{Guid.NewGuid():N}");

        var result = await InvokeAsync(command, $"init --project-dir \"{missingPath}\"");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Project directory not found", result.StdErr);
    }

    [Fact]
    public async Task MigrateInit_OutputPathIsDirectory_ReturnsExitCode1()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var outputDirectory = Path.Combine(tempRoot, "existing-dir");
            Directory.CreateDirectory(outputDirectory);

            var result = await InvokeAsync(MigrateInitCommand.Create(), $"init --project-dir \"{tempRoot}\" --output \"{outputDirectory}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Output path points to a directory", result.StdErr);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task MigrateInit_AlreadyInitializedOutputExists_ReturnsExitCode1()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var outputPath = Path.Combine(tempRoot, "migration-plan.json");
            await File.WriteAllTextAsync(outputPath, "{}");

            var result = await InvokeAsync(MigrateInitCommand.Create(), $"init --project-dir \"{tempRoot}\" --output \"{outputPath}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Migration plan already exists", result.StdErr);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task MigrateInit_InvalidManifestJson_ReturnsExitCode1()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, "{ invalid json }");

            var result = await InvokeAsync(MigrateInitCommand.Create(), $"init --project-dir \"{tempRoot}\" --manifest \"{manifestPath}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Failed to read manifest", result.StdErr);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Theory]
    [InlineData("", "root-help.txt")]
    [InlineData("doctor", "doctor-help.txt")]
    [InlineData("migrate", "migrate-help.txt")]
    [InlineData("migrate init", "migrate-init-help.txt")]
    [InlineData("ci", "ci-help.txt")]
    [InlineData("analyze", "analyze-help.txt")]
    public async Task Cli_HelpContracts_MatchSnapshots(string commandPath, string snapshotFile)
    {
        var root = CreateRootCommand();
        var command = ResolveCommand(root, commandPath);

        var expected = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Snapshots", "cli-help", snapshotFile));
        var actual = RenderHelpContract(command, commandPath);

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    private static RootCommand CreateRootCommand()
    {
        var ci = new Command("ci", "CI/CD workflow tools")
        {
            CiBootstrapCommand.Create(),
            CiParityReportCommand.Create(),
        };

        return new RootCommand("WrapGod CLI -- extract manifests, generate wrappers, and analyze migrations")
        {
            InitCommand.Create(),
            ExtractCommand.Create(),
            GenerateCommand.Create(),
            AnalyzeCommand.Create(),
            DoctorCommand.Create(),
            ExplainCommand.Create(),
            MigrateInitCommand.Create(),
            ci,
        };
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> InvokeAsync(Command command, string args)
    {
        var previousOut = Console.Out;
        var previousErr = Console.Error;
        var previousExitCode = Environment.ExitCode;
        using var stdOut = new StringWriter();
        using var stdErr = new StringWriter();

        try
        {
            Console.SetOut(stdOut);
            Console.SetError(stdErr);
            Environment.ExitCode = 0;

            var invokeCode = await command.InvokeAsync(args);
            var effectiveExitCode = Environment.ExitCode == 0 ? invokeCode : Environment.ExitCode;
            return (effectiveExitCode, stdOut.ToString(), stdErr.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousErr);
            Environment.ExitCode = previousExitCode;
        }
    }

    private static string Normalize(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }

    private static Command ResolveCommand(RootCommand root, string commandPath)
    {
        Command current = root;
        foreach (var segment in commandPath.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current.Subcommands.Single(c => c.Name == segment);
        }

        return current;
    }

    private static string RenderHelpContract(Command command, string commandPath)
    {
        var displayPath = string.IsNullOrWhiteSpace(commandPath) ? "<root>" : commandPath;
        var lines = new List<string>
        {
            $"command: {displayPath}",
            $"description: {command.Description}",
            $"subcommands: {string.Join(",", command.Subcommands.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal))}",
            "options:"
        };

        foreach (var option in command.Options.OrderBy(o => o.Name, StringComparer.Ordinal))
        {
            var aliases = string.Join(",", option.Aliases.OrderBy(a => a, StringComparer.Ordinal));
            lines.Add($"  - {option.Name}|aliases={aliases}|arity={option.Arity.MinimumNumberOfValues}..{option.Arity.MaximumNumberOfValues}|description={NormalizeDefaultValue(option.Description ?? string.Empty)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeDefaultValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Replace(Directory.GetCurrentDirectory(), "<cwd>", StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────────────────────
    // Doctor happy-path and branch coverage
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Doctor_ValidProject_ReturnsExitCode0()
    {
        var tempRoot = CreateTempDir();
        try
        {
            // Set up a valid project environment
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "wrapgod.config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "vendor.wrapgod.json"),
                """{"schemaVersion":"1.0","generatedAt":"2026-01-01T00:00:00Z","assembly":{"name":"Acme","version":"1.0.0"},"types":[]}""");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "MyApp.csproj"),
                """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="WrapGod.Generator" Version="1.0.0" /></ItemGroup></Project>""");
            Directory.CreateDirectory(Path.Combine(tempRoot, ".wrapgod-cache"));

            var result = await InvokeAsync(DoctorCommand.Create(), $"--project-dir \"{tempRoot}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("All checks passed", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Doctor_MissingConfig_ReportsFailure()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var result = await InvokeAsync(DoctorCommand.Create(), $"--project-dir \"{tempRoot}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Config file not found", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Doctor_InvalidConfigJson_ReportsFailure()
    {
        var tempRoot = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "wrapgod.config.json"), "not valid json{{{");

            var result = await InvokeAsync(DoctorCommand.Create(), $"--project-dir \"{tempRoot}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Config file invalid JSON", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Doctor_NoManifestFiles_ReportsFailure()
    {
        var tempRoot = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "wrapgod.config.json"), "{}");

            var result = await InvokeAsync(DoctorCommand.Create(), $"--project-dir \"{tempRoot}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("No manifest files found", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Doctor_InvalidManifestJson_ReportsFailure()
    {
        var tempRoot = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "wrapgod.config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "bad.wrapgod.json"), "not json");

            var result = await InvokeAsync(DoctorCommand.Create(), $"--project-dir \"{tempRoot}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Manifest invalid", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Doctor_NoCsprojFiles_ReportsFailure()
    {
        var tempRoot = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "wrapgod.config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "vendor.wrapgod.json"),
                """{"schemaVersion":"1.0","generatedAt":"2026-01-01T00:00:00Z","assembly":{"name":"Acme","version":"1.0.0"},"types":[]}""");

            var result = await InvokeAsync(DoctorCommand.Create(), $"--project-dir \"{tempRoot}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("No .csproj file found", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Doctor_CsprojWithoutGeneratorRef_ReportsFailure()
    {
        var tempRoot = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "wrapgod.config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "vendor.wrapgod.json"),
                """{"schemaVersion":"1.0","generatedAt":"2026-01-01T00:00:00Z","assembly":{"name":"Acme","version":"1.0.0"},"types":[]}""");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "MyApp.csproj"),
                """<Project Sdk="Microsoft.NET.Sdk"></Project>""");

            var result = await InvokeAsync(DoctorCommand.Create(), $"--project-dir \"{tempRoot}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("WrapGod.Generator not referenced", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task Doctor_MissingCacheDirectory_ReportsFailure()
    {
        var tempRoot = CreateTempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "wrapgod.config.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "vendor.wrapgod.json"),
                """{"schemaVersion":"1.0","generatedAt":"2026-01-01T00:00:00Z","assembly":{"name":"Acme","version":"1.0.0"},"types":[]}""");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "MyApp.csproj"),
                """<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="WrapGod.Generator" Version="1.0.0" /></ItemGroup></Project>""");

            var result = await InvokeAsync(DoctorCommand.Create(), $"--project-dir \"{tempRoot}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Cache directory missing", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // MigrateInit happy-path and branch coverage
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task MigrateInit_NoManifest_ScansSuccessfully()
    {
        var tempRoot = CreateTempDir();
        try
        {
            // No manifest -- should scan with "common patterns" message
            var outputPath = Path.Combine(tempRoot, "plan.json");

            var result = await InvokeAsync(MigrateInitCommand.Create(),
                $"init --project-dir \"{tempRoot}\" --output \"{outputPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("No manifest found", result.StdOut);
            Assert.Contains("Plan written to", result.StdOut);
            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task MigrateInit_WithManifest_ScansAndProducesPlan()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "schemaVersion": "1.0",
                  "generatedAt": "2026-01-01T00:00:00Z",
                  "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
                  "types": [
                    { "fullName": "Acme.Lib.FooService", "name": "FooService" },
                    { "fullName": "Acme.Lib.BarClient", "name": "BarClient" }
                  ]
                }
                """);

            // Create a source file that uses the types
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "MyService.cs"), """
                using Acme.Lib;
                namespace MyApp;
                public class MyService
                {
                    private readonly FooService _foo;
                    private readonly BarClient _bar;
                    public MyService(FooService foo, BarClient bar)
                    {
                        _foo = foo;
                        _bar = bar;
                    }
                }
                """);

            var outputPath = Path.Combine(tempRoot, "plan.json");
            var result = await InvokeAsync(MigrateInitCommand.Create(),
                $"init --project-dir \"{tempRoot}\" --manifest \"{manifestPath}\" --output \"{outputPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Loaded", result.StdOut);
            Assert.Contains("types from manifest", result.StdOut);
            Assert.Contains("Plan written to", result.StdOut);
            Assert.True(File.Exists(outputPath));

            // Verify the plan JSON is valid
            var planJson = await File.ReadAllTextAsync(outputPath);
            var doc = JsonDocument.Parse(planJson);
            Assert.True(doc.RootElement.TryGetProperty("summary", out _));
            Assert.True(doc.RootElement.TryGetProperty("actions", out _));
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task MigrateInit_AutoDetectsManifest()
    {
        var tempRoot = CreateTempDir();
        try
        {
            // Put a manifest in the project dir without --manifest flag
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "vendor.wrapgod.json"), """
                {
                  "schemaVersion": "1.0",
                  "generatedAt": "2026-01-01T00:00:00Z",
                  "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
                  "types": []
                }
                """);

            var outputPath = Path.Combine(tempRoot, "plan.json");
            var result = await InvokeAsync(MigrateInitCommand.Create(),
                $"init --project-dir \"{tempRoot}\" --output \"{outputPath}\"");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Auto-detected manifest", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task MigrateInit_ClassifiesActions_TypeofAsManual()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "schemaVersion": "1.0",
                  "generatedAt": "2026-01-01T00:00:00Z",
                  "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
                  "types": [
                    { "fullName": "Acme.Lib.FooService", "name": "FooService" }
                  ]
                }
                """);

            // Source file with typeof() usage -- should be classified "manual"
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "Reflector.cs"), """
                namespace MyApp;
                public class Reflector
                {
                    public System.Type Get() => typeof(FooService);
                }
                """);

            var outputPath = Path.Combine(tempRoot, "plan.json");
            var result = await InvokeAsync(MigrateInitCommand.Create(),
                $"init --project-dir \"{tempRoot}\" --manifest \"{manifestPath}\" --output \"{outputPath}\"");

            Assert.Equal(0, result.ExitCode);
            var planJson = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("\"manual\"", planJson);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task MigrateInit_ClassifiesActions_InheritanceAsAssisted()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var manifestPath = Path.Combine(tempRoot, "vendor.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "schemaVersion": "1.0",
                  "generatedAt": "2026-01-01T00:00:00Z",
                  "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
                  "types": [
                    { "fullName": "Acme.Lib.BaseHandler", "name": "BaseHandler" }
                  ]
                }
                """);

            // Source file with inheritance -- should be classified "assisted"
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "MyHandler.cs"), """
                namespace MyApp;
                public class MyHandler : BaseHandler
                {
                    public void Handle() { }
                }
                """);

            var outputPath = Path.Combine(tempRoot, "plan.json");
            var result = await InvokeAsync(MigrateInitCommand.Create(),
                $"init --project-dir \"{tempRoot}\" --manifest \"{manifestPath}\" --output \"{outputPath}\"");

            Assert.Equal(0, result.ExitCode);
            var planJson = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("\"assisted\"", planJson);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task MigrateInit_ProjectPathIsFile_ReturnsExitCode1()
    {
        var tempRoot = CreateTempDir();
        try
        {
            var filePath = Path.Combine(tempRoot, "project.txt");
            await File.WriteAllTextAsync(filePath, "x");

            var result = await InvokeAsync(MigrateInitCommand.Create(), $"init --project-dir \"{filePath}\"");

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("Project directory not found", result.StdErr);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    [Fact]
    public async Task MigrateInit_SkipsObjAndBinDirectories()
    {
        var tempRoot = CreateTempDir();
        try
        {
            // Create a source file in obj/ -- should be excluded
            var objDir = Path.Combine(tempRoot, "obj");
            Directory.CreateDirectory(objDir);
            await File.WriteAllTextAsync(Path.Combine(objDir, "Generated.cs"), "class Generated {}");

            // Create a source file in src -- should be included
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "App.cs"), "class App {}");

            var outputPath = Path.Combine(tempRoot, "plan.json");
            var result = await InvokeAsync(MigrateInitCommand.Create(),
                $"init --project-dir \"{tempRoot}\" --output \"{outputPath}\"");

            Assert.Equal(0, result.ExitCode);
            // Should find 1 source file (App.cs), not 2
            Assert.Contains("Found 1 source files", result.StdOut);
        }
        finally
        {
            SafeDelete(tempRoot);
        }
    }

    private static string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wrapgod-cli-hardening-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SafeDelete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
