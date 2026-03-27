namespace WrapGod.Abstractions.Diagnostics;

public enum WgCliExitCode
{
    Success = 0,
    RuntimeFailure = 1,
    DiagnosticsError = 2,
    WarningsAsErrors = 3,
}

public static class DiagnosticsGateEvaluator
{
    public static WgCliExitCode EvaluateExitCode(
        IEnumerable<WgDiagnosticV1> diagnostics,
        bool warningsAsErrors,
        bool commandFailed = false)
    {
        if (commandFailed)
        {
            return WgCliExitCode.RuntimeFailure;
        }

        var hasError = false;
        var hasWarning = false;

        foreach (var diagnostic in diagnostics)
        {
            var effectiveSeverity = GetEffectiveSeverity(diagnostic);
            if (effectiveSeverity is null)
            {
                continue;
            }

            if (string.Equals(effectiveSeverity, WgDiagnosticSeverity.Error, StringComparison.OrdinalIgnoreCase))
            {
                hasError = true;
                break;
            }

            if (string.Equals(effectiveSeverity, WgDiagnosticSeverity.Warning, StringComparison.OrdinalIgnoreCase))
            {
                hasWarning = true;
            }
        }

        if (hasError)
        {
            return WgCliExitCode.DiagnosticsError;
        }

        if (warningsAsErrors && hasWarning)
        {
            return WgCliExitCode.WarningsAsErrors;
        }

        return WgCliExitCode.Success;
    }

    private static string? GetEffectiveSeverity(WgDiagnosticV1 diagnostic)
    {
        if (diagnostic.Suppression is not null)
        {
            return null;
        }

        if (string.Equals(diagnostic.Severity, WgDiagnosticSeverity.None, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return diagnostic.Severity;
    }
}
