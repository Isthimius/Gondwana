using System.ComponentModel;
using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Publish;

internal sealed class PublishBlazorCommand : Command<PublishBlazorCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--project")]
        [Description("Path to the .csproj file or directory containing a single .csproj. Defaults to the current directory.")]
        public string? Project { get; init; }

        [CommandOption("-c|--configuration")]
        [Description("Build configuration. Defaults to 'Release'.")]
        [DefaultValue("Release")]
        public string Configuration { get; init; } = "Release";

        [CommandOption("--skip-workload")]
        [Description("Skip 'dotnet workload install wasm-tools'. Use when the workload is already installed.")]
        public bool SkipWorkload { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!ProjectHelper.TryResolveProject(settings.Project, out var csprojPath, out var error))
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(error!)}[/]");
            if (error!.Contains("No .csproj found", StringComparison.Ordinal))
                AnsiConsole.MarkupLine("[dim]Pass -p|--project <path-to.csproj> to specify the project.[/]");
            return 1;
        }

        if (!ProjectHelper.IsBlazorWebAssemblyProject(csprojPath!))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] This project does not appear to be a Blazor WebAssembly project.");
            AnsiConsole.MarkupLine("[dim]Expected Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" or a Gondwana.Blazor package/project reference. Continuing anyway...[/]");
        }

        var projectName = Path.GetFileNameWithoutExtension(csprojPath);
        AnsiConsole.MarkupLine($"Publishing [bold]{Markup.Escape(projectName!)}[/] for Blazor WebAssembly...");

        var publishExit = ProjectHelper.PublishBlazorProject(csprojPath!, settings.Configuration, settings.SkipWorkload, out var wwwroot);
        if (publishExit != 0)
            return publishExit;

        if (wwwroot is not null && Directory.Exists(wwwroot))
        {
            AnsiConsole.MarkupLine("[green]Publish complete.[/]");
            Console.WriteLine(wwwroot);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Publish succeeded but wwwroot output could not be located.");
            AnsiConsole.MarkupLine("[dim]Expected output at: bin/<Configuration>/net8.0/publish/wwwroot/[/]");
        }

        return 0;
    }
}
