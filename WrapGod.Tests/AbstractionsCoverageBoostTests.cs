using TinyBDD;
using TinyBDD.Xunit;
using WrapGod.Abstractions.Config;
using WrapGod.Abstractions.Diagnostics;
using Xunit.Abstractions;

namespace WrapGod.Tests;

[Feature("Abstractions coverage boost: config models, attributes, diagnostics")]
public sealed class AbstractionsCoverageBoostTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ═══════════════════════════════════════════════════════════════════
    //  Config model properties and defaults
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("WrapGodConfig has default empty types list")]
    [Fact]
    public Task WrapGodConfig_Defaults()
        => Given("a new WrapGodConfig", () => new WrapGodConfig())
            .Then("Types is empty list", cfg => cfg.Types is not null && cfg.Types.Count == 0)
            .AssertPassed();

    [Scenario("TypeConfig has correct defaults")]
    [Fact]
    public Task TypeConfig_Defaults()
        => Given("a new TypeConfig", () => new TypeConfig())
            .Then("SourceType is empty", tc => tc.SourceType == string.Empty)
            .And("Include is null", tc => tc.Include is null)
            .And("TargetName is null", tc => tc.TargetName is null)
            .And("Members is empty list", tc => tc.Members is not null && tc.Members.Count == 0)
            .And("IsGenericPattern is false", tc => !tc.IsGenericPattern)
            .And("GenericArity is 0", tc => tc.GenericArity == 0)
            .AssertPassed();

    [Scenario("TypeConfig settable properties")]
    [Fact]
    public Task TypeConfig_Setters()
    {
        var tc = new TypeConfig
        {
            SourceType = "Acme.FooService",
            Include = true,
            TargetName = "MyFoo",
            IsGenericPattern = true,
            GenericArity = 2,
            Members = new List<MemberConfig>
            {
                new() { SourceMember = "DoWork", Include = false, TargetName = "Go" },
            },
        };

        return Given("a configured TypeConfig", () => tc)
            .Then("SourceType is set", t => t.SourceType == "Acme.FooService")
            .And("Include is true", t => t.Include == true)
            .And("TargetName is set", t => t.TargetName == "MyFoo")
            .And("IsGenericPattern is true", t => t.IsGenericPattern)
            .And("GenericArity is 2", t => t.GenericArity == 2)
            .And("Members has one entry", t => t.Members.Count == 1)
            .AssertPassed();
    }

    [Scenario("MemberConfig has correct defaults")]
    [Fact]
    public Task MemberConfig_Defaults()
        => Given("a new MemberConfig", () => new MemberConfig())
            .Then("SourceMember is empty", mc => mc.SourceMember == string.Empty)
            .And("Include is null", mc => mc.Include is null)
            .And("TargetName is null", mc => mc.TargetName is null)
            .AssertPassed();

    [Scenario("MemberConfig settable properties")]
    [Fact]
    public Task MemberConfig_Setters()
    {
        var mc = new MemberConfig
        {
            SourceMember = "Execute",
            Include = false,
            TargetName = "Run",
        };

        return Given("a configured MemberConfig", () => mc)
            .Then("SourceMember is set", m => m.SourceMember == "Execute")
            .And("Include is false", m => m.Include == false)
            .And("TargetName is set", m => m.TargetName == "Run")
            .AssertPassed();
    }

    [Scenario("ConfigSource enum values")]
    [Fact]
    public Task ConfigSource_Values()
        => Given("ConfigSource enum", () => true)
            .Then("Json is defined", _ => Enum.IsDefined(typeof(ConfigSource), ConfigSource.Json))
            .And("Attributes is defined", _ => Enum.IsDefined(typeof(ConfigSource), ConfigSource.Attributes))
            .AssertPassed();

    [Scenario("ConfigMergeResult defaults")]
    [Fact]
    public Task ConfigMergeResult_Defaults()
        => Given("a new ConfigMergeResult", () => new ConfigMergeResult())
            .Then("Config is not null", r => r.Config is not null)
            .And("Diagnostics is empty", r => r.Diagnostics is not null && r.Diagnostics.Count == 0)
            .AssertPassed();

    [Scenario("ConfigMergeResult settable")]
    [Fact]
    public Task ConfigMergeResult_Settable()
    {
        var result = new ConfigMergeResult
        {
            Config = new WrapGodConfig { Types = [new() { SourceType = "X" }] },
            Diagnostics = [new ConfigDiagnostic { Code = "WG001", Message = "test" }],
        };

        return Given("a populated ConfigMergeResult", () => result)
            .Then("Config has types", r => r.Config.Types.Count == 1)
            .And("Diagnostics has entries", r => r.Diagnostics.Count == 1)
            .AssertPassed();
    }

    [Scenario("ConfigDiagnostic defaults")]
    [Fact]
    public Task ConfigDiagnostic_Defaults()
        => Given("a new ConfigDiagnostic", () => new ConfigDiagnostic())
            .Then("Code is empty", cd => cd.Code == string.Empty)
            .And("Message is empty", cd => cd.Message == string.Empty)
            .And("Target is null", cd => cd.Target is null)
            .AssertPassed();

    [Scenario("ConfigDiagnostic settable")]
    [Fact]
    public Task ConfigDiagnostic_Settable()
    {
        var cd = new ConfigDiagnostic { Code = "WG100", Message = "Something", Target = "Foo" };

        return Given("a populated ConfigDiagnostic", () => cd)
            .Then("Code is set", d => d.Code == "WG100")
            .And("Message is set", d => d.Message == "Something")
            .And("Target is set", d => d.Target == "Foo")
            .AssertPassed();
    }

    [Scenario("ConfigMergeOptions defaults")]
    [Fact]
    public Task ConfigMergeOptions_Defaults()
        => Given("a new ConfigMergeOptions", () => new ConfigMergeOptions())
            .Then("HigherPrecedence defaults to Attributes", o =>
                o.HigherPrecedence == ConfigSource.Attributes)
            .AssertPassed();

    [Scenario("ConfigMergeOptions settable")]
    [Fact]
    public Task ConfigMergeOptions_Settable()
    {
        var opts = new ConfigMergeOptions { HigherPrecedence = ConfigSource.Json };
        return Given("options set to Json precedence", () => opts)
            .Then("HigherPrecedence is Json", o => o.HigherPrecedence == ConfigSource.Json)
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Attribute constructors and properties
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("WrapTypeAttribute constructor and properties")]
    [Fact]
    public Task WrapTypeAttribute_Properties()
    {
        var attr = new WrapTypeAttribute("MySource")
        {
            Include = false,
            TargetName = "MyTarget",
        };

        return Given("a WrapTypeAttribute", () => attr)
            .Then("SourceType is set", a => a.SourceType == "MySource")
            .And("Include is false", a => !a.Include)
            .And("TargetName is set", a => a.TargetName == "MyTarget")
            .AssertPassed();
    }

    [Scenario("WrapTypeAttribute defaults")]
    [Fact]
    public Task WrapTypeAttribute_Defaults()
    {
        var attr = new WrapTypeAttribute("Src");

        return Given("a default WrapTypeAttribute", () => attr)
            .Then("Include defaults to true", a => a.Include)
            .And("TargetName defaults to null", a => a.TargetName is null)
            .AssertPassed();
    }

    [Scenario("WrapMemberAttribute constructor and properties")]
    [Fact]
    public Task WrapMemberAttribute_Properties()
    {
        var attr = new WrapMemberAttribute("DoWork")
        {
            Include = false,
            TargetName = "Go",
        };

        return Given("a WrapMemberAttribute", () => attr)
            .Then("SourceMember is set", a => a.SourceMember == "DoWork")
            .And("Include is false", a => !a.Include)
            .And("TargetName is set", a => a.TargetName == "Go")
            .AssertPassed();
    }

    [Scenario("WrapMemberAttribute defaults")]
    [Fact]
    public Task WrapMemberAttribute_Defaults()
    {
        var attr = new WrapMemberAttribute("M");

        return Given("a default WrapMemberAttribute", () => attr)
            .Then("Include defaults to true", a => a.Include)
            .And("TargetName defaults to null", a => a.TargetName is null)
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WgDiagnosticV1 model
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("WgDiagnosticV1 defaults")]
    [Fact]
    public Task WgDiagnosticV1_Defaults()
        => Given("a new WgDiagnosticV1", () => new WgDiagnosticV1())
            .Then("Schema is correct", d => d.Schema == WgDiagnosticV1.SchemaId)
            .And("Code is empty", d => d.Code == string.Empty)
            .And("Severity is warning", d => d.Severity == WgDiagnosticSeverity.Warning)
            .And("Stage is analyze", d => d.Stage == WgDiagnosticStage.Analyze)
            .And("Message is empty", d => d.Message == string.Empty)
            .And("Source is not null", d => d.Source is not null)
            .And("Location is null", d => d.Location is null)
            .And("RelatedLocations is null", d => d.RelatedLocations is null)
            .And("HelpUri is null", d => d.HelpUri is null)
            .And("Tags is null", d => d.Tags is null)
            .And("Fingerprint is null", d => d.Fingerprint is null)
            .And("Properties is null", d => d.Properties is null)
            .And("Suppression is null", d => d.Suppression is null)
            .AssertPassed();

    [Scenario("WgDiagnosticV1 all properties settable")]
    [Fact]
    public Task WgDiagnosticV1_AllProperties()
    {
        var ts = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var diag = new WgDiagnosticV1
        {
            Code = "WG001",
            Severity = WgDiagnosticSeverity.Error,
            Stage = WgDiagnosticStage.Extract,
            Category = "extraction",
            Message = "Test error",
            HelpUri = "https://example.com",
            Tags = ["tag1", "tag2"],
            Fingerprint = "fp123",
            Properties = new Dictionary<string, object?> { ["key"] = "val" },
            Suppression = new WgDiagnosticSuppression
            {
                Kind = "pragma",
                Justification = "test",
                Source = "user",
            },
            Location = new WgDiagnosticLocation
            {
                Uri = "file.cs",
                Line = 10,
                Column = 5,
                EndLine = 10,
                EndColumn = 20,
                Symbol = "Method",
            },
            RelatedLocations = [new WgDiagnosticLocation { Uri = "other.cs" }],
            TimestampUtc = ts,
        };

        return Given("a fully populated WgDiagnosticV1", () => diag)
            .Then("Code is set", d => d.Code == "WG001")
            .And("Severity is error", d => d.Severity == WgDiagnosticSeverity.Error)
            .And("Stage is extract", d => d.Stage == WgDiagnosticStage.Extract)
            .And("Category is set", d => d.Category == "extraction")
            .And("Message is set", d => d.Message == "Test error")
            .And("HelpUri is set", d => d.HelpUri == "https://example.com")
            .And("Tags has 2 entries", d => d.Tags!.Count == 2)
            .And("Fingerprint is set", d => d.Fingerprint == "fp123")
            .And("Properties has key", d => d.Properties!.ContainsKey("key"))
            .And("Suppression is set", d => d.Suppression!.Kind == "pragma")
            .And("Location is set", d => d.Location!.Line == 10)
            .And("RelatedLocations has entry", d => d.RelatedLocations!.Count == 1)
            .And("TimestampUtc is set", d => d.TimestampUtc == ts)
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WgDiagnosticSeverity / Stage constants
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("WgDiagnosticSeverity constants")]
    [Fact]
    public Task Severity_Constants()
        => Given("severity constants", () => true)
            .Then("Error is error", _ => WgDiagnosticSeverity.Error == "error")
            .And("Warning is warning", _ => WgDiagnosticSeverity.Warning == "warning")
            .And("Note is note", _ => WgDiagnosticSeverity.Note == "note")
            .And("None is none", _ => WgDiagnosticSeverity.None == "none")
            .AssertPassed();

    [Scenario("WgDiagnosticStage constants")]
    [Fact]
    public Task Stage_Constants()
        => Given("stage constants", () => true)
            .Then("Extract is extract", _ => WgDiagnosticStage.Extract == "extract")
            .And("Plan is plan", _ => WgDiagnosticStage.Plan == "plan")
            .And("Generate is generate", _ => WgDiagnosticStage.Generate == "generate")
            .And("Analyze is analyze", _ => WgDiagnosticStage.Analyze == "analyze")
            .And("Fix is fix", _ => WgDiagnosticStage.Fix == "fix")
            .And("Cli is cli", _ => WgDiagnosticStage.Cli == "cli")
            .And("Config is config", _ => WgDiagnosticStage.Config == "config")
            .AssertPassed();

    // ═══════════════════════════════════════════════════════════════════
    //  WgDiagnosticSource
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("WgDiagnosticSource defaults and setters")]
    [Fact]
    public Task DiagnosticSource_Properties()
    {
        var src = new WgDiagnosticSource
        {
            Tool = "CustomTool",
            Component = "Extractor",
            Version = "1.2.3",
        };

        return Given("a WgDiagnosticSource", () => src)
            .Then("Tool is set", s => s.Tool == "CustomTool")
            .And("Component is set", s => s.Component == "Extractor")
            .And("Version is set", s => s.Version == "1.2.3")
            .AssertPassed();
    }

    [Scenario("WgDiagnosticSource defaults")]
    [Fact]
    public Task DiagnosticSource_Defaults()
        => Given("a new WgDiagnosticSource", () => new WgDiagnosticSource())
            .Then("Tool defaults to WrapGod", s => s.Tool == "WrapGod")
            .And("Component is null", s => s.Component is null)
            .And("Version is null", s => s.Version is null)
            .AssertPassed();

    // ═══════════════════════════════════════════════════════════════════
    //  WgDiagnosticLocation
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("WgDiagnosticLocation all properties")]
    [Fact]
    public Task DiagnosticLocation_Properties()
    {
        var loc = new WgDiagnosticLocation
        {
            Uri = "test.cs",
            Line = 1,
            Column = 2,
            EndLine = 3,
            EndColumn = 4,
            Symbol = "Method",
        };

        return Given("a WgDiagnosticLocation", () => loc)
            .Then("Uri is set", l => l.Uri == "test.cs")
            .And("Line is set", l => l.Line == 1)
            .And("Column is set", l => l.Column == 2)
            .And("EndLine is set", l => l.EndLine == 3)
            .And("EndColumn is set", l => l.EndColumn == 4)
            .And("Symbol is set", l => l.Symbol == "Method")
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WgDiagnosticSuppression
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("WgDiagnosticSuppression properties")]
    [Fact]
    public Task DiagnosticSuppression_Properties()
    {
        var sup = new WgDiagnosticSuppression
        {
            Kind = "pragma",
            Justification = "Not needed",
            Source = "user",
        };

        return Given("a WgDiagnosticSuppression", () => sup)
            .Then("Kind is pragma", s => s.Kind == "pragma")
            .And("Justification is set", s => s.Justification == "Not needed")
            .And("Source is user", s => s.Source == "user")
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  WgDiagnosticEmitter
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("WgDiagnosticEmitter.EmitJson produces valid JSON")]
    [Fact]
    public Task EmitJson_ValidOutput()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1
            {
                Code = "WG001",
                Message = "Test",
                Tags = ["tag1"],
                Properties = new Dictionary<string, object?> { ["extra"] = "val" },
            },
        };

        return Given("diagnostics emitted as JSON",
                () => WgDiagnosticEmitter.EmitJson(diagnostics))
            .Then("output is valid JSON", json =>
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array;
            })
            .AssertPassed();
    }

    [Scenario("WgDiagnosticEmitter.EmitSarif produces SARIF JSON")]
    [Fact]
    public Task EmitSarif_ValidOutput()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1
            {
                Code = "WG001",
                Severity = WgDiagnosticSeverity.Error,
                Message = "Error occurred",
                Category = "test",
                HelpUri = "https://help.com",
                Tags = ["important"],
                Fingerprint = "fp1",
                Suppression = new WgDiagnosticSuppression { Kind = "pragma", Justification = "ok" },
                Location = new WgDiagnosticLocation
                {
                    Uri = "file.cs",
                    Line = 1,
                    Column = 1,
                    EndLine = 1,
                    EndColumn = 10,
                    Symbol = "Method",
                },
                RelatedLocations = [new WgDiagnosticLocation { Uri = "other.cs", Symbol = "Sym" }],
                Properties = new Dictionary<string, object?> { ["custom"] = "value" },
            },
            new WgDiagnosticV1
            {
                Code = "WG002",
                Severity = WgDiagnosticSeverity.Note,
                Message = "Note message",
            },
            new WgDiagnosticV1
            {
                Code = "WG001",
                Severity = WgDiagnosticSeverity.Warning,
                Message = "Another WG001",
            },
        };

        return Given("diagnostics emitted as SARIF",
                () => WgDiagnosticEmitter.EmitSarif(diagnostics))
            .Then("output contains version 2.1.0", sarif =>
                sarif.Contains("\"2.1.0\"", StringComparison.Ordinal))
            .And("output contains runs", sarif =>
                sarif.Contains("\"runs\"", StringComparison.Ordinal))
            .And("output contains ruleId", sarif =>
                sarif.Contains("\"ruleId\"", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("WgDiagnosticEmitter.EmitSarif with null/empty input")]
    [Fact]
    public Task EmitSarif_EmptyInput()
        => Given("empty diagnostics",
                () => WgDiagnosticEmitter.EmitSarif(Enumerable.Empty<WgDiagnosticV1>()))
            .Then("output is valid JSON", sarif =>
            {
                using var doc = System.Text.Json.JsonDocument.Parse(sarif);
                return true;
            })
            .AssertPassed();

    [Scenario("WgDiagnosticEmitter.EmitSarif with no location")]
    [Fact]
    public Task EmitSarif_NoLocation()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1
            {
                Code = "WG001",
                Message = "No location",
                Location = null,
            },
        };

        return Given("diagnostics with no location",
                () => WgDiagnosticEmitter.EmitSarif(diagnostics))
            .Then("output is valid JSON", sarif =>
            {
                using var doc = System.Text.Json.JsonDocument.Parse(sarif);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WgDiagnosticEmitter.EmitSarif with suppression kinds")]
    [Fact]
    public Task EmitSarif_SuppressionKinds()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1
            {
                Code = "WG001",
                Message = "Suppressed via pragma",
                Suppression = new WgDiagnosticSuppression { Kind = "pragma" },
            },
            new WgDiagnosticV1
            {
                Code = "WG002",
                Message = "Suppressed via editorconfig",
                Suppression = new WgDiagnosticSuppression { Kind = "editorconfig" },
            },
            new WgDiagnosticV1
            {
                Code = "WG003",
                Message = "Suppressed via other",
                Suppression = new WgDiagnosticSuppression { Kind = "external" },
            },
        };

        return Given("diagnostics with various suppression kinds",
                () => WgDiagnosticEmitter.EmitSarif(diagnostics))
            .Then("output contains inSource", sarif =>
                sarif.Contains("inSource", StringComparison.Ordinal))
            .And("output contains external", sarif =>
                sarif.Contains("external", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("WgDiagnosticEmitter.FromConfigDiagnostic")]
    [Fact]
    public Task FromConfigDiagnostic_Converts()
    {
        var cd = new ConfigDiagnostic { Code = "WG100", Message = "Config issue", Target = "TypeA" };
        var ts = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        return Given("a ConfigDiagnostic converted",
                () => WgDiagnosticEmitter.FromConfigDiagnostic(cd, ts))
            .Then("Code matches", wg => wg.Code == "WG100")
            .And("Stage is config", wg => wg.Stage == WgDiagnosticStage.Config)
            .And("Location has symbol", wg => wg.Location!.Symbol == "TypeA")
            .And("Timestamp matches", wg => wg.TimestampUtc == ts)
            .AssertPassed();
    }

    [Scenario("WgDiagnosticEmitter.FromConfigDiagnostic with null target")]
    [Fact]
    public Task FromConfigDiagnostic_NullTarget()
    {
        var cd = new ConfigDiagnostic { Code = "WG100", Message = "No target" };

        return Given("a ConfigDiagnostic with null target",
                () => WgDiagnosticEmitter.FromConfigDiagnostic(cd))
            .Then("Location is null", wg => wg.Location is null)
            .AssertPassed();
    }

    // ═══════════════════════════════════════════════════════════════════
    //  DiagnosticsGateEvaluator
    // ═══════════════════════════════════════════════════════════════════

    [Scenario("DiagnosticsGateEvaluator: command failed returns RuntimeFailure")]
    [Fact]
    public Task GateEvaluator_CommandFailed()
        => Given("a failed command evaluation",
                () => DiagnosticsGateEvaluator.EvaluateExitCode(
                    Enumerable.Empty<WgDiagnosticV1>(), false, commandFailed: true))
            .Then("returns RuntimeFailure", code => code == WgCliExitCode.RuntimeFailure)
            .AssertPassed();

    [Scenario("DiagnosticsGateEvaluator: no diagnostics returns Success")]
    [Fact]
    public Task GateEvaluator_NoDiags_Success()
        => Given("empty diagnostics",
                () => DiagnosticsGateEvaluator.EvaluateExitCode(
                    Enumerable.Empty<WgDiagnosticV1>(), false))
            .Then("returns Success", code => code == WgCliExitCode.Success)
            .AssertPassed();

    [Scenario("DiagnosticsGateEvaluator: error diagnostics returns DiagnosticsError")]
    [Fact]
    public Task GateEvaluator_Error_DiagnosticsError()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1 { Severity = WgDiagnosticSeverity.Error, Code = "WG001", Message = "err" },
        };

        return Given("diagnostics with error",
                () => DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, false))
            .Then("returns DiagnosticsError", code => code == WgCliExitCode.DiagnosticsError)
            .AssertPassed();
    }

    [Scenario("DiagnosticsGateEvaluator: warnings with warningsAsErrors returns WarningsAsErrors")]
    [Fact]
    public Task GateEvaluator_WarningsAsErrors()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1 { Severity = WgDiagnosticSeverity.Warning, Code = "WG002", Message = "warn" },
        };

        return Given("warnings with warningsAsErrors enabled",
                () => DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, warningsAsErrors: true))
            .Then("returns WarningsAsErrors", code => code == WgCliExitCode.WarningsAsErrors)
            .AssertPassed();
    }

    [Scenario("DiagnosticsGateEvaluator: warnings without warningsAsErrors returns Success")]
    [Fact]
    public Task GateEvaluator_Warnings_Success()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1 { Severity = WgDiagnosticSeverity.Warning, Code = "WG002", Message = "warn" },
        };

        return Given("warnings without warningsAsErrors",
                () => DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, warningsAsErrors: false))
            .Then("returns Success", code => code == WgCliExitCode.Success)
            .AssertPassed();
    }

    [Scenario("DiagnosticsGateEvaluator: suppressed diagnostics are skipped")]
    [Fact]
    public Task GateEvaluator_SuppressedSkipped()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1
            {
                Severity = WgDiagnosticSeverity.Error,
                Code = "WG001",
                Message = "suppressed err",
                Suppression = new WgDiagnosticSuppression { Kind = "pragma" },
            },
        };

        return Given("suppressed error diagnostics",
                () => DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, false))
            .Then("returns Success (suppressed)", code => code == WgCliExitCode.Success)
            .AssertPassed();
    }

    [Scenario("DiagnosticsGateEvaluator: none severity is skipped")]
    [Fact]
    public Task GateEvaluator_NoneSeverity_Skipped()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1
            {
                Severity = WgDiagnosticSeverity.None,
                Code = "WG001",
                Message = "none severity",
            },
        };

        return Given("diagnostics with none severity",
                () => DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, warningsAsErrors: true))
            .Then("returns Success", code => code == WgCliExitCode.Success)
            .AssertPassed();
    }

    [Scenario("DiagnosticsGateEvaluator: note severity alone is Success")]
    [Fact]
    public Task GateEvaluator_NoteSeverity()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1
            {
                Severity = WgDiagnosticSeverity.Note,
                Code = "WG001",
                Message = "note",
            },
        };

        return Given("diagnostics with note severity",
                () => DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, false))
            .Then("returns Success", code => code == WgCliExitCode.Success)
            .AssertPassed();
    }

    [Scenario("WgCliExitCode enum values")]
    [Fact]
    public Task ExitCode_Values()
        => Given("WgCliExitCode enum", () => true)
            .Then("Success is 0", _ => (int)WgCliExitCode.Success == 0)
            .And("RuntimeFailure is 1", _ => (int)WgCliExitCode.RuntimeFailure == 1)
            .And("DiagnosticsError is 2", _ => (int)WgCliExitCode.DiagnosticsError == 2)
            .And("WarningsAsErrors is 3", _ => (int)WgCliExitCode.WarningsAsErrors == 3)
            .AssertPassed();

    [Scenario("WgDiagnosticEmitter.EmitSarif with location but no region")]
    [Fact]
    public Task EmitSarif_LocationNoRegion()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1
            {
                Code = "WG001",
                Message = "Location with symbol only",
                Location = new WgDiagnosticLocation { Symbol = "MyMethod" },
            },
        };

        return Given("diagnostic with location having only a symbol",
                () => WgDiagnosticEmitter.EmitSarif(diagnostics))
            .Then("output is valid SARIF", sarif =>
            {
                using var doc = System.Text.Json.JsonDocument.Parse(sarif);
                return sarif.Contains("MyMethod", StringComparison.Ordinal);
            })
            .AssertPassed();
    }
}
