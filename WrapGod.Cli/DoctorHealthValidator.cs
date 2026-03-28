using System.Diagnostics;
using System.Text.Json;
using WrapGod.Abstractions.Diagnostics;

namespace WrapGod.Cli;

internal sealed class DoctorHealthValidator
{
    internal delegate (int ExitCode, string StdOut, string StdErr) CommandRunner(string fileName, string arguments, string workingDirectory);
    private readonly CommandRunner _runCommand;

    public DoctorHealthValidator(CommandRunner? runCommand = null)
    {
        _runCommand = runCommand ?? RunCommand;
    }

    public Task<IReadOnlyList<WgDiagnosticV1>> ValidateAsync(DirectoryInfo repoRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = new List<WgDiagnosticV1>();
        if (!repoRoot.Exists)
        {
            diagnostics.Add(Diagnostic("WG7100", WgDiagnosticSeverity.Error, $"Doctor path does not exist: {repoRoot.FullName}", repoRoot, "Pass an existing repository path with --path (or run doctor from your repo root)."));
            return Task.FromResult<IReadOnlyList<WgDiagnosticV1>>(diagnostics);
        }

        ValidateTooling(repoRoot, diagnostics);
        ValidateSourceAndLockState(repoRoot, diagnostics);
        ValidateCiReadiness(repoRoot, diagnostics);

        return Task.FromResult<IReadOnlyList<WgDiagnosticV1>>(diagnostics);
    }

    private void ValidateTooling(DirectoryInfo repoRoot, List<WgDiagnosticV1> diagnostics)
    {
        var globalJson = new FileInfo(Path.Combine(repoRoot.FullName, "global.json"));
        if (!globalJson.Exists)
        {
            diagnostics.Add(Diagnostic("WG7101", WgDiagnosticSeverity.Warning, "global.json was not found. SDK pinning is recommended for deterministic builds.", repoRoot, "Add global.json at repo root with the desired SDK version (for example 10.0.104).", "https://learn.microsoft.com/dotnet/core/tools/global-json"));
        }

        var dotnetProbe = _runCommand("dotnet", "--version", repoRoot.FullName);
        if (dotnetProbe.ExitCode != 0)
        {
            diagnostics.Add(Diagnostic("WG7104", WgDiagnosticSeverity.Error, "dotnet CLI was not found or failed to run.", repoRoot, "Install .NET SDK 10+ and ensure `dotnet --version` works in your PATH.", properties: new Dictionary<string, object?> { ["stderr"] = dotnetProbe.StdErr }));
        }
    }

    private static void ValidateSourceAndLockState(DirectoryInfo repoRoot, List<WgDiagnosticV1> diagnostics)
    {
        var lockfile = new FileInfo(Path.Combine(repoRoot.FullName, "wrapgod.lock.json"));
        if (!lockfile.Exists)
        {
            diagnostics.Add(Diagnostic("WG7110", WgDiagnosticSeverity.Warning, "wrapgod.lock.json was not found; source/TFM resolution is not pinned yet.", repoRoot, "After #124 lands, generate and commit wrapgod.lock.json to pin source identity and TFM decisions.", "https://github.com/JerrettDavis/WrapGod/issues/124", tags: ["dependency:#124"]));
        }
        else
        {
            try
            {
                using var stream = lockfile.OpenRead();
                JsonDocument.Parse(stream);
            }
            catch (JsonException)
            {
                diagnostics.Add(Diagnostic("WG7111", WgDiagnosticSeverity.Error, "wrapgod.lock.json exists but is not valid JSON.", repoRoot, "Regenerate the lockfile or fix its JSON syntax."));
            }
        }

        var hasManifest = repoRoot.EnumerateFiles("*.wrapgod.json", SearchOption.AllDirectories).Any();
        var hasConfig = repoRoot.EnumerateFiles("*.wrapgod.config.json", SearchOption.AllDirectories).Any() || File.Exists(Path.Combine(repoRoot.FullName, "wrapgod.json"));
        if (!hasManifest && !hasConfig)
        {
            diagnostics.Add(Diagnostic("WG7112", WgDiagnosticSeverity.Warning, "No WrapGod manifest/config files were discovered.", repoRoot, "Add a manifest (*.wrapgod.json) or configuration file (wrapgod.json / *.wrapgod.config.json).", "https://github.com/JerrettDavis/WrapGod/issues/123", tags: ["dependency:#123"]));
        }
    }

    private static void ValidateCiReadiness(DirectoryInfo repoRoot, List<WgDiagnosticV1> diagnostics)
    {
        var workflowDir = new DirectoryInfo(Path.Combine(repoRoot.FullName, ".github", "workflows"));
        if (!workflowDir.Exists)
        {
            diagnostics.Add(Diagnostic("WG7120", WgDiagnosticSeverity.Warning, "No .github/workflows directory found; CI readiness checks cannot run.", repoRoot, "Add a CI workflow (for example .github/workflows/ci.yml) that restores, builds, and tests WrapGod."));
            return;
        }

        var workflowFiles = workflowDir.EnumerateFiles("*.yml", SearchOption.TopDirectoryOnly).Concat(workflowDir.EnumerateFiles("*.yaml", SearchOption.TopDirectoryOnly)).ToList();
        if (workflowFiles.Count == 0)
        {
            diagnostics.Add(Diagnostic("WG7121", WgDiagnosticSeverity.Warning, "Workflow directory exists but contains no YAML workflow files.", repoRoot, "Commit at least one workflow file such as ci.yml or pr-validation.yml."));
            return;
        }

        if (!workflowFiles.Any(f => string.Equals(f.Name, "ci.yml", StringComparison.OrdinalIgnoreCase)))
        {
            diagnostics.Add(Diagnostic("WG7122", WgDiagnosticSeverity.Warning, "ci.yml was not found in .github/workflows.", repoRoot, "Add .github/workflows/ci.yml as the primary continuous integration workflow."));
        }
    }

    private static WgDiagnosticV1 Diagnostic(string code, string severity, string message, DirectoryInfo repoRoot, string remediation, string? helpUri = null, Dictionary<string, object?>? properties = null, List<string>? tags = null)
    {
        properties ??= new Dictionary<string, object?>();
        properties["remediation"] = remediation;

        return new WgDiagnosticV1
        {
            Code = code,
            Severity = severity,
            Stage = WgDiagnosticStage.Cli,
            Category = "doctor",
            Message = message,
            HelpUri = helpUri,
            Tags = tags,
            Properties = properties,
            Source = new WgDiagnosticSource { Tool = "WrapGod", Component = "WrapGod.Cli.doctor" },
            Location = new WgDiagnosticLocation { Uri = repoRoot.FullName },
        };
    }

    private static (int ExitCode, string StdOut, string StdErr) RunCommand(string fileName, string arguments, string workingDirectory)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };

            process.Start();
            var stdOut = process.StandardOutput.ReadToEnd();
            var stdErr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stdOut, stdErr);
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }
}
