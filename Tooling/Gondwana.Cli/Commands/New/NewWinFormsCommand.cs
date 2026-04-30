using System.ComponentModel;
using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.New;

internal sealed class NewWinFormsCommand : Command<NewWinFormsCommand.Settings>
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

        var outputArg = settings.Output is not null
            ? $" -o \"{settings.Output}\""
            : string.Empty;

        AnsiConsole.MarkupLine($"Creating Gondwana WinForms project: [bold]{Markup.Escape(settings.Name)}[/]");

        var arguments = $"new gondwana-winforms -n \"{settings.Name}\"{outputArg}";
        var exitCode = ProcessHelper.RunLive("dotnet", arguments);

        if (exitCode == 0)
        {
            AnsiConsole.MarkupLine($"[green]Project '{Markup.Escape(settings.Name)}' created successfully.[/]");
            AnsiConsole.MarkupLine($"[dim]cd {Markup.Escape(settings.Output ?? settings.Name)} && dotnet run[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Project creation failed. Is the Gondwana template installed?[/]");
            AnsiConsole.MarkupLine("[dim]Run: gondwana templates install[/]");
        }

        return exitCode;
    }
}
