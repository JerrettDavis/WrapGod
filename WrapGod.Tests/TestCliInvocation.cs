using System.CommandLine;
using System.CommandLine.Help;

namespace WrapGod.Tests;

/// <summary>
/// Test-only compatibility shim for the System.CommandLine 2.0.0-rc API.
///
/// The rc API removed the <c>Command.InvokeAsync(string)</c> / <c>Command.InvokeAsync(string[])</c>
/// convenience overloads in favour of the explicit <c>command.Parse(args).InvokeAsync()</c> pipeline.
/// The existing CLI test suite invokes commands with a single command-line string (and, in a few
/// places, a token array).  These extension methods restore the old ergonomics so the tests continue
/// to express intent the same way, while routing through the new parse-then-invoke pipeline.
///
/// Behaviourally equivalent to the old overloads: the command line is tokenised by
/// <see cref="Command.Parse(string, System.CommandLine.ParserConfiguration?)"/> and the resulting
/// <see cref="System.CommandLine.ParseResult"/> is invoked; the returned <see cref="int"/> is the
/// invocation exit code (parse errors and the framework help/version actions propagate their own
/// non-zero exit codes exactly as before).
/// </summary>
internal static class TestCliInvocation
{
    public static Task<int> InvokeAsync(this Command command, string commandLine)
        => EnsureHelp(command).Parse(commandLine).InvokeAsync(CreateConfiguration());

    public static Task<int> InvokeAsync(this Command command, string[] args)
        => EnsureHelp(command).Parse(args).InvokeAsync(CreateConfiguration());

    // The old Command.InvokeAsync overloads implicitly applied UseDefaults(), which added a
    // recursive --help option (and error handling) to whatever command was invoked — even a bare
    // subcommand tree. In the rc API only RootCommand ships those by default, so a standalone
    // Command created in a test has no --help. Add a recursive HelpOption when one is absent so
    // "<subcommand> --help" resolves to the HelpAction exactly as before.
    private static Command EnsureHelp(Command command)
    {
        if (!command.Options.Any(o => o is HelpOption))
        {
            command.Add(new HelpOption());
        }

        return command;
    }

    // The rc API routes framework-generated output (help text, parse errors, version) through
    // InvocationConfiguration.Output/Error rather than Console directly. The old
    // Command.InvokeAsync overloads wrote to Console, which the tests capture via Console.SetOut.
    // Bind Output/Error to the currently-installed Console writers (the tests' StringWriters) so
    // captured stdout/stderr and exit codes match the pre-migration behaviour.
    private static InvocationConfiguration CreateConfiguration() => new()
    {
        Output = Console.Out,
        Error = Console.Error,
    };
}
