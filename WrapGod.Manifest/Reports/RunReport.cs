using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WrapGod.Manifest.Reports;

/// <summary>
/// Unified run report capturing the outcome of a WrapGod pipeline execution.
/// </summary>
public sealed class RunReport
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("sourcesResolved")]
    public List<ResolvedSource> SourcesResolved { get; set; } = new List<ResolvedSource>();

    [JsonPropertyName("typesExtracted")]
    public int TypesExtracted { get; set; }

    [JsonPropertyName("wrappersGenerated")]
    public int WrappersGenerated { get; set; }

    [JsonPropertyName("diagnosticsFound")]
    public int DiagnosticsFound { get; set; }

    [JsonPropertyName("fixesApplied")]
    public int FixesApplied { get; set; }

    [JsonPropertyName("diagnostics")]
    public List<ReportDiagnostic> Diagnostics { get; set; } = new List<ReportDiagnostic>();
}

/// <summary>
/// A source that was resolved during the run.
/// </summary>
public sealed class ResolvedSource
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("sourceType")]
    public string SourceType { get; set; } = string.Empty;
}

/// <summary>
/// A diagnostic entry in the run report.
/// </summary>
public sealed class ReportDiagnostic
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;
}

/// <summary>
/// Writes a <see cref="RunReport"/> as JSON or human-readable text.
/// </summary>
public static class RunReportWriter
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Serializes the run report to a JSON string.
    /// </summary>
    public static string SerializeJson(RunReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));
        return JsonSerializer.Serialize(report, s_jsonOptions);
    }

    /// <summary>
    /// Writes the run report to a JSON file.
    /// </summary>
    public static void WriteJsonToFile(RunReport report, string path)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));
        if (path is null) throw new ArgumentNullException(nameof(path));
        System.IO.File.WriteAllText(path, SerializeJson(report));
    }

    /// <summary>
    /// Formats the run report as human-readable text.
    /// </summary>
    public static string FormatText(RunReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));

        var sb = new StringBuilder();
        sb.AppendLine("WrapGod Run Report");
        sb.AppendLine(new string('-', 40));
        sb.AppendLine($"Timestamp:          {report.Timestamp:u}");
        sb.AppendLine($"Sources resolved:   {report.SourcesResolved.Count}");
        sb.AppendLine($"Types extracted:    {report.TypesExtracted}");
        sb.AppendLine($"Wrappers generated: {report.WrappersGenerated}");
        sb.AppendLine($"Diagnostics found:  {report.DiagnosticsFound}");
        sb.AppendLine($"Fixes applied:      {report.FixesApplied}");

        if (report.SourcesResolved.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Sources:");
            foreach (var source in report.SourcesResolved)
            {
                sb.AppendLine($"  {source.Name} {source.Version} ({source.SourceType})");
            }
        }

        if (report.Diagnostics.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Diagnostics:");
            foreach (var diag in report.Diagnostics)
            {
                sb.AppendLine($"  [{diag.Severity}] {diag.Code}: {diag.Message}");
                if (!string.IsNullOrEmpty(diag.File))
                {
                    sb.AppendLine($"         {diag.File}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Writes the run report as human-readable text to a file.
    /// </summary>
    public static void WriteTextToFile(RunReport report, string path)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));
        if (path is null) throw new ArgumentNullException(nameof(path));
        System.IO.File.WriteAllText(path, FormatText(report));
    }
}

/// <summary>
/// Reads and validates a <see cref="RunReport"/> from JSON.
/// </summary>
public static class RunReportReader
{
    /// <summary>
    /// Deserializes a run report from a JSON string.
    /// </summary>
    public static RunReport Deserialize(string json)
    {
        if (json is null) throw new ArgumentNullException(nameof(json));

        var report = JsonSerializer.Deserialize<RunReport>(json)
            ?? throw new InvalidOperationException("Failed to deserialize run report: result was null.");

        return report;
    }

    /// <summary>
    /// Reads and deserializes a run report from the specified file path.
    /// </summary>
    public static RunReport ReadFromFile(string path)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        var json = System.IO.File.ReadAllText(path);
        return Deserialize(json);
    }
}
