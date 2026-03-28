using System.CommandLine;
using WrapGod.Abstractions.Diagnostics;

namespace WrapGod.Cli;

internal static class DoctorCommand
{
    public static Command Create()
    {
        var pathOption = new Option<DirectoryInfo>(["--path", "-p"], () => new DirectoryInfo(Directory.GetCurrentDirectory()), "Project root path to validate");
        var formatOption = new Option<string>("--format", () => "text", "Output format: text, json, sarif");
        var warningsAsErrorsOption = new Option<bool>("--warnings-as-errors", "Treat warnings as errors (exit code 3)");

        var command = new Command("doctor", "Validate setup, source/lockfile health, and CI readiness")
        {
            pathOption,
            formatOption,
            warningsAsErrorsOption,
        };

        command.SetHandler(async (DirectoryInfo path, string format, bool warningsAsErrors) =>
        {
            Environment.ExitCode = await HandleAsync(path, format, warningsAsErrors);
        }, pathOption, formatOption, warningsAsErrorsOption);

        return command;
    }

    internal static async Task<int> HandleAsync(DirectoryInfo path, string format, bool warningsAsErrors)
    {
        var validator = new DoctorHealthValidator();
        var diagnostics = await validator.ValidateAsync(path);
        var exitCode = DiagnosticsGateEvaluator.EvaluateExitCode(diagnostics, warningsAsErrors);

        switch (format.ToLowerInvariant())
        {
            case "text":
                EmitText(path.FullName, diagnostics, exitCode);
                break;
            case "json":
                Console.WriteLine(WgDiagnosticEmitter.EmitJson(diagnostics));
                break;
            case "sarif":
                Console.WriteLine(WgDiagnosticEmitter.EmitSarif(diagnostics));
                break;
            default:
                Console.Error.WriteLine($"Unknown format '{format}'. Supported values: text, json, sarif.");
                return (int)WgCliExitCode.RuntimeFailure;
        }

        return (int)exitCode;
    }

    private static void EmitText(string rootPath, IReadOnlyList<WgDiagnosticV1> diagnostics, WgCliExitCode exitCode)
    {
        Console.WriteLine("WrapGod Doctor");
        Console.WriteLine(new string('-', 40));
        Console.WriteLine($"Path: {rootPath}");
        Console.WriteLine();

        if (diagnostics.Count == 0)
        {
            Console.WriteLine("No issues found. ✅");
            return;
        }

        foreach (var diagnostic in diagnostics.OrderBy(d => d.Severity).ThenBy(d => d.Code, StringComparer.Ordinal))
        {
            Console.WriteLine($"[{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message}");
            if (diagnostic.Properties is not null && diagnostic.Properties.TryGetValue("remediation", out var remediation))
            {
                Console.WriteLine($"  Fix: {remediation}");
            }

            if (!string.IsNullOrWhiteSpace(diagnostic.HelpUri))
            {
                Console.WriteLine($"  Dependency: {diagnostic.HelpUri}");
            }

            Console.WriteLine();
        }

        Console.WriteLine($"Exit code: {(int)exitCode} ({exitCode})");
    }
}
