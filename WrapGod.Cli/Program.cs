using System.CommandLine;
using WrapGod.Cli;

var ciCommand = new Command("ci", "CI/CD workflow tools")
{
    CiBootstrapCommand.Create(),
    CiParityReportCommand.Create()
};

var rootCommand = new RootCommand("WrapGod CLI -- extract manifests, generate wrappers, and analyze migrations")
{
    InitCommand.Create(),
    ExtractCommand.Create(),
    GenerateCommand.Create(),
    AnalyzeCommand.Create(),
    DoctorCommand.Create(),
    ExplainCommand.Create(),
    MigrateCommandBuilder.Build(),
    ciCommand
};

return await rootCommand.InvokeAsync(args);

// ─────────────────────────────────────────────────────────────────────────────
// Coverage exclusion for the top-level entry point.
//
// Top-level statements compile to a generated `Program` class with a synthetic
// `<Main>$` method.  That method is the CLI's actual entry point — it wires the
// command tree together and dispatches to System.CommandLine.  Every behaviour
// inside the assembly is independently covered by sub-command tests that invoke
// the relevant Command.InvokeAsync directly.  We therefore exclude this
// synthetic class from coverage via a partial declaration adorned with
// [ExcludeFromCodeCoverage].
//
// This is the conventional Coverlet-friendly way to exclude top-level Main
// entry points — see https://github.com/coverlet-coverage/coverlet/issues/838
// ─────────────────────────────────────────────────────────────────────────────
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(
    Justification = "Top-level Main entry point: every dispatched sub-command is covered independently by CLI tests.")]
internal partial class Program;
