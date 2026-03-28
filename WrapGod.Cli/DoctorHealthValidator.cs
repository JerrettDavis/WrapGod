using System.Diagnostics;
using System.Text.Json;
using WrapGod.Abstractions.Diagnostics;

namespace WrapGod.Cli;

public sealed class DoctorHealthValidator
{
    public delegate (int ExitCode, string StdOut, string StdErr) CommandRunner(string fileName, string arguments, string workingDirectory);
    private readonly CommandRunner _runCommand;

    public DoctorHealthValidator(CommandRunner? runCommand = null) => _runCommand = runCommand ?? RunCommand;

    public Task<IReadOnlyList<WgDiagnosticV1>> ValidateAsync(DirectoryInfo repoRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<WgDiagnosticV1>();

        if (!repoRoot.Exists)
        {
            diagnostics.Add(Diag("WG7100", WgDiagnosticSeverity.Error, $"Project root does not exist: {repoRoot.FullName}", repoRoot, "Pass a valid repository path to --path."));
            return Task.FromResult<IReadOnlyList<WgDiagnosticV1>>(diagnostics);
        }

        var probe = _runCommand("dotnet", "--version", repoRoot.FullName);
        if (probe.ExitCode != 0)
            diagnostics.Add(Diag("WG7104", WgDiagnosticSeverity.Error, "dotnet CLI was not found or failed to run.", repoRoot, "Install .NET SDK 10+ and ensure `dotnet --version` works.", properties: new() { ["stderr"] = probe.StdErr }));

        var lockfile = new FileInfo(Path.Combine(repoRoot.FullName, "wrapgod.lock.json"));
        if (!lockfile.Exists)
            diagnostics.Add(Diag("WG7110", WgDiagnosticSeverity.Warning, "wrapgod.lock.json was not found; source/TFM resolution is not pinned yet.", repoRoot, "After #124 lands, generate and commit wrapgod.lock.json.", "https://github.com/JerrettDavis/WrapGod/issues/124", new() { "dependency:#124" }));
        else
        {
            try { using var s = lockfile.OpenRead(); JsonDocument.Parse(s); }
            catch (JsonException) { diagnostics.Add(Diag("WG7111", WgDiagnosticSeverity.Error, "wrapgod.lock.json exists but is not valid JSON.", repoRoot, "Regenerate the lockfile or fix JSON syntax.")); }
        }

        var hasManifest = repoRoot.EnumerateFiles("*.wrapgod.json", SearchOption.AllDirectories).Any();
        var hasConfig = repoRoot.EnumerateFiles("*.wrapgod.config.json", SearchOption.AllDirectories).Any() || File.Exists(Path.Combine(repoRoot.FullName, "wrapgod.json"));
        if (!hasManifest && !hasConfig)
            diagnostics.Add(Diag("WG7112", WgDiagnosticSeverity.Warning, "No WrapGod manifest/config files were discovered.", repoRoot, "Add *.wrapgod.json or wrapgod.json / *.wrapgod.config.json.", "https://github.com/JerrettDavis/WrapGod/issues/123", new() { "dependency:#123" }));

        var wfDir = new DirectoryInfo(Path.Combine(repoRoot.FullName, ".github", "workflows"));
        if (!wfDir.Exists) diagnostics.Add(Diag("WG7120", WgDiagnosticSeverity.Warning, "No .github/workflows directory found.", repoRoot, "Add CI workflow(s) with restore/build/test."));

        return Task.FromResult<IReadOnlyList<WgDiagnosticV1>>(diagnostics);
    }

    private static WgDiagnosticV1 Diag(string code, string sev, string msg, DirectoryInfo root, string remediation, string? helpUri = null, List<string>? tags = null, Dictionary<string, object?>? properties = null)
    {
        properties ??= new();
        properties["remediation"] = remediation;
        return new WgDiagnosticV1
        {
            Code = code, Severity = sev, Stage = WgDiagnosticStage.Cli, Category = "doctor", Message = msg, HelpUri = helpUri, Tags = tags,
            Properties = properties, Source = new() { Tool = "WrapGod", Component = "WrapGod.Cli.doctor" }, Location = new() { Uri = root.FullName }
        };
    }

    private static (int ExitCode, string StdOut, string StdErr) RunCommand(string fileName, string arguments, string workingDirectory)
    {
        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo { FileName = fileName, Arguments = arguments, WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true }
            };
            p.Start(); var so = p.StandardOutput.ReadToEnd(); var se = p.StandardError.ReadToEnd(); p.WaitForExit(); return (p.ExitCode, so, se);
        }
        catch (Exception ex) { return (-1, string.Empty, ex.Message); }
    }
}
