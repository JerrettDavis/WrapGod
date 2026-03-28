using System.CommandLine;
using WrapGod.Abstractions.Diagnostics;

namespace WrapGod.Cli;

public static class DoctorCommand
{
    public static Command Create()
    {
        var pathOption = new Option<DirectoryInfo>(["--path", "-p"], () => new DirectoryInfo(Directory.GetCurrentDirectory()), "Project root path to validate");
        var formatOption = new Option<string>("--format", () => "text", "Output format: text, json, sarif");
        var warningsAsErrorsOption = new Option<bool>("--warnings-as-errors", "Treat warnings as errors (exit code 3)");
        var command = new Command("doctor", "Validate setup, source/lockfile health, and CI readiness") { pathOption, formatOption, warningsAsErrorsOption };
        command.SetHandler(async (DirectoryInfo p, string f, bool w) => Environment.ExitCode = await HandleAsync(p, f, w), pathOption, formatOption, warningsAsErrorsOption);
        return command;
    }

    public static async Task<int> HandleAsync(DirectoryInfo path, string format, bool warningsAsErrors)
    {
        var diagnostics = await new DoctorHealthValidator().ValidateAsync(path);
        var exitCode = DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, warningsAsErrors);
        Console.WriteLine(format.Equals("sarif", StringComparison.OrdinalIgnoreCase) ? WgDiagnosticEmitter.EmitSarif(diagnostics)
            : format.Equals("json", StringComparison.OrdinalIgnoreCase) ? WgDiagnosticEmitter.EmitJson(diagnostics)
            : string.Join(Environment.NewLine, diagnostics.Select(d => $"[{d.Severity}] {d.Code}: {d.Message}")));
        return (int)exitCode;
    }
}
