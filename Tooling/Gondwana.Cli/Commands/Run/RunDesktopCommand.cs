using System.ComponentModel;
using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Run;

internal sealed class RunDesktopCommand : Command<RunDesktopCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--project")]
        [Description("Path to the .csproj file or directory containing a single .csproj. Defaults to the current directory.")]
        public string? Project { get; init; }

        [CommandOption("-c|--configuration")]
        [Description("Build configuration. Defaults to 'Debug'.")]
        [DefaultValue("Debug")]
        public string Configuration { get; init; } = "Debug";

        [CommandOption("-f|--framework")]
        [Description("Target framework to run (e.g. 'net8.0'). Required when the project targets multiple frameworks.")]
        public string? Framework { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var projectPath = settings.Project ?? Directory.GetCurrentDirectory();

        string? csprojPath;
        if (File.Exists(projectPath) && projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            csprojPath = projectPath;
        }
        else if (Directory.Exists(projectPath))
        {
            var found = Directory.GetFiles(projectPath, "*.csproj");
            if (found.Length == 0)
            {
                AnsiConsole.MarkupLine("[red]No .csproj found in the specified directory.[/]");
                AnsiConsole.MarkupLine("[dim]Pass -p|--project <path-to.csproj> to specify the project.[/]");
                return 1;
            }
            if (found.Length > 1)
            {
                AnsiConsole.MarkupLine("[red]Multiple .csproj files found. Pass -p|--project <path-to.csproj> to specify one.[/]");
                return 1;
            }
            csprojPath = found[0];
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Project path not found: {Markup.Escape(projectPath)}[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"Running [bold]{Markup.Escape(Path.GetFileNameWithoutExtension(csprojPath))}[/]...");

        var runArgs = new List<string>
        {
            "run", "--project", csprojPath,
            "-c", settings.Configuration,
        };

        if (settings.Framework is not null)
        {
            runArgs.Add("-f");
            runArgs.Add(settings.Framework);
        }

        return ProcessHelper.RunLive("dotnet", runArgs);
    }
}
