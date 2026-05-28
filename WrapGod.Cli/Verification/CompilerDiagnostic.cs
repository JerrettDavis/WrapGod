namespace WrapGod.Cli.Verification;

/// <summary>The severity of a compiler diagnostic.</summary>
internal enum DiagnosticSeverity
{
    Error,
    Warning,
    Info,
}

/// <summary>
/// A single compiler diagnostic parsed from <c>dotnet build</c> output.
/// </summary>
internal sealed class CompilerDiagnostic
{
    /// <summary>
    /// The absolute (or relative) path to the file that produced the diagnostic,
    /// or <see langword="null"/> when the diagnostic has no file context
    /// (e.g. MSBuild-level errors, linker errors).
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>1-based line number, or <c>0</c> when unavailable.</summary>
    public int Line { get; init; }

    /// <summary>1-based column number, or <c>0</c> when unavailable.</summary>
    public int Column { get; init; }

    /// <summary>Error, Warning, or Info.</summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>Diagnostic code such as <c>CS0103</c>.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable diagnostic message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>The original unparsed line for debugging purposes.</summary>
    public string RawLine { get; init; } = string.Empty;
}
