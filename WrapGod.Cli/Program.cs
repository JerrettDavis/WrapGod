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
    MigrateInitCommand.Create(),
    ciCommand
};

return await rootCommand.InvokeAsync(args);
