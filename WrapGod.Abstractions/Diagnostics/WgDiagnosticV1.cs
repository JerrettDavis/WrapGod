using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WrapGod.Abstractions.Diagnostics;

public sealed class WgDiagnosticV1
{
    public const string SchemaId = "wg.diagnostic.v1";

    [JsonPropertyName("schema")]
    public string Schema { get; set; } = SchemaId;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = WgDiagnosticSeverity.Warning;

    [JsonPropertyName("stage")]
    public string Stage { get; set; } = WgDiagnosticStage.Analyze;

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public WgDiagnosticSource Source { get; set; } = new();

    [JsonPropertyName("location")]
    public WgDiagnosticLocation? Location { get; set; }

    [JsonPropertyName("relatedLocations")]
    public List<WgDiagnosticLocation>? RelatedLocations { get; set; }

    [JsonPropertyName("helpUri")]
    public string? HelpUri { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; set; }

    [JsonPropertyName("properties")]
    public Dictionary<string, object?>? Properties { get; set; }

    [JsonPropertyName("suppression")]
    public WgDiagnosticSuppression? Suppression { get; set; }

    [JsonPropertyName("timestampUtc")]
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public static class WgDiagnosticSeverity
{
    public const string Error = "error";
    public const string Warning = "warning";
    public const string Note = "note";
    public const string None = "none";
}

public static class WgDiagnosticStage
{
    public const string Extract = "extract";
    public const string Plan = "plan";
    public const string Generate = "generate";
    public const string Analyze = "analyze";
    public const string Fix = "fix";
    public const string Cli = "cli";
    public const string Config = "config";
}

public sealed class WgDiagnosticSource
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; } = "WrapGod";

    [JsonPropertyName("component")]
    public string? Component { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public sealed class WgDiagnosticLocation
{
    [JsonPropertyName("uri")]
    public string? Uri { get; set; }

    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("column")]
    public int? Column { get; set; }

    [JsonPropertyName("endLine")]
    public int? EndLine { get; set; }

    [JsonPropertyName("endColumn")]
    public int? EndColumn { get; set; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }
}

public sealed class WgDiagnosticSuppression
{
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("justification")]
    public string? Justification { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }
}
