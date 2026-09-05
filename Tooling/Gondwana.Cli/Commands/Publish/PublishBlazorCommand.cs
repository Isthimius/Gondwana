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

        [CommandOption("-f|--framework")]
        [Description("Browser target framework. Auto-detected when the project has a single browser target.")]
        public string? Framework { get; init; }

        [CommandOption("--base-href")]
        [Description("Override the published <base href> for subdirectory/static hosting, for example /games/mygame/ or ./.")]
        public string? BaseHref { get; init; }

        [CommandOption("--skip-workload")]
        [Description("Skip checking/installing the wasm-tools workload. Use when the environment is already prepared.")]
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

        if (!ProjectHelper.TryResolveBrowserFramework(csprojPath!, settings.Framework, out var framework, out error))
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(error!)}[/]");
            return 1;
        }

        var projectName = Path.GetFileNameWithoutExtension(csprojPath);
        AnsiConsole.MarkupLine($"Publishing [bold]{Markup.Escape(projectName!)}[/] for Blazor WebAssembly/WebGL ({Markup.Escape(framework!)})...");

        var publishExit = ProjectHelper.PublishBlazorProject(
            csprojPath!,
            settings.Configuration,
            framework!,
            settings.SkipWorkload,
            settings.BaseHref,
            out var wwwroot);
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
            AnsiConsole.MarkupLine($"[dim]Expected output at: bin/{Markup.Escape(settings.Configuration)}/{Markup.Escape(framework!)}/publish/wwwroot/[/]");
        }

        return 0;
    }
}
