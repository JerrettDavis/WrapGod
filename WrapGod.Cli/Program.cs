using System.CommandLine;
using WrapGod.Cli;

var rootCommand = new RootCommand("WrapGod CLI -- extract manifests, generate wrappers, analyze migrations, and validate setup health")
{
    ExtractCommand.Create(),
    GenerateCommand.Create(),
    AnalyzeCommand.Create(),
    DoctorCommand.Create(),
};

return await rootCommand.InvokeAsync(args);
