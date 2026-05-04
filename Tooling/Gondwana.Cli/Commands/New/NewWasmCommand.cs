using System.ComponentModel;
using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.New;

internal sealed class NewWasmCommand : Command<NewWasmCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("The name of the new project.")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("-o|--output")]
        [Description("The directory to place the generated output in. Defaults to a new folder named <name> in the current directory.")]
        public string? Output { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            AnsiConsole.MarkupLine("[red]Project name is required.[/]");
            return 1;
        }

        var args = new List<string> { "new", "gondwana-wasm", "-n", settings.Name };
        if (settings.Output is not null)
        {
            args.Add("-o");
            args.Add(settings.Output);
        }

        AnsiConsole.MarkupLine($"Creating Gondwana WASM project: [bold]{Markup.Escape(settings.Name)}[/]");

        var exitCode = ProcessHelper.RunLive("dotnet", args);

        if (exitCode == 0)
        {
            AnsiConsole.MarkupLine($"[green]Project '{Markup.Escape(settings.Name)}' created successfully.[/]");
            AnsiConsole.MarkupLine($"[dim]Desktop: cd {Markup.Escape(settings.Output ?? settings.Name)} && dotnet run[/]");
            AnsiConsole.MarkupLine($"[dim]WASM:    cd {Markup.Escape(settings.Output ?? settings.Name)} && dotnet workload install wasm-tools && dotnet publish -f net8.0-browser[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Project creation failed. Is the Gondwana template installed?[/]");
            AnsiConsole.MarkupLine("[dim]Run: gondwana templates install[/]");
        }

        return exitCode;
    }
}
