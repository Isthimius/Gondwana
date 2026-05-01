using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands;

internal sealed class HelpCommand : Command
{
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[bold]Gondwana CLI[/] — quick reference\n");

        var table = new Table().BorderColor(Color.Grey).Border(TableBorder.Simple);
        table.AddColumn("[bold]Command[/]");
        table.AddColumn("[bold]Description[/]");

        table.AddRow("[cyan]gondwana help[/]",                    "Show this help summary.");
        table.AddRow("[cyan]gondwana doctor[/]",                  "Validate your local Gondwana development environment.");
        table.AddRow("[cyan]gondwana info[/]",                    "Show information about the Gondwana project in the current directory.");
        table.AddRow("[cyan]gondwana new winforms <name>[/]",     "Scaffold a new WinForms Gondwana project.");
        table.AddRow("[cyan]gondwana new avalonia <name>[/]",     "Scaffold a new Avalonia Gondwana project (Windows, macOS, Linux).");
        table.AddRow("[cyan]gondwana templates install[/]",       "Install Gondwana.Templates from NuGet.");
        table.AddRow("[cyan]gondwana templates update[/]",        "Update installed Gondwana templates.");
        table.AddRow("[cyan]gondwana templates list[/]",          "List installed Gondwana templates.");
        table.AddRow("[cyan]gondwana pack[/]",                     "Pack a directory of files into an asset bundle (shorthand).");
        table.AddRow("[cyan]gondwana assets pack[/]",             "Pack a directory of files into an asset bundle.");
        table.AddRow("[cyan]gondwana assets list[/]",             "List all assets in a bundle.");
        table.AddRow("[cyan]gondwana assets extract[/]",          "Extract all assets from a bundle to a directory.");
        table.AddRow("[cyan]gondwana assets generate-keys[/]",    "Generate a C# constants class for all asset keys in a bundle.");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\nRun [cyan]gondwana <command> --help[/] for detailed usage of any command.");

        return 0;
    }
}
