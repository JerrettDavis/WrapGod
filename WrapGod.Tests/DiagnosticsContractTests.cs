using System.Text.Json;
using Json.Schema;
using Microsoft.CodeAnalysis;
using WrapGod.Abstractions.Config;
using WrapGod.Abstractions.Diagnostics;
using WrapGod.Analyzers;

namespace WrapGod.Tests;

public sealed class DiagnosticsContractTests
{
    private static readonly DateTime FixedUtc =
        new(2026, 03, 27, 22, 0, 0, DateTimeKind.Utc);

    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));

    private static JsonDocument EmitSampleJson()
    {
        var analyzerDescriptor = new DiagnosticDescriptor(
            "WG2001",
            "Direct type usage",
            "Type 'Vendor.Client' has a generated wrapper interface; use 'IClient' instead",
            "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: "https://wrapgod.dev/docs/analyzers/WG2001");

        var analyzerDiagnostic = Diagnostic.Create(analyzerDescriptor, Location.None);
        var wg2001 = RoslynDiagnosticAdapter.ToWgDiagnosticV1(analyzerDiagnostic, FixedUtc);

        var configDiagnostic = new ConfigDiagnostic
        {
            Code = "WG6001",
            Message = "Type include conflict between JSON and attributes.",
            Target = "Vendor.Client",
        };

        var wg6001 = WgDiagnosticEmitter.FromConfigDiagnostic(configDiagnostic, FixedUtc);

        var json = WgDiagnosticEmitter.EmitJson(new[] { wg2001, wg6001 });
        return JsonDocument.Parse(json);
    }

    [Fact]
    public void JsonOutput_ValidatesAgainstRfc0054Shape()
    {
        using var doc = EmitSampleJson();
        var schemaText = File.ReadAllText(Path.Combine(RepoRoot, "schemas", "wg.diagnostic.v1.schema.json"));
        var schema = JsonSchema.FromText(schemaText);

        var diagnostics = doc.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, diagnostics.Length);

        foreach (var diagnostic in diagnostics)
        {
            var result = schema.Evaluate(diagnostic, new EvaluationOptions { OutputFormat = OutputFormat.Flag });
            Assert.True(result.IsValid);
        }
    }

    [Fact]
    public void ExistingProducers_NormalizeIntoCanonicalModel()
    {
        using var doc = EmitSampleJson();
        var diagnostics = doc.RootElement.EnumerateArray().ToArray();

        var wg2001 = diagnostics.Single(d => d.GetProperty("code").GetString() == "WG2001");
        var wg6001 = diagnostics.Single(d => d.GetProperty("code").GetString() == "WG6001");

        Assert.Equal("wg.diagnostic.v1", wg2001.GetProperty("schema").GetString());
        Assert.Equal("analyze", wg2001.GetProperty("stage").GetString());
        Assert.Equal("warning", wg2001.GetProperty("severity").GetString());

        Assert.Equal("wg.diagnostic.v1", wg6001.GetProperty("schema").GetString());
        Assert.Equal("config", wg6001.GetProperty("stage").GetString());
        Assert.Equal("warning", wg6001.GetProperty("severity").GetString());
    }

    [Fact]
    public void SeveritySerialization_UsesLowercaseContractValues()
    {
        var payload = new[]
        {
            new WgDiagnosticV1
            {
                Code = "WG6002",
                Severity = WgDiagnosticSeverity.Error,
                Stage = WgDiagnosticStage.Config,
                Message = "Type targetName conflict.",
                Source = new WgDiagnosticSource { Tool = "WrapGod", Component = "tests" },
                TimestampUtc = FixedUtc,
            },
            new WgDiagnosticV1
            {
                Code = "WG2002",
                Severity = WgDiagnosticSeverity.Note,
                Stage = WgDiagnosticStage.Analyze,
                Message = "Method call can be migrated through facade.",
                Source = new WgDiagnosticSource { Tool = "WrapGod", Component = "tests" },
                TimestampUtc = FixedUtc,
            },
        };

        var json = WgDiagnosticEmitter.EmitJson(payload);
        Assert.Contains("\"severity\": \"error\"", json, StringComparison.Ordinal);
        Assert.Contains("\"severity\": \"note\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SarifOutput_UsesSarif210WithStableWgRuleCatalogProjection()
    {
        var payload = new[]
        {
            new WgDiagnosticV1
            {
                Code = "WG6001",
                Severity = WgDiagnosticSeverity.Warning,
                Stage = WgDiagnosticStage.Config,
                Category = "config",
                Message = "Config include conflict.",
                HelpUri = "https://wrapgod.dev/docs/config/WG6001",
                Source = new WgDiagnosticSource { Tool = "WrapGod", Component = "tests" },
                TimestampUtc = FixedUtc,
            },
            new WgDiagnosticV1
            {
                Code = "WG2002",
                Severity = WgDiagnosticSeverity.Error,
                Stage = WgDiagnosticStage.Analyze,
                Category = "migration",
                Message = "Facade migration required.",
                HelpUri = "https://wrapgod.dev/docs/analyzers/WG2002",
                Source = new WgDiagnosticSource { Tool = "WrapGod", Component = "tests" },
                TimestampUtc = FixedUtc,
            },
            new WgDiagnosticV1
            {
                Code = "WG2002",
                Severity = WgDiagnosticSeverity.Warning,
                Stage = WgDiagnosticStage.Analyze,
                Category = "migration",
                Message = "Additional WG2002 occurrence.",
                Source = new WgDiagnosticSource { Tool = "WrapGod", Component = "tests" },
                TimestampUtc = FixedUtc,
            },
        };

        using var doc = JsonDocument.Parse(WgDiagnosticEmitter.EmitSarif(payload));
        var root = doc.RootElement;

        Assert.Equal("2.1.0", root.GetProperty("version").GetString());
        Assert.Equal("https://json.schemastore.org/sarif-2.1.0.json", root.GetProperty("$schema").GetString());

        var run = root.GetProperty("runs")[0];
        Assert.Equal("WrapGod", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());

        var rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules").EnumerateArray().ToArray();
        Assert.Equal(2, rules.Length);
        Assert.Equal("WG2002", rules[0].GetProperty("id").GetString());
        Assert.Equal("WG6001", rules[1].GetProperty("id").GetString());
        Assert.Equal("error", rules[0].GetProperty("defaultConfiguration").GetProperty("level").GetString());
        Assert.Equal("warning", rules[1].GetProperty("defaultConfiguration").GetProperty("level").GetString());

        var results = run.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(3, results.Length);
        Assert.Equal("WG6001", results[0].GetProperty("ruleId").GetString());
        Assert.Equal("WG2002", results[1].GetProperty("ruleId").GetString());
    }

    [Fact]
    public void SarifMapping_IncludesLocationsRelatedLocationsFingerprintsAndSuppressions()
    {
        var payload = new[]
        {
            new WgDiagnosticV1
            {
                Code = "WG2001",
                Severity = WgDiagnosticSeverity.Warning,
                Stage = WgDiagnosticStage.Analyze,
                Category = "migration",
                Message = "Direct type usage detected.",
                Source = new WgDiagnosticSource { Tool = "WrapGod", Component = "tests" },
                Location = new WgDiagnosticLocation
                {
                    Uri = "src/Program.cs",
                    Line = 42,
                    Column = 5,
                    EndLine = 42,
                    EndColumn = 18,
                },
                RelatedLocations =
                [
                    new WgDiagnosticLocation
                    {
                        Symbol = "Vendor.Client",
                    },
                ],
                Fingerprint = "WG2001:src/Program.cs:42:5",
                Suppression = new WgDiagnosticSuppression
                {
                    Kind = "pragma",
                    Justification = "Intentional migration staging",
                    Source = "#pragma warning disable WG2001",
                },
                TimestampUtc = FixedUtc,
            },
        };

        using var doc = JsonDocument.Parse(WgDiagnosticEmitter.EmitSarif(payload));
        var result = doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

        Assert.Equal("WG2001", result.GetProperty("ruleId").GetString());
        Assert.Equal("warning", result.GetProperty("level").GetString());
        Assert.Equal("src/Program.cs", result.GetProperty("locations")[0]
            .GetProperty("physicalLocation")
            .GetProperty("artifactLocation")
            .GetProperty("uri")
            .GetString());

        Assert.Equal("Vendor.Client", result.GetProperty("relatedLocations")[0]
            .GetProperty("logicalLocations")[0]
            .GetProperty("name")
            .GetString());

        Assert.Equal("WG2001:src/Program.cs:42:5", result.GetProperty("fingerprints")
            .GetProperty("wrapgodFingerprint")
            .GetString());

        Assert.Equal("inSource", result.GetProperty("suppressions")[0].GetProperty("kind").GetString());
        Assert.Equal("Intentional migration staging", result.GetProperty("suppressions")[0].GetProperty("justification").GetString());
    }
}
