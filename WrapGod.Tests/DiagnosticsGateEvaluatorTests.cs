using WrapGod.Abstractions.Diagnostics;

namespace WrapGod.Tests;

public sealed class DiagnosticsGateEvaluatorTests
{
    [Fact]
    public void EvaluateExitCode_ReturnsRuntimeFailure_WhenCommandFails()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1 { Code = "WG2001", Severity = WgDiagnosticSeverity.Error, Message = "error" },
        };

        var result = DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, warningsAsErrors: false, commandFailed: true);

        Assert.Equal(WgCliExitCode.RuntimeFailure, result);
    }

    [Fact]
    public void EvaluateExitCode_ReturnsDiagnosticsError_WhenErrorExists()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1 { Code = "WG2001", Severity = WgDiagnosticSeverity.Warning, Message = "warning" },
            new WgDiagnosticV1 { Code = "WG6002", Severity = WgDiagnosticSeverity.Error, Message = "error" },
        };

        var result = DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, warningsAsErrors: false);

        Assert.Equal(WgCliExitCode.DiagnosticsError, result);
    }

    [Fact]
    public void EvaluateExitCode_ReturnsWarningsAsErrors_WhenEnabledAndWarningExists()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1 { Code = "WG7001", Severity = WgDiagnosticSeverity.Warning, Message = "warning" },
        };

        var result = DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, warningsAsErrors: true);

        Assert.Equal(WgCliExitCode.WarningsAsErrors, result);
    }

    [Fact]
    public void EvaluateExitCode_ReturnsSuccess_WhenNoBlockingDiagnostics()
    {
        var diagnostics = new[]
        {
            new WgDiagnosticV1 { Code = "WG2002", Severity = WgDiagnosticSeverity.Note, Message = "note" },
            new WgDiagnosticV1
            {
                Code = "WG7002",
                Severity = WgDiagnosticSeverity.Warning,
                Message = "suppressed warning",
                Suppression = new WgDiagnosticSuppression { Kind = "inSource", Justification = "intentional" },
            },
            new WgDiagnosticV1 { Code = "WG7003", Severity = WgDiagnosticSeverity.None, Message = "none" },
        };

        var result = DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, warningsAsErrors: true);

        Assert.Equal(WgCliExitCode.Success, result);
    }
}
