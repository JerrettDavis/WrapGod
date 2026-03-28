using System.CommandLine;
using WrapGod.Cli;

var rootCommand = new RootCommand("WrapGod CLI -- extract manifests, generate wrappers, and analyze migrations")
{
    InitCommand.Create(),
    ExtractCommand.Create(),
    GenerateCommand.Create(),
    AnalyzeCommand.Create(),
    DoctorCommand.Create()
};

return await rootCommand.InvokeAsync(args);
