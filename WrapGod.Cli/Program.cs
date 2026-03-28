using System.CommandLine;
using WrapGod.Cli;

var rootCommand = new RootCommand("WrapGod CLI -- extract manifests, generate wrappers, analyze migrations, and bootstrap baseline files")
{
    ExtractCommand.Create(),
    GenerateCommand.Create(),
    AnalyzeCommand.Create(),
    InitCommand.Create(),
};

return await rootCommand.InvokeAsync(args);
