using System.CommandLine;
using WrapGod.Cli;

namespace WrapGod.Tests;

public sealed class CliCommandTests
{
    private static readonly string[] ExpectedRootCommands =
        ["analyze", "ci", "doctor", "explain", "extract", "generate", "init", "migrate"];

    private static readonly string[] ExpectedCiCommands = ["bootstrap", "parity"];

    [Fact]
    public void RootCommand_WiresExpectedCommands()
    {
        var root = CreateRootCommand();
        var commandNames = root.Subcommands.Select(c => c.Name).OrderBy(n => n).ToArray();

        Assert.Equal(ExpectedRootCommands, commandNames);

        var ci = root.Subcommands.Single(c => c.Name == "ci");
        var ciNames = ci.Subcommands.Select(c => c.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedCiCommands, ciNames);
    }

    [Fact]
    public async Task AnalyzeCommand_MissingManifest_SetsRuntimeFailureExitCode()
    {
        var command = AnalyzeCommand.Create();
        var previousExitCode = Environment.ExitCode;
        Environment.ExitCode = 0;

        try
        {
            await command.InvokeAsync("does-not-exist.wrapgod.json");

            Assert.Equal(1, Environment.ExitCode);
        }
        finally
        {
            Environment.ExitCode = previousExitCode;
        }
    }

    [Fact]
    public async Task AnalyzeCommand_WarningsAsErrors_ConfigMissing_SetsWarningGateExitCode()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"wrapgod-cli-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var manifestPath = Path.Combine(tempRoot, "sample.wrapgod.json");
            await File.WriteAllTextAsync(manifestPath, """
                {
                  "schemaVersion": "1.0",
                  "generatedAt": "2026-03-29T00:00:00Z",
                  "assembly": { "name": "Acme.Lib", "version": "1.0.0" },
                  "types": []
                }
                """);

            var missingConfigPath = Path.Combine(tempRoot, "missing.wrapgod.config.json");
            var command = AnalyzeCommand.Create();

            var previousExitCode = Environment.ExitCode;
            Environment.ExitCode = 0;

            try
            {
                await command.InvokeAsync($"\"{manifestPath}\" --config \"{missingConfigPath}\" --warnings-as-errors");

                Assert.Equal(3, Environment.ExitCode);
            }
            finally
            {
                Environment.ExitCode = previousExitCode;
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
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
}
