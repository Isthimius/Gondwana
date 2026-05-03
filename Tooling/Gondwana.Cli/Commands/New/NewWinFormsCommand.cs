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

        [CommandOption("-b|--backbuffer")]
        [Description("The backbuffer type to use for rendering: 'bitmap' (default, CPU-based) or 'gpu' (OpenGL-accelerated).")]
        public string? Backbuffer { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            AnsiConsole.MarkupLine("[red]Project name is required.[/]");
            return 1;
        }

        var backbuffer = settings.Backbuffer?.ToLowerInvariant();
        if (backbuffer is not null && backbuffer != "bitmap" && backbuffer != "gpu")
        {
            AnsiConsole.MarkupLine("[red]Invalid --backbuffer value. Use 'bitmap' or 'gpu'.[/]");
            return 1;
        }

        var args = new List<string> { "new", "gondwana-winforms", "-n", settings.Name };
        if (settings.Output is not null)
        {
            args.Add("-o");
            args.Add(settings.Output);
        }
        if (backbuffer is not null)
        {
            args.Add("--Backbuffer");
            args.Add(backbuffer);
        }

        AnsiConsole.MarkupLine($"Creating Gondwana WinForms project: [bold]{Markup.Escape(settings.Name)}[/]");

        var exitCode = ProcessHelper.RunLive("dotnet", args);

        if (exitCode == 0)
        {
            AnsiConsole.MarkupLine($"[green]Project '{Markup.Escape(settings.Name)}' created successfully.[/]");
            AnsiConsole.MarkupLine($"[dim]cd {Markup.Escape(settings.Output ?? settings.Name)} && dotnet run[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Project creation failed. Is the Gondwana template installed?[/]");
            AnsiConsole.MarkupLine("[dim]Run: gondwana templates install[/]");
            if (backbuffer is not null)
                AnsiConsole.MarkupLine("[dim]Tip: if templates are already installed, the installed version may be too old to support --backbuffer. Run: gondwana templates update[/]");
        }

        return exitCode;
    }
}
