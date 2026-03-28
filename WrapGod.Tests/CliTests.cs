using System.Diagnostics;
using System.Text.Json;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("CLI commands: unit, integration, and end-to-end tests for all 9 WrapGod CLI commands")]
public sealed class CliTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Path to the solution root (repo root)
    // AppContext.BaseDirectory = .../WrapGod.Tests/bin/Release/net10.0/
    // Go up 4 levels: net10.0 → Release → bin → WrapGod.Tests → WrapGod (repo root)
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static readonly string CliDll = Path.Combine(
        RepoRoot, "WrapGod.Cli", "bin", "Release", "net10.0", "WrapGod.Cli.dll");

    private static readonly string RuntimeDll = Path.Combine(
        RepoRoot, "WrapGod.Runtime", "bin", "Release", "netstandard2.0", "WrapGod.Runtime.dll");

    private static (int ExitCode, string StdOut, string StdErr) RunCli(
        string args, string? workingDir = null)
    {
        if (!File.Exists(CliDll))
            return (-1, "", $"CLI DLL not found: {CliDll}");

        var psi = new ProcessStartInfo("dotnet", $"\"{CliDll}\" {args}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir ?? RepoRoot
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(30_000);
        return (process.ExitCode, stdout, stderr);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wrapgod-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CleanupTempDir(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
    }

    // ===== UNIT TESTS: CLI invocation basics =====

    [Scenario("Root command with --help shows available commands")]
    [Fact]
    public Task RootCommand_Help_ShowsCommands()
        => Given("the CLI is invoked with --help", () =>
        {
            var (exitCode, stdout, _) = RunCli("--help");
            return new { ExitCode = exitCode, StdOut = stdout };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output contains 'extract' command", result =>
            result.StdOut.Contains("extract", StringComparison.OrdinalIgnoreCase))
        .And("output contains 'analyze' command", result =>
            result.StdOut.Contains("analyze", StringComparison.OrdinalIgnoreCase))
        .And("output contains 'generate' command", result =>
            result.StdOut.Contains("generate", StringComparison.OrdinalIgnoreCase))
        .And("output contains 'init' command", result =>
            result.StdOut.Contains("init", StringComparison.OrdinalIgnoreCase))
        .And("output contains 'doctor' command", result =>
            result.StdOut.Contains("doctor", StringComparison.OrdinalIgnoreCase))
        .And("output contains 'explain' command", result =>
            result.StdOut.Contains("explain", StringComparison.OrdinalIgnoreCase))
        .And("output contains 'migrate' command", result =>
            result.StdOut.Contains("migrate", StringComparison.OrdinalIgnoreCase))
        .And("output contains 'ci' command", result =>
            result.StdOut.Contains("ci", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Extract command with --help shows options")]
    [Fact]
    public Task ExtractCommand_Help_ShowsOptions()
        => Given("the CLI is invoked with 'extract --help'", () =>
        {
            var (exitCode, stdout, _) = RunCli("extract --help");
            return new { ExitCode = exitCode, StdOut = stdout };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output mentions assembly-path", result =>
            result.StdOut.Contains("assembly-path", StringComparison.OrdinalIgnoreCase))
        .And("output mentions --output option", result =>
            result.StdOut.Contains("--output", StringComparison.OrdinalIgnoreCase))
        .And("output mentions --nuget option", result =>
            result.StdOut.Contains("--nuget", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Analyze command with --help shows options")]
    [Fact]
    public Task AnalyzeCommand_Help_ShowsOptions()
        => Given("the CLI is invoked with 'analyze --help'", () =>
        {
            var (exitCode, stdout, _) = RunCli("analyze --help");
            return new { ExitCode = exitCode, StdOut = stdout };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output mentions manifest-path", result =>
            result.StdOut.Contains("manifest-path", StringComparison.OrdinalIgnoreCase))
        .And("output mentions --warnings-as-errors", result =>
            result.StdOut.Contains("--warnings-as-errors", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Init command with --help shows options")]
    [Fact]
    public Task InitCommand_Help_ShowsOptions()
        => Given("the CLI is invoked with 'init --help'", () =>
        {
            var (exitCode, stdout, _) = RunCli("init --help");
            return new { ExitCode = exitCode, StdOut = stdout };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output mentions --source option", result =>
            result.StdOut.Contains("--source", StringComparison.OrdinalIgnoreCase))
        .And("output mentions --output option", result =>
            result.StdOut.Contains("--output", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Doctor command with --help shows options")]
    [Fact]
    public Task DoctorCommand_Help_ShowsOptions()
        => Given("the CLI is invoked with 'doctor --help'", () =>
        {
            var (exitCode, stdout, _) = RunCli("doctor --help");
            return new { ExitCode = exitCode, StdOut = stdout };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output mentions --project-dir option", result =>
            result.StdOut.Contains("--project-dir", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Explain command with --help shows options")]
    [Fact]
    public Task ExplainCommand_Help_ShowsOptions()
        => Given("the CLI is invoked with 'explain --help'", () =>
        {
            var (exitCode, stdout, _) = RunCli("explain --help");
            return new { ExitCode = exitCode, StdOut = stdout };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output mentions symbol argument", result =>
            result.StdOut.Contains("symbol", StringComparison.OrdinalIgnoreCase))
        .And("output mentions --manifest option", result =>
            result.StdOut.Contains("--manifest", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Migrate init command with --help shows options")]
    [Fact]
    public Task MigrateInitCommand_Help_ShowsOptions()
        => Given("the CLI is invoked with 'migrate init --help'", () =>
        {
            var (exitCode, stdout, _) = RunCli("migrate init --help");
            return new { ExitCode = exitCode, StdOut = stdout };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output mentions --project-dir option", result =>
            result.StdOut.Contains("--project-dir", StringComparison.OrdinalIgnoreCase))
        .And("output mentions --manifest option", result =>
            result.StdOut.Contains("--manifest", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("CI bootstrap command with --help shows options")]
    [Fact]
    public Task CiBootstrapCommand_Help_ShowsOptions()
        => Given("the CLI is invoked with 'ci bootstrap --help'", () =>
        {
            var (exitCode, stdout, _) = RunCli("ci bootstrap --help");
            return new { ExitCode = exitCode, StdOut = stdout };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output mentions --output option", result =>
            result.StdOut.Contains("--output", StringComparison.OrdinalIgnoreCase))
        .And("output mentions --force option", result =>
            result.StdOut.Contains("--force", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("CI parity command with --help shows options")]
    [Fact]
    public Task CiParityCommand_Help_ShowsOptions()
        => Given("the CLI is invoked with 'ci parity --help'", () =>
        {
            var (exitCode, stdout, _) = RunCli("ci parity --help");
            return new { ExitCode = exitCode, StdOut = stdout };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output mentions --workflow-dir option", result =>
            result.StdOut.Contains("--workflow-dir", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Unknown command returns non-zero exit code")]
    [Fact]
    public Task UnknownCommand_ReturnsError()
        => Given("the CLI is invoked with an unknown command", () =>
        {
            var (exitCode, _, stderr) = RunCli("nonexistent-command");
            return new { ExitCode = exitCode, StdErr = stderr };
        })
        .Then("exit code is non-zero", result => result.ExitCode != 0)
        .AssertPassed();

    // ===== INTEGRATION TESTS: commands with real files =====

    [Scenario("Extract from WrapGod.Runtime assembly produces valid manifest")]
    [Fact]
    public Task Extract_FromRuntimeAssembly_ProducesValidManifest()
        => Given("a real assembly and a temp output path", () =>
        {
            var tempDir = CreateTempDir();
            var outputPath = Path.Combine(tempDir, "manifest.wrapgod.json");

            var (exitCode, stdout, stderr) = RunCli(
                $"extract \"{RuntimeDll}\" --output \"{outputPath}\"");

            string? manifestJson = null;
            if (File.Exists(outputPath))
                manifestJson = File.ReadAllText(outputPath);

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr, ManifestJson = manifestJson };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("manifest file was created", result => result.ManifestJson is not null)
        .And("manifest is valid JSON", result =>
        {
            try { JsonDocument.Parse(result.ManifestJson!); return true; }
            catch { return false; }
        })
        .And("manifest contains schemaVersion", result =>
            result.ManifestJson!.Contains("schemaVersion", StringComparison.OrdinalIgnoreCase))
        .And("manifest contains assembly info", result =>
            result.ManifestJson!.Contains("assembly", StringComparison.OrdinalIgnoreCase))
        .And("manifest contains types array", result =>
            result.ManifestJson!.Contains("types", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Extract with no arguments shows error")]
    [Fact]
    public Task Extract_NoArguments_ShowsError()
        => Given("the CLI is invoked with 'extract' and no arguments", () =>
        {
            var (exitCode, stdout, stderr) = RunCli("extract");
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("output contains error about missing input", result =>
            result.StdErr.Contains("assembly-path", StringComparison.OrdinalIgnoreCase) ||
            result.StdErr.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
            result.StdOut.Contains("Error", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Extract with non-existent assembly shows error")]
    [Fact]
    public Task Extract_NonExistentAssembly_ShowsError()
        => Given("a path to a non-existent assembly", () =>
        {
            var fakePath = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid() + ".dll");
            var (exitCode, stdout, stderr) = RunCli($"extract \"{fakePath}\"");
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("stderr mentions assembly not found", result =>
            result.StdErr.Contains("not found", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Analyze valid manifest returns exit code 0")]
    [Fact]
    public Task Analyze_ValidManifest_ReturnsSuccess()
        => Given("a manifest extracted from WrapGod.Runtime", () =>
        {
            var tempDir = CreateTempDir();
            var manifestPath = Path.Combine(tempDir, "manifest.wrapgod.json");

            // Extract first
            RunCli($"extract \"{RuntimeDll}\" --output \"{manifestPath}\"");

            // Then analyze
            var (exitCode, stdout, stderr) = RunCli($"analyze \"{manifestPath}\"");

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output mentions Assembly", result =>
            result.StdOut.Contains("Assembly", StringComparison.OrdinalIgnoreCase))
        .And("output mentions Types count", result =>
            result.StdOut.Contains("Types:", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Analyze missing manifest returns error message")]
    [Fact]
    public Task Analyze_MissingManifest_ReturnsError()
        => Given("a path to a non-existent manifest", () =>
        {
            var fakePath = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid() + ".json");
            var (exitCode, stdout, stderr) = RunCli($"analyze \"{fakePath}\"");
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("stderr mentions manifest not found", result =>
            result.StdErr.Contains("not found", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Generate with valid manifest prints usage info")]
    [Fact]
    public Task Generate_WithManifest_PrintsUsageInfo()
        => Given("a valid manifest", () =>
        {
            var tempDir = CreateTempDir();
            var manifestPath = Path.Combine(tempDir, "manifest.wrapgod.json");
            RunCli($"extract \"{RuntimeDll}\" --output \"{manifestPath}\"");

            var (exitCode, stdout, stderr) = RunCli($"generate \"{manifestPath}\"");

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output mentions WrapGod Generator", result =>
            result.StdOut.Contains("WrapGod Generator", StringComparison.Ordinal))
        .And("output mentions compile-time", result =>
            result.StdOut.Contains("compile", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Generate with missing manifest shows error")]
    [Fact]
    public Task Generate_MissingManifest_ShowsError()
        => Given("a path to a non-existent manifest", () =>
        {
            var fakePath = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid() + ".json");
            var (exitCode, stdout, stderr) = RunCli($"generate \"{fakePath}\"");
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("stderr mentions manifest not found", result =>
            result.StdErr.Contains("not found", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Init in temp directory creates config and cache")]
    [Fact]
    public Task Init_InTempDir_CreatesConfigAndCache()
        => Given("a clean temp directory", () =>
        {
            var tempDir = CreateTempDir();
            var configPath = Path.Combine(tempDir, "wrapgod.config.json");

            var (exitCode, stdout, stderr) = RunCli(
                $"init --source test-assembly.dll --output \"{configPath}\"");

            var configExists = File.Exists(configPath);
            var cacheExists = Directory.Exists(Path.Combine(tempDir, ".wrapgod-cache"));
            string? configJson = configExists ? File.ReadAllText(configPath) : null;

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr,
                         ConfigExists = configExists, CacheExists = cacheExists,
                         ConfigJson = configJson };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("config file was created", result => result.ConfigExists)
        .And("cache directory was created", result => result.CacheExists)
        .And("config is valid JSON", result =>
        {
            try { JsonDocument.Parse(result.ConfigJson!); return true; }
            catch { return false; }
        })
        .And("stdout mentions 'Created config'", result =>
            result.StdOut.Contains("Created config", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Init when config already exists shows error")]
    [Fact]
    public Task Init_ConfigAlreadyExists_ShowsError()
        => Given("a temp directory with an existing config file", () =>
        {
            var tempDir = CreateTempDir();
            var configPath = Path.Combine(tempDir, "wrapgod.config.json");
            File.WriteAllText(configPath, "{}");

            var (exitCode, stdout, stderr) = RunCli(
                $"init --source test.dll --output \"{configPath}\"");

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("stderr mentions config already exists", result =>
            result.StdErr.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Doctor in empty temp directory reports failures")]
    [Fact]
    public Task Doctor_EmptyDir_ReportsFailures()
        => Given("a temp directory with no project files", () =>
        {
            var tempDir = CreateTempDir();

            var (exitCode, stdout, stderr) = RunCli(
                $"doctor --project-dir \"{tempDir}\"");

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("output mentions WrapGod Doctor", result =>
            result.StdOut.Contains("WrapGod Doctor", StringComparison.Ordinal))
        .And("output reports failures", result =>
            result.StdOut.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
        .And("output contains results summary", result =>
            result.StdOut.Contains("Results:", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Doctor in directory with manifest and config reports passes")]
    [Fact]
    public Task Doctor_WithManifestAndConfig_ReportsPasses()
        => Given("a temp directory with config and manifest", () =>
        {
            var tempDir = CreateTempDir();

            // Create config
            var configPath = Path.Combine(tempDir, "wrapgod.config.json");
            File.WriteAllText(configPath, """{"source": "test.dll", "types": []}""");

            // Extract a real manifest
            var manifestPath = Path.Combine(tempDir, "test.wrapgod.json");
            RunCli($"extract \"{RuntimeDll}\" --output \"{manifestPath}\"");

            // Create cache directory
            Directory.CreateDirectory(Path.Combine(tempDir, ".wrapgod-cache"));

            var (exitCode, stdout, stderr) = RunCli(
                $"doctor --project-dir \"{tempDir}\"");

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("output mentions WrapGod Doctor", result =>
            result.StdOut.Contains("WrapGod Doctor", StringComparison.Ordinal))
        .And("output reports passes for config", result =>
            result.StdOut.Contains("Config file valid", StringComparison.OrdinalIgnoreCase))
        .And("output reports passes for manifest", result =>
            result.StdOut.Contains("Manifest valid", StringComparison.OrdinalIgnoreCase))
        .And("output reports passes for cache", result =>
            result.StdOut.Contains("Cache directory exists", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Explain a type from a real manifest shows type info")]
    [Fact]
    public Task Explain_KnownType_ShowsTypeInfo()
        => Given("a manifest extracted from WrapGod.Runtime", () =>
        {
            var tempDir = CreateTempDir();
            var manifestPath = Path.Combine(tempDir, "manifest.wrapgod.json");
            RunCli($"extract \"{RuntimeDll}\" --output \"{manifestPath}\"");

            // Read manifest to find a real type name
            var json = File.ReadAllText(manifestPath);
            var doc = JsonDocument.Parse(json);
            var types = doc.RootElement.GetProperty("Types");
            var firstType = types.EnumerateArray().FirstOrDefault();
            var typeName = firstType.ValueKind != JsonValueKind.Undefined
                ? firstType.GetProperty("FullName").GetString() ?? "Unknown"
                : "Unknown";

            var (exitCode, stdout, stderr) = RunCli(
                $"explain \"{typeName}\" --manifest \"{manifestPath}\"");

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr, TypeName = typeName };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("output contains Type:", result =>
            result.StdOut.Contains("Type:", StringComparison.OrdinalIgnoreCase))
        .And("output contains Assembly:", result =>
            result.StdOut.Contains("Assembly:", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Explain unknown symbol shows not found")]
    [Fact]
    public Task Explain_UnknownSymbol_ShowsNotFound()
        => Given("a manifest and a non-existent symbol", () =>
        {
            var tempDir = CreateTempDir();
            var manifestPath = Path.Combine(tempDir, "manifest.wrapgod.json");
            RunCli($"extract \"{RuntimeDll}\" --output \"{manifestPath}\"");

            var (exitCode, stdout, stderr) = RunCli(
                $"explain \"NonExistent.FakeType.DoesNotExist\" --manifest \"{manifestPath}\"");

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("output mentions not found", result =>
            result.StdOut.Contains("not found", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("Migrate init in temp directory with manifest and .cs files produces migration plan")]
    [Fact]
    public Task MigrateInit_WithSourceFiles_ProducesPlan()
        => Given("a temp directory with a manifest and C# source files", () =>
        {
            var tempDir = CreateTempDir();

            // Extract manifest
            var manifestPath = Path.Combine(tempDir, "test.wrapgod.json");
            RunCli($"extract \"{RuntimeDll}\" --output \"{manifestPath}\"");

            // Read manifest to get a real type name
            var manifestJson = File.ReadAllText(manifestPath);
            var doc = JsonDocument.Parse(manifestJson);
            var types = doc.RootElement.GetProperty("Types");
            var firstType = types.EnumerateArray().FirstOrDefault();
            var typeName = firstType.ValueKind != JsonValueKind.Undefined
                ? firstType.GetProperty("Name").GetString() ?? "SomeType"
                : "SomeType";

            // Create a fake .cs file that references the type
            var csContent = $"using System;\nclass MyConsumer\n{{\n    private {typeName} _svc = new {typeName}();\n}}\n";
            File.WriteAllText(Path.Combine(tempDir, "Consumer.cs"), csContent);

            var planPath = Path.Combine(tempDir, "migration-plan.json");
            var (exitCode, stdout, stderr) = RunCli(
                $"migrate init --project-dir \"{tempDir}\" --manifest \"{manifestPath}\" --output \"{planPath}\"");

            string? planJson = File.Exists(planPath) ? File.ReadAllText(planPath) : null;

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr, PlanJson = planJson };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("migration plan file was created", result => result.PlanJson is not null)
        .And("plan is valid JSON", result =>
        {
            try { JsonDocument.Parse(result.PlanJson!); return true; }
            catch { return false; }
        })
        .And("plan contains summary section", result =>
            result.PlanJson!.Contains("summary", StringComparison.OrdinalIgnoreCase))
        .And("stdout mentions Migration Plan Summary", result =>
            result.StdOut.Contains("Migration Plan", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("CI bootstrap generates workflow file")]
    [Fact]
    public Task CiBootstrap_GeneratesWorkflowFile()
        => Given("a temp directory for workflow output", () =>
        {
            var tempDir = CreateTempDir();

            var (exitCode, stdout, stderr) = RunCli(
                $"ci bootstrap --output \"{tempDir}\"");

            var workflowPath = Path.Combine(tempDir, "wrapgod-ci.yml");
            var workflowExists = File.Exists(workflowPath);
            string? workflowContent = workflowExists ? File.ReadAllText(workflowPath) : null;

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr,
                         WorkflowExists = workflowExists, WorkflowContent = workflowContent };
        })
        .Then("exit code is 0", result => result.ExitCode == 0)
        .And("workflow file was created", result => result.WorkflowExists)
        .And("workflow contains dotnet build step", result =>
            result.WorkflowContent!.Contains("dotnet build", StringComparison.OrdinalIgnoreCase))
        .And("workflow contains wrap-god extract step", result =>
            result.WorkflowContent!.Contains("wrap-god extract", StringComparison.OrdinalIgnoreCase))
        .And("workflow contains wrap-god analyze step", result =>
            result.WorkflowContent!.Contains("wrap-god analyze", StringComparison.OrdinalIgnoreCase))
        .And("stdout mentions Generated", result =>
            result.StdOut.Contains("Generated", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("CI bootstrap with --force overwrites existing workflow")]
    [Fact]
    public Task CiBootstrap_WithForce_OverwritesExisting()
        => Given("a temp directory with an existing workflow file", () =>
        {
            var tempDir = CreateTempDir();
            var workflowPath = Path.Combine(tempDir, "wrapgod-ci.yml");
            File.WriteAllText(workflowPath, "old-content");

            // Without --force, should warn about existing file
            var (_, stdoutNoForce, stderrNoForce) = RunCli(
                $"ci bootstrap --output \"{tempDir}\"");

            // With --force, should succeed and overwrite
            var (exitCodeForce, stdoutForce, _) = RunCli(
                $"ci bootstrap --output \"{tempDir}\" --force");

            var newContent = File.ReadAllText(workflowPath);

            CleanupTempDir(tempDir);
            return new { StdOutNoForce = stdoutNoForce, StdErrNoForce = stderrNoForce,
                         ExitCodeForce = exitCodeForce, StdOutForce = stdoutForce,
                         NewContent = newContent };
        })
        .Then("without --force, output warns about existing file", result =>
            result.StdErrNoForce.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        .And("with --force, exit code is 0", result => result.ExitCodeForce == 0)
        .And("with --force, output mentions Generated", result =>
            result.StdOutForce.Contains("Generated", StringComparison.OrdinalIgnoreCase))
        .And("file content is updated (not old-content)", result =>
            result.NewContent.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("CI parity in directory without workflows reports no files found")]
    [Fact]
    public Task CiParity_NoWorkflows_ReportsNoFilesFound()
        => Given("a temp directory with no workflow files", () =>
        {
            var tempDir = CreateTempDir();

            var (exitCode, stdout, stderr) = RunCli(
                $"ci parity --workflow-dir \"{tempDir}\"");

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("output mentions no workflow files found", result =>
            result.StdOut.Contains("No workflow", StringComparison.OrdinalIgnoreCase) ||
            result.StdErr.Contains("No workflow", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("CI parity with non-existent workflow directory reports not found")]
    [Fact]
    public Task CiParity_NonExistentDir_ReportsNotFound()
        => Given("a non-existent workflow directory", () =>
        {
            var fakePath = Path.Combine(Path.GetTempPath(), "nonexistent-" + Guid.NewGuid().ToString("N")[..8]);
            var (exitCode, stdout, stderr) = RunCli(
                $"ci parity --workflow-dir \"{fakePath}\"");
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("output mentions directory not found", result =>
            result.StdOut.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            result.StdErr.Contains("not found", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("CI parity with bootstrapped workflow shows full parity")]
    [Fact]
    public Task CiParity_WithBootstrappedWorkflow_ShowsParity()
        => Given("a temp directory with a bootstrapped workflow", () =>
        {
            var tempDir = CreateTempDir();

            // Bootstrap first
            RunCli($"ci bootstrap --output \"{tempDir}\"");

            // Then check parity
            var (exitCode, stdout, stderr) = RunCli(
                $"ci parity --workflow-dir \"{tempDir}\"");

            CleanupTempDir(tempDir);
            return new { ExitCode = exitCode, StdOut = stdout, StdErr = stderr };
        })
        .Then("exit code is 0 (full parity)", result => result.ExitCode == 0)
        .And("output mentions Parity percentage", result =>
            result.StdOut.Contains("Parity:", StringComparison.OrdinalIgnoreCase))
        .And("output shows all steps as PASS", result =>
            !result.StdOut.Contains("[MISS]", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    // ===== E2E TESTS: full pipeline =====

    [Scenario("E2E: Extract then Analyze pipeline succeeds")]
    [Fact]
    public Task E2E_Extract_Then_Analyze_Succeeds()
        => Given("a full extract-then-analyze pipeline", () =>
        {
            var tempDir = CreateTempDir();
            var manifestPath = Path.Combine(tempDir, "manifest.wrapgod.json");

            // Step 1: Extract
            var (extractExit, extractOut, extractErr) = RunCli(
                $"extract \"{RuntimeDll}\" --output \"{manifestPath}\"");

            // Step 2: Analyze
            var (analyzeExit, analyzeOut, analyzeErr) = RunCli(
                $"analyze \"{manifestPath}\"");

            CleanupTempDir(tempDir);
            return new
            {
                ExtractExit = extractExit, ExtractOut = extractOut,
                AnalyzeExit = analyzeExit, AnalyzeOut = analyzeOut
            };
        })
        .Then("extract succeeds", result => result.ExtractExit == 0)
        .And("analyze succeeds", result => result.AnalyzeExit == 0)
        .And("extract output mentions Types", result =>
            result.ExtractOut.Contains("Types", StringComparison.OrdinalIgnoreCase))
        .And("analyze output mentions Assembly", result =>
            result.AnalyzeOut.Contains("Assembly", StringComparison.OrdinalIgnoreCase))
        .And("analyze output shows type breakdown", result =>
            result.AnalyzeOut.Contains("Type breakdown", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("E2E: Init then Doctor validates setup")]
    [Fact]
    public Task E2E_Init_Then_Doctor_ValidatesSetup()
        => Given("init creates project structure, then doctor validates it", () =>
        {
            var tempDir = CreateTempDir();
            var configPath = Path.Combine(tempDir, "wrapgod.config.json");

            // Step 1: Init
            var (initExit, initOut, _) = RunCli(
                $"init --source test.dll --output \"{configPath}\"");

            // Step 2: Extract a manifest into the same directory
            var manifestPath = Path.Combine(tempDir, "test.wrapgod.json");
            RunCli($"extract \"{RuntimeDll}\" --output \"{manifestPath}\"");

            // Step 3: Doctor
            var (doctorExit, doctorOut, _) = RunCli(
                $"doctor --project-dir \"{tempDir}\"");

            CleanupTempDir(tempDir);
            return new
            {
                InitExit = initExit, InitOut = initOut,
                DoctorExit = doctorExit, DoctorOut = doctorOut
            };
        })
        .Then("init succeeds", result => result.InitExit == 0)
        .And("init output mentions Created config", result =>
            result.InitOut.Contains("Created config", StringComparison.OrdinalIgnoreCase))
        .And("doctor reports config valid", result =>
            result.DoctorOut.Contains("Config file valid", StringComparison.OrdinalIgnoreCase))
        .And("doctor reports manifest valid", result =>
            result.DoctorOut.Contains("Manifest valid", StringComparison.OrdinalIgnoreCase))
        .And("doctor reports cache exists", result =>
            result.DoctorOut.Contains("Cache directory exists", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();

    [Scenario("E2E: Extract then Explain shows type info from pipeline")]
    [Fact]
    public Task E2E_Extract_Then_Explain_ShowsTypeInfo()
        => Given("extract produces a manifest then explain queries it", () =>
        {
            var tempDir = CreateTempDir();
            var manifestPath = Path.Combine(tempDir, "manifest.wrapgod.json");

            // Step 1: Extract
            RunCli($"extract \"{RuntimeDll}\" --output \"{manifestPath}\"");

            // Step 2: Find a type name
            var json = File.ReadAllText(manifestPath);
            var doc = JsonDocument.Parse(json);
            var types = doc.RootElement.GetProperty("Types");
            var firstType = types.EnumerateArray().FirstOrDefault();
            var typeName = firstType.ValueKind != JsonValueKind.Undefined
                ? firstType.GetProperty("Name").GetString() ?? "Unknown"
                : "Unknown";

            // Step 3: Explain
            var (explainExit, explainOut, _) = RunCli(
                $"explain \"{typeName}\" --manifest \"{manifestPath}\"");

            CleanupTempDir(tempDir);
            return new
            {
                ExplainExit = explainExit, ExplainOut = explainOut, TypeName = typeName
            };
        })
        .Then("explain succeeds", result => result.ExplainExit == 0)
        .And("output shows the type name", result =>
            result.ExplainOut.Contains(result.TypeName, StringComparison.OrdinalIgnoreCase))
        .And("output shows wrapper info", result =>
            result.ExplainOut.Contains("IWrapped", StringComparison.OrdinalIgnoreCase))
        .AssertPassed();
}
