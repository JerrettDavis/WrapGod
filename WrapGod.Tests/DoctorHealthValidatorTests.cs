using WrapGod.Abstractions.Diagnostics;
using WrapGod.Cli;

namespace WrapGod.Tests;

public sealed class DoctorHealthValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ReportsError_WhenPathMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), "wrapgod-doctor-tests", Guid.NewGuid().ToString("N"));
        var validator = new DoctorHealthValidator((_, _, _) => (0, "10.0.104", string.Empty));
        var diagnostics = await validator.ValidateAsync(new DirectoryInfo(path));
        Assert.Contains(diagnostics, d => d.Code == "WG7100");
    }

    [Fact]
    public async Task ValidateAsync_ReportsDependencyWarnings_WhenLockfileAndDiscoveryMissing()
    {
        var root = CreateRepoRoot();
        var validator = new DoctorHealthValidator((_, _, _) => (0, "10.0.104", string.Empty));
        var diagnostics = await validator.ValidateAsync(root);
        Assert.Contains(diagnostics, d => d.Code == "WG7110");
        Assert.Contains(diagnostics, d => d.Code == "WG7112");
    }

    private static DirectoryInfo CreateRepoRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "wrapgod-doctor-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var workflows = Path.Combine(path, ".github", "workflows");
        Directory.CreateDirectory(workflows);
        File.WriteAllText(Path.Combine(workflows, "ci.yml"), "name: ci\n");
        return new DirectoryInfo(path);
    }
}
