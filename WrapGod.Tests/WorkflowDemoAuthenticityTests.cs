using System.Diagnostics;
using System.Text.Json;

namespace WrapGod.Tests;

public sealed class WorkflowDemoAuthenticityTests
{
    [Fact]
    public async Task WorkflowDemo_ProducesRealPipelineArtifacts()
    {
        var repoRoot = FindRepoRoot();
        var outputDir = Path.Combine(repoRoot, "examples", "output");
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, recursive: true);
        }

        var demoProject = Path.Combine(repoRoot, "examples", "WrapGod.WorkflowDemo", "WrapGod.WorkflowDemo.csproj");
        var run = await RunProcessAsync("dotnet", $"run --project \"{demoProject}\" -nologo", repoRoot);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("WG2001 present: True", run.Output, StringComparison.Ordinal);
        Assert.Contains("WG2002 present: True", run.Output, StringComparison.Ordinal);
        Assert.Contains("Generated BetterFoo interface: True", run.Output, StringComparison.Ordinal);
        Assert.Contains("Generated BarClient wrapper (expected false): False", run.Output, StringComparison.Ordinal);

        var manifestPath = Path.Combine(outputDir, "acme.wrapgod.json");
        var diagnosticsPath = Path.Combine(outputDir, "diagnostics.txt");
        var fixedSourcePath = Path.Combine(outputDir, "Consumer.fixed.cs");
        var generatedInterfacePath = Path.Combine(outputDir, "generated", "IWrappedBetterFoo.g.cs");
        var generatedFacadePath = Path.Combine(outputDir, "generated", "BetterFooFacade.g.cs");
        var excludedInterfacePath = Path.Combine(outputDir, "generated", "IWrappedBarClient.g.cs");

        Assert.True(File.Exists(manifestPath), "manifest was not generated");
        Assert.True(File.Exists(diagnosticsPath), "diagnostics output was not generated");
        Assert.True(File.Exists(fixedSourcePath), "fixed source output was not generated");
        Assert.True(File.Exists(generatedInterfacePath), "wrapped interface output was not generated");
        Assert.True(File.Exists(generatedFacadePath), "facade output was not generated");
        Assert.False(File.Exists(excludedInterfacePath), "excluded type wrapper should not be generated");

        using (var json = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath)))
        {
            var root = json.RootElement;
            var assembly = root.GetProperty("assembly");
            Assert.Equal("Acme.Lib", assembly.GetProperty("name").GetString());

            var types = root.GetProperty("types").EnumerateArray().ToList();
            Assert.NotEmpty(types);
            Assert.Contains(types, t => t.GetProperty("fullName").GetString() == "Acme.Lib.FooService");
        }

        var diagnostics = await File.ReadAllTextAsync(diagnosticsPath);
        Assert.Contains("WG2001", diagnostics, StringComparison.Ordinal);
        Assert.Contains("WG2002", diagnostics, StringComparison.Ordinal);

        var fixedSource = await File.ReadAllTextAsync(fixedSourcePath);
        Assert.Contains("IWrappedBetterFoo", fixedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private Acme.Lib.FooService _svc", fixedSource, StringComparison.Ordinal);

        var generatedInterface = await File.ReadAllTextAsync(generatedInterfacePath);
        Assert.Contains("interface IWrappedBetterFoo", generatedInterface, StringComparison.Ordinal);

        var generatedFacade = await File.ReadAllTextAsync(generatedFacadePath);
        Assert.Contains("class BetterFooFacade", generatedFacade, StringComparison.Ordinal);
        Assert.Contains("Acme.Lib.FooService", generatedFacade, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "WrapGod.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string arguments, string workingDirectory)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start process: {fileName}");
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, stdout + Environment.NewLine + stderr);
    }
}
