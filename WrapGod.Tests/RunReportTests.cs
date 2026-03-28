using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Manifest.Reports;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Unified run report contract")]
public sealed class RunReportTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static RunReport CreateSampleReport() => new()
    {
        SchemaVersion = "1.0",
        Timestamp = new DateTimeOffset(2026, 3, 27, 12, 0, 0, TimeSpan.Zero),
        TypesExtracted = 15,
        WrappersGenerated = 12,
        DiagnosticsFound = 3,
        FixesApplied = 2,
        SourcesResolved =
        [
            new ResolvedSource
            {
                Name = "Newtonsoft.Json",
                Version = "13.0.3",
                SourceType = "nuget"
            },
            new ResolvedSource
            {
                Name = "Serilog",
                Version = "4.0.0",
                SourceType = "nuget"
            }
        ],
        Diagnostics =
        [
            new ReportDiagnostic
            {
                Code = "WG2001",
                Severity = "Warning",
                Message = "Direct usage of JsonConvert detected",
                File = "Services/DataService.cs"
            }
        ]
    };

    [Scenario("JSON roundtrip preserves all data")]
    [Fact]
    public Task JsonRoundtrip() =>
        Given("a sample run report serialized and deserialized", () =>
        {
            var original = CreateSampleReport();
            var json = RunReportWriter.SerializeJson(original);
            var deserialized = RunReportReader.Deserialize(json);
            return (Original: original, Roundtrip: deserialized, Json: json);
        })
        .Then("schema version is preserved", r => r.Roundtrip.SchemaVersion == "1.0")
        .And("timestamp is preserved", r => r.Roundtrip.Timestamp == r.Original.Timestamp)
        .And("types extracted is preserved", r => r.Roundtrip.TypesExtracted == 15)
        .And("wrappers generated is preserved", r => r.Roundtrip.WrappersGenerated == 12)
        .And("diagnostics found is preserved", r => r.Roundtrip.DiagnosticsFound == 3)
        .And("fixes applied is preserved", r => r.Roundtrip.FixesApplied == 2)
        .And("sources count is preserved", r => r.Roundtrip.SourcesResolved.Count == 2)
        .And("first source name is preserved", r =>
            r.Roundtrip.SourcesResolved[0].Name == "Newtonsoft.Json")
        .And("diagnostics list is preserved", r => r.Roundtrip.Diagnostics.Count == 1)
        .And("diagnostic code is preserved", r =>
            r.Roundtrip.Diagnostics[0].Code == "WG2001")
        .AssertPassed();

    [Scenario("Text format contains all sections")]
    [Fact]
    public Task TextFormat() =>
        Given("a sample run report formatted as text", () =>
        {
            var report = CreateSampleReport();
            return RunReportWriter.FormatText(report);
        })
        .Then("contains header", text => text.Contains("WrapGod Run Report"))
        .And("contains types extracted", text => text.Contains("Types extracted:    15"))
        .And("contains wrappers generated", text => text.Contains("Wrappers generated: 12"))
        .And("contains source name", text => text.Contains("Newtonsoft.Json"))
        .And("contains diagnostic code", text => text.Contains("WG2001"))
        .AssertPassed();

    [Scenario("Empty report serializes with defaults")]
    [Fact]
    public Task EmptyReport() =>
        Given("an empty run report", () =>
        {
            var report = new RunReport();
            var json = RunReportWriter.SerializeJson(report);
            return RunReportReader.Deserialize(json);
        })
        .Then("types extracted defaults to 0", r => r.TypesExtracted == 0)
        .And("wrappers generated defaults to 0", r => r.WrappersGenerated == 0)
        .And("diagnostics found defaults to 0", r => r.DiagnosticsFound == 0)
        .And("fixes applied defaults to 0", r => r.FixesApplied == 0)
        .And("sources list is empty", r => r.SourcesResolved.Count == 0)
        .And("diagnostics list is empty", r => r.Diagnostics.Count == 0)
        .AssertPassed();

    [Scenario("File roundtrip via temp file")]
    [Fact]
    public Task FileRoundtrip() =>
        Given("a report written to and read from a temp file", () =>
        {
            var original = CreateSampleReport();
            var path = Path.GetTempFileName();
            try
            {
                RunReportWriter.WriteJsonToFile(original, path);
                var loaded = RunReportReader.ReadFromFile(path);
                return (Original: original, Loaded: loaded);
            }
            finally
            {
                File.Delete(path);
            }
        })
        .Then("types extracted matches", r => r.Loaded.TypesExtracted == r.Original.TypesExtracted)
        .And("sources count matches", r =>
            r.Loaded.SourcesResolved.Count == r.Original.SourcesResolved.Count)
        .AssertPassed();

    [Scenario("Null arguments throw ArgumentNullException")]
    [Fact]
    public Task NullGuards() =>
        Given("null argument scenarios", () => true)
        .Then("SerializeJson throws on null", _ =>
        {
            try { RunReportWriter.SerializeJson(null!); return false; }
            catch (ArgumentNullException) { return true; }
        })
        .And("WriteJsonToFile throws on null report", _ =>
        {
            try { RunReportWriter.WriteJsonToFile(null!, "test.json"); return false; }
            catch (ArgumentNullException) { return true; }
        })
        .And("WriteJsonToFile throws on null path", _ =>
        {
            try { RunReportWriter.WriteJsonToFile(new RunReport(), null!); return false; }
            catch (ArgumentNullException) { return true; }
        })
        .And("FormatText throws on null", _ =>
        {
            try { RunReportWriter.FormatText(null!); return false; }
            catch (ArgumentNullException) { return true; }
        })
        .And("Deserialize throws on null", _ =>
        {
            try { RunReportReader.Deserialize(null!); return false; }
            catch (ArgumentNullException) { return true; }
        })
        .And("ReadFromFile throws on null", _ =>
        {
            try { RunReportReader.ReadFromFile(null!); return false; }
            catch (ArgumentNullException) { return true; }
        })
        .AssertPassed();
}
