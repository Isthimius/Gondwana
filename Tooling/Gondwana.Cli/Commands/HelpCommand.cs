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
        table.AddRow("[cyan]gondwana check [[--fix]][/]",          "Check project packages, resources, and configuration; optionally align older packages to core.");
        table.AddRow("[cyan]gondwana upgrade[/]",                 "Upgrade referenced Gondwana packages together; supports --version and --dry-run.");
        table.AddRow("[cyan]gondwana add <feature>[/]",           "Add widgets, audio, midi, gamepad, video, or hosting.");
        table.AddRow("[cyan]gondwana serve[/]",                   "Serve a published browser build with WASM isolation headers.");
        table.AddRow("[cyan]gondwana tilesheet info <file>[/]",    "Show GTS image, region, frame, and collision metadata.");
        table.AddRow("[cyan]gondwana tilesheet validate <file>[/]", "Check GTS image references and geometry.");
        table.AddRow("[cyan]gondwana assets inspect <file>[/]",   "Show bundle summary and entry sizes.");
        table.AddRow("[cyan]gondwana assets validate <file>[/]",  "Validate archive integrity, paths, and tilesheet contents.");
        table.AddRow("[cyan]gondwana assets unpack <file> <output>[/]", "Extract safely; existing files require --overwrite.");
        table.AddRow("[cyan]gondwana new winforms <name>[/]",     "Scaffold a new WinForms Gondwana project.");
        table.AddRow("[cyan]gondwana new avalonia <name>[/]",     "Scaffold a new Avalonia Gondwana project (Windows, macOS, Linux).");
        table.AddRow("[cyan]gondwana new blazor <name>[/]",       "Scaffold a Blazor WebAssembly Gondwana project using GPU-backed WebGL.");
        table.AddRow("[cyan]gondwana run[/]",                     "Run the desktop build of the project in the current directory.");
        table.AddRow("[cyan]gondwana run blazor[/]",              "Build and run the Blazor WebAssembly/WebGL project in the browser.");
        table.AddRow("[cyan]gondwana publish blazor[/]",          "Publish a Gondwana Blazor WebAssembly/WebGL project for browser deployment.");
        table.AddRow("[cyan]gondwana publish[/]",                 "Publish the desktop project for distribution.");
        table.AddRow("[cyan]gondwana publish itch[/]",            "Package published browser output for itch.io.");
        table.AddRow("[cyan]gondwana deploy blazor[/]",           "Deploy published browser output locally or over SSH.");
        table.AddRow("[cyan]gondwana deploy itch[/]",             "Upload the browser package with butler.");
        table.AddRow("[cyan]gondwana templates install[/]",       "Install Gondwana.Templates, or check for updates if already installed.");
        table.AddRow("[cyan]gondwana templates update[/]",        "Check installed Gondwana templates for updates without downgrading newer local versions.");
        table.AddRow("[cyan]gondwana templates list[/]",          "List installed Gondwana templates.");
        table.AddRow("[cyan]gondwana pack[/]",                    "Pack a directory of files into an asset bundle (shorthand).");
        table.AddRow("[cyan]gondwana assets pack[/]",             "Pack a directory of files into an asset bundle.");
        table.AddRow("[cyan]gondwana assets list[/]",             "List all assets in a bundle.");
        table.AddRow("[cyan]gondwana assets extract[/]",          "Extract all assets from a bundle to a directory.");
        table.AddRow("[cyan]gondwana assets generate-keys[/]",    "Generate a C# constants class for all asset keys in a bundle.");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\nRun [cyan]gondwana <command> --help[/] for detailed usage of any command.");

        return 0;
    }
}
