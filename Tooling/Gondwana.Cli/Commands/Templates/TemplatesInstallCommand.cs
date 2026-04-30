using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Templates;

internal sealed class TemplatesInstallCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("Installing [bold]Gondwana.Templates[/] from NuGet...");
        var exitCode = ProcessHelper.RunLive("dotnet", "new install Gondwana.Templates");

        if (exitCode == 0)
            AnsiConsole.MarkupLine("[green]Templates installed successfully.[/]");
        else
            AnsiConsole.MarkupLine("[red]Template installation failed.[/]");

        return exitCode;
    }
}
