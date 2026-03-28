using System.CommandLine;
using WrapGod.Cli;

var rootCommand = new RootCommand("WrapGod CLI -- extract manifests, generate wrappers, analyze migrations, bootstrap baseline files, and validate setup health")
{
    ExtractCommand.Create(),
    GenerateCommand.Create(),
    AnalyzeCommand.Create(),
    InitCommand.Create(),
    DoctorCommand.Create(),
};

return await rootCommand.InvokeAsync(args);
