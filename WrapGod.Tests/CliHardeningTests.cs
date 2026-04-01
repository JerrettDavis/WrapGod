using System.CommandLine;
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
