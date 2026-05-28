using System.CommandLine;

namespace WrapGod.Cli;

/// <summary>
/// Builds the <c>migrate</c> command tree, aggregating all migrate sub-commands.
/// Add new sub-commands here as additional issues are implemented (#199 apply, #200 status, #201 verify).
/// </summary>
internal static class MigrateCommandBuilder
{
    public static Command Build()
    {
        var migrateCommand = new Command("migrate", "Migration tools for adopting WrapGod wrappers")
        {
            MigrateInitCommand.CreateSubCommand(),
            MigrateGenerateCommand.Create(),
            MigrateApplyCommand.Create(),
            MigrateStatusCommand.Create(),
        };

        return migrateCommand;
    }
}
