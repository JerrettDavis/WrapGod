using Microsoft.CodeAnalysis;
using WrapGod.Abstractions.Diagnostics;

namespace WrapGod.Analyzers;

public static class RoslynDiagnosticAdapter
{
    public static WgDiagnosticV1 ToWgDiagnosticV1(Diagnostic diagnostic, DateTime? timestampUtc = null)
    {
        var span = diagnostic.Location.GetLineSpan();
        var hasFile = span.Path is not null;
        var hasPosition = span.StartLinePosition.Line >= 0 && span.StartLinePosition.Character >= 0;

        return new WgDiagnosticV1
        {
            Schema = WgDiagnosticV1.SchemaId,
            Code = diagnostic.Id,
            Severity = MapSeverity(diagnostic.Severity),
            Stage = WgDiagnosticStage.Analyze,
            Category = "migration",
            Message = diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
            Source = new WgDiagnosticSource
            {
                Tool = "WrapGod",
                Component = "WrapGod.Analyzers",
            },
            Location = hasFile
                ? new WgDiagnosticLocation
                {
                    Uri = span.Path,
                    Line = hasPosition ? span.StartLinePosition.Line + 1 : null,
                    Column = hasPosition ? span.StartLinePosition.Character + 1 : null,
                    EndLine = hasPosition ? span.EndLinePosition.Line + 1 : null,
                    EndColumn = hasPosition ? span.EndLinePosition.Character + 1 : null,
                }
                : null,
            HelpUri = diagnostic.Descriptor.HelpLinkUri,
            TimestampUtc = timestampUtc ?? DateTime.UtcNow,
        };
    }

    private static string MapSeverity(DiagnosticSeverity severity)
        => severity switch
        {
            DiagnosticSeverity.Error => WgDiagnosticSeverity.Error,
            DiagnosticSeverity.Warning => WgDiagnosticSeverity.Warning,
            DiagnosticSeverity.Info => WgDiagnosticSeverity.Note,
            DiagnosticSeverity.Hidden => WgDiagnosticSeverity.None,
            _ => WgDiagnosticSeverity.Note,
        };
}
