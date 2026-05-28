using System.Text.RegularExpressions;

namespace WrapGod.Cli.Verification;

/// <summary>
/// Parses <c>dotnet build</c> / MSBuild / Roslyn output into structured
/// <see cref="CompilerDiagnostic"/> records.
/// </summary>
/// <remarks>
/// <para>
/// The canonical diagnostic line format produced by Roslyn/MSBuild is:
/// <code>
/// /path/to/File.cs(42,5): error CS0103: The name 'X' does not exist in the current context [/path/to/proj.csproj]
/// </code>
/// This parser handles both Unix-style (<c>/abs/path</c>) and Windows-style
/// (<c>C:\abs\path</c> or relative) file paths.
/// </para>
/// <para>
/// Lines that do not match the pattern (e.g. MSBuild progress messages) are silently
/// ignored — they are available to the caller via the raw output string if needed.
/// </para>
/// </remarks>
internal static partial class DiagnosticParser
{
    // Pattern: <file>(<line>,<col>): <severity> <code>: <message> [optional project ref]
    // Handles both absolute Unix paths and Windows drive-letter paths.
    // Code prefix is up to 6 letters to cover MSBuild / NETSDK / NuGet codes
    // (e.g. NETSDK1138, NU1605, MSB3001) alongside the standard CS#### / VBNC#### / IDE####.
    [GeneratedRegex(
        @"^(?<file>.+?)\((?<line>\d+),(?<col>\d+)\):\s+(?<sev>error|warning|info)\s+(?<code>[A-Za-z]{1,6}\d+):\s+(?<msg>.+?)(?:\s+\[.*?\])?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticLineRegex();

    /// <summary>
    /// Parses the combined stdout+stderr output of a <c>dotnet build</c> invocation and
    /// returns all lines that match the diagnostic pattern.
    /// </summary>
    /// <param name="buildOutput">Combined stdout + stderr from the build.</param>
    /// <returns>
    /// Sequence of parsed diagnostics. Lines that do not match are silently skipped.
    /// </returns>
    public static IReadOnlyList<CompilerDiagnostic> Parse(string buildOutput)
    {
        ArgumentNullException.ThrowIfNull(buildOutput);

        var results = new List<CompilerDiagnostic>();
        var regex   = DiagnosticLineRegex();

        foreach (var rawLine in buildOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var m = regex.Match(line);
            if (!m.Success)
                continue;

            var severity = m.Groups["sev"].Value.ToLowerInvariant() switch
            {
                "error"   => DiagnosticSeverity.Error,
                "warning" => DiagnosticSeverity.Warning,
                _         => DiagnosticSeverity.Info,
            };

            _ = int.TryParse(m.Groups["line"].Value, out var lineNo);
            _ = int.TryParse(m.Groups["col"].Value,  out var colNo);

            results.Add(new CompilerDiagnostic
            {
                FilePath = m.Groups["file"].Value,
                Line     = lineNo,
                Column   = colNo,
                Severity = severity,
                Code     = m.Groups["code"].Value,
                Message  = m.Groups["msg"].Value.Trim(),
                RawLine  = line,
            });
        }

        return results;
    }
}
