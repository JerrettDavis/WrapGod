using System;
using System.Collections.Generic;
using System.Linq;
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

    public static string EmitSarif(IEnumerable<WgDiagnosticV1> diagnostics)
    {
        var materialized = diagnostics?.ToList() ?? new List<WgDiagnosticV1>();

        var rules = materialized
            .Where(d => !string.IsNullOrWhiteSpace(d.Code))
            .GroupBy(d => d.Code, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(BuildRuleDescriptor)
            .ToList();

        var results = materialized
            .Select(BuildResult)
            .ToList();

        var sarif = new Dictionary<string, object?>
        {
            ["version"] = "2.1.0",
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["runs"] = new object[]
            {
                new
                {
                    tool = new
                    {
                        driver = new
                        {
                            name = "WrapGod",
                            rules,
                        },
                    },
                    results,
                },
            },
        };

        return JsonSerializer.Serialize(sarif, JsonOptions);
    }

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

    private static object BuildRuleDescriptor(IGrouping<string, WgDiagnosticV1> group)
    {
        var category = group.Select(x => x.Category).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        var helpUri = group.Select(x => x.HelpUri).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        var fullDescription = group.Select(x => x.Message).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        var tags = group
            .Where(x => x.Tags is { Count: > 0 })
            .SelectMany(x => x.Tags!)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToArray();

        return new
        {
            id = group.Key,
            name = group.Key,
            shortDescription = new { text = $"WrapGod rule {group.Key}" },
            fullDescription = string.IsNullOrWhiteSpace(fullDescription)
                ? null
                : new { text = fullDescription },
            helpUri,
            defaultConfiguration = new
            {
                level = MapSeverity(ResolveDefaultSeverity(group)),
            },
            properties = new
            {
                category,
                tags = tags.Length == 0 ? null : tags,
            },
        };
    }

    private static object BuildResult(WgDiagnosticV1 diagnostic)
    {
        return new
        {
            ruleId = diagnostic.Code,
            level = MapSeverity(diagnostic.Severity),
            message = new { text = diagnostic.Message },
            locations = BuildLocations(diagnostic.Location),
            relatedLocations = BuildRelatedLocations(diagnostic.RelatedLocations),
            fingerprints = string.IsNullOrWhiteSpace(diagnostic.Fingerprint)
                ? null
                : new Dictionary<string, string> { ["wrapgodFingerprint"] = diagnostic.Fingerprint },
            suppressions = BuildSuppressions(diagnostic.Suppression),
            properties = BuildResultProperties(diagnostic),
        };
    }

    private static object[]? BuildLocations(WgDiagnosticLocation? location)
    {
        if (location is null)
        {
            return null;
        }

        return new[] { BuildSarifLocation(location) };
    }

    private static object[]? BuildRelatedLocations(IReadOnlyCollection<WgDiagnosticLocation>? locations)
    {
        if (locations is null || locations.Count == 0)
        {
            return null;
        }

        return locations.Select(BuildSarifLocation).ToArray();
    }

    private static object BuildSarifLocation(WgDiagnosticLocation location)
    {
        return new
        {
            physicalLocation = new
            {
                artifactLocation = string.IsNullOrWhiteSpace(location.Uri) ? null : new { uri = location.Uri },
                region = BuildRegion(location),
            },
            logicalLocations = string.IsNullOrWhiteSpace(location.Symbol)
                ? null
                : new[] { new { name = location.Symbol } },
        };
    }

    private static object? BuildRegion(WgDiagnosticLocation location)
    {
        if (!location.Line.HasValue && !location.Column.HasValue && !location.EndLine.HasValue && !location.EndColumn.HasValue)
        {
            return null;
        }

        return new
        {
            startLine = location.Line,
            startColumn = location.Column,
            endLine = location.EndLine,
            endColumn = location.EndColumn,
        };
    }

    private static object[]? BuildSuppressions(WgDiagnosticSuppression? suppression)
    {
        if (suppression is null)
        {
            return null;
        }

        return new[]
        {
            new
            {
                kind = MapSuppressionKind(suppression.Kind),
                justification = suppression.Justification,
            },
        };
    }

    private static object? BuildResultProperties(WgDiagnosticV1 diagnostic)
    {
        var baseProperties = new Dictionary<string, object?>
        {
            ["schema"] = diagnostic.Schema,
            ["stage"] = diagnostic.Stage,
            ["category"] = diagnostic.Category,
            ["source.tool"] = diagnostic.Source.Tool,
            ["source.component"] = diagnostic.Source.Component,
            ["source.version"] = diagnostic.Source.Version,
            ["timestampUtc"] = diagnostic.TimestampUtc.ToUniversalTime().ToString("O"),
            ["suppression.source"] = diagnostic.Suppression?.Source,
        };

        if (diagnostic.Tags is { Count: > 0 })
        {
            baseProperties["tags"] = diagnostic.Tags;
        }

        if (diagnostic.Properties is not null)
        {
            foreach (var pair in diagnostic.Properties)
            {
                baseProperties[pair.Key] = pair.Value;
            }
        }

        return baseProperties;
    }

    private static string ResolveDefaultSeverity(IEnumerable<WgDiagnosticV1> diagnostics)
    {
        var severities = diagnostics.Select(d => d.Severity).ToArray();
        if (severities.Any(s => string.Equals(s, WgDiagnosticSeverity.Error, StringComparison.OrdinalIgnoreCase)))
        {
            return WgDiagnosticSeverity.Error;
        }

        if (severities.Any(s => string.Equals(s, WgDiagnosticSeverity.Warning, StringComparison.OrdinalIgnoreCase)))
        {
            return WgDiagnosticSeverity.Warning;
        }

        if (severities.Any(s => string.Equals(s, WgDiagnosticSeverity.Note, StringComparison.OrdinalIgnoreCase)))
        {
            return WgDiagnosticSeverity.Note;
        }

        return WgDiagnosticSeverity.None;
    }

    private static string MapSeverity(string? severity)
    {
        if (string.Equals(severity, WgDiagnosticSeverity.Error, StringComparison.OrdinalIgnoreCase))
        {
            return "error";
        }

        if (string.Equals(severity, WgDiagnosticSeverity.Warning, StringComparison.OrdinalIgnoreCase))
        {
            return "warning";
        }

        if (string.Equals(severity, WgDiagnosticSeverity.Note, StringComparison.OrdinalIgnoreCase))
        {
            return "note";
        }

        return "none";
    }

    private static string MapSuppressionKind(string? suppressionKind)
    {
        if (string.Equals(suppressionKind, "pragma", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(suppressionKind, "editorconfig", StringComparison.OrdinalIgnoreCase))
        {
            return "inSource";
        }

        return "external";
    }
}
