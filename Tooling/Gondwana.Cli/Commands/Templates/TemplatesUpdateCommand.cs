using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Templates;

internal sealed class TemplatesUpdateCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("Updating [bold]Gondwana.Templates[/]...");
        var exitCode = ProcessHelper.RunLive("dotnet", "new update Gondwana.Templates");

        if (exitCode == 0)
            AnsiConsole.MarkupLine("[green]Templates updated successfully.[/]");
        else
            AnsiConsole.MarkupLine("[red]Template update failed.[/]");

        return exitCode;
    }
}
