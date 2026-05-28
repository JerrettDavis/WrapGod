namespace WrapGod.Cli.Verification;

/// <summary>
/// Abstraction over the build invocation step so tests can inject a stub without
/// spawning a real <c>dotnet build</c> process.
/// </summary>
internal interface IBuildRunner
{
    /// <summary>
    /// Invokes a build against <paramref name="projectDir"/>.
    /// </summary>
    /// <param name="projectDir">The directory that contains the <c>.csproj</c> to build.</param>
    /// <param name="buildConfig">Build configuration, e.g. <c>Debug</c> or <c>Release</c>.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A <see cref="BuildResult"/> containing the combined stdout+stderr output and the
    /// process exit code, or a failure result if the build could not be invoked.
    /// </returns>
    Task<BuildResult> RunAsync(
        string projectDir,
        string buildConfig,
        CancellationToken cancellationToken = default);
}

/// <summary>Represents the outcome of a build invocation.</summary>
internal sealed class BuildResult
{
    /// <summary>Whether <c>dotnet build</c> was successfully launched (independent of exit code).</summary>
    public bool Launched { get; init; }

    /// <summary>
    /// When <see cref="Launched"/> is <see langword="false"/>, contains the reason the build
    /// could not be started (e.g. "dotnet not found on PATH").
    /// </summary>
    public string? LaunchError { get; init; }

    /// <summary>Combined stdout + stderr output captured from the build process.</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>The process exit code (0 = success, non-zero = build errors).</summary>
    public int ExitCode { get; init; }

    /// <summary>Creates a result for a failed launch (dotnet not found, etc.).</summary>
    public static BuildResult LaunchFailed(string error) =>
        new() { Launched = false, LaunchError = error };

    /// <summary>Creates a result for a completed build invocation.</summary>
    public static BuildResult Completed(string output, int exitCode) =>
        new() { Launched = true, Output = output, ExitCode = exitCode };
}
