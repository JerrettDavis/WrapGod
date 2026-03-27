using System.Text.Json;
using WrapGod.Abstractions.Config;

namespace WrapGod.Abstractions.Diagnostics;

public static class WgDiagnosticEmitter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static string EmitJson(IEnumerable<WgDiagnosticV1> diagnostics)
        => JsonSerializer.Serialize(diagnostics, JsonOptions);

    public static WgDiagnosticV1 FromConfigDiagnostic(ConfigDiagnostic diagnostic, DateTime? timestampUtc = null)
    {
        return new WgDiagnosticV1
        {
            Schema = WgDiagnosticV1.SchemaId,
            Code = diagnostic.Code,
            Severity = WgDiagnosticSeverity.Warning,
            Stage = WgDiagnosticStage.Config,
            Category = "config",
            Message = diagnostic.Message,
            Source = new WgDiagnosticSource
            {
                Tool = "WrapGod",
                Component = "WrapGod.Manifest.ConfigMergeEngine",
            },
            Location = string.IsNullOrWhiteSpace(diagnostic.Target)
                ? null
                : new WgDiagnosticLocation { Symbol = diagnostic.Target },
            TimestampUtc = timestampUtc ?? DateTime.UtcNow,
        };
    }
}
