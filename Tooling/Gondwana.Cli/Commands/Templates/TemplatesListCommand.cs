using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Templates;

internal sealed class TemplatesListCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("Installed [bold]Gondwana[/] templates:");
        AnsiConsole.WriteLine();

        var exitCode = ProcessHelper.RunLive("dotnet", "new list gondwana");
        return exitCode;
    }
}
