using WrapGod.Abstractions.Diagnostics;
using WrapGod.Cli;

namespace WrapGod.Tests;

public sealed class DoctorHealthValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ReportsError_WhenDotnetMissing()
    {
        var root = CreateRepoRoot();
        var validator = new DoctorHealthValidator((_, _, _) => (-1, string.Empty, "dotnet not found"));
        var diagnostics = await validator.ValidateAsync(root);
        Assert.Contains(diagnostics, d => d.Code == "WG7104" && d.Severity == WgDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task ValidateAsync_ReportsDependencyWarnings_WhenLockfileAndDiscoveryAreMissing()
    {
        var root = CreateRepoRoot();
        var validator = new DoctorHealthValidator((_, _, _) => (0, "10.0.104", string.Empty));
        var diagnostics = await validator.ValidateAsync(root);
        Assert.Contains(diagnostics, d => d.Code == "WG7110" && d.Tags!.Contains("dependency:#124"));
        Assert.Contains(diagnostics, d => d.Code == "WG7112" && d.Tags!.Contains("dependency:#123"));
    }

    [Fact]
    public async Task HandleAsync_ReturnsWarningsAsErrors_WhenEnabled()
    {
        var root = CreateRepoRoot();
        var code = await DoctorCommand.HandleAsync(root, "json", warningsAsErrors: true);
        Assert.Equal((int)WgCliExitCode.WarningsAsErrors, code);
    }

    private static DirectoryInfo CreateRepoRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "wrapgod-doctor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var workflows = Path.Combine(path, ".github", "workflows");
        Directory.CreateDirectory(workflows);
        File.WriteAllText(Path.Combine(workflows, "ci.yml"), "name: ci\nsteps:\n  - run: dotnet test");
        return new DirectoryInfo(path);
    }
}
