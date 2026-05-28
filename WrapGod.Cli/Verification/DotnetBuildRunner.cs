using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace WrapGod.Cli.Verification;

/// <summary>
/// Real implementation of <see cref="IBuildRunner"/> that spawns <c>dotnet build</c>
/// as a child process and captures its combined stdout + stderr.
/// </summary>
/// <remarks>
/// Excluded from code coverage because it requires a real <c>dotnet</c> SDK on PATH
/// and spawns an out-of-process build. Unit tests inject <c>StubBuildRunner</c> instead.
/// Integration coverage is provided by the CI pipeline's own build step.
/// </remarks>
[ExcludeFromCodeCoverage]
internal sealed class DotnetBuildRunner : IBuildRunner
{
    public async Task<BuildResult> RunAsync(
        string projectDir,
        string buildConfig,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectDir);
        ArgumentNullException.ThrowIfNull(buildConfig);

        var output = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName               = "dotnet",
            // --nologo suppresses the Microsoft banner; --no-restore avoids implicit restore
            // noise; -p:RunAnalyzers=false keeps output clean; -p:WarningLevel=4 maximises
            // diagnostic emission so we catch as much as possible.
            Arguments              = $"build --nologo --no-restore -c {buildConfig} -p:WarningLevel=4 -p:RunAnalyzers=false",
            WorkingDirectory       = projectDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        Process? process;
        try
        {
            process = Process.Start(psi);
        }
        catch (Exception ex) when (ex is FileNotFoundException or Win32Exception or InvalidOperationException)
        {
            return BuildResult.LaunchFailed($"dotnet build could not be started: {ex.Message}");
        }

        if (process is null)
            return BuildResult.LaunchFailed("dotnet build process failed to start (returned null).");

        using (process)
        {
            // Read output asynchronously to avoid deadlocks on large output.
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            output.Append(await stdoutTask.ConfigureAwait(false));
            output.Append(await stderrTask.ConfigureAwait(false));

            return BuildResult.Completed(output.ToString(), process.ExitCode);
        }
    }
}
