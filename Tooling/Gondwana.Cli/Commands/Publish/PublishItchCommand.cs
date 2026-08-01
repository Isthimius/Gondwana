using System.ComponentModel;
using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Publish;

internal sealed class PublishItchCommand : Command<PublishItchCommand.Settings>
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

        [CommandOption("-o|--output")]
        [Description("Output zip path. Defaults to bin/<Configuration>/<TargetFramework>/publish/<ProjectName>-itch.zip.")]
        public string? Output { get; init; }

        [CommandOption("--skip-build")]
        [Description("Skip the dotnet publish step and package an existing Blazor publish output.")]
        public bool SkipBuild { get; init; }

        [CommandOption("--skip-workload")]
        [Description("Skip 'dotnet workload install wasm-tools' during the publish step.")]
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

        var projectName = Path.GetFileNameWithoutExtension(csprojPath!);
        AnsiConsole.MarkupLine($"Packaging [bold]{Markup.Escape(projectName)}[/] for itch.io...");

        var zipPath = ProjectHelper.CreateBlazorItchPackage(csprojPath!, settings.Configuration, settings.SkipBuild, settings.SkipWorkload, settings.Output, out var exitCode);
        if (exitCode != 0)
            return exitCode;

        if (zipPath is null)
        {
            AnsiConsole.MarkupLine("[red]Blazor publish wwwroot not found.[/]");
            AnsiConsole.MarkupLine("[dim]Run without --skip-build or publish the Blazor project first.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine("[green]Itch package created![/]");
        Console.WriteLine(zipPath);
        AnsiConsole.MarkupLine("[dim]Use 'gondwana deploy itch' to upload it with butler.[/]");
        return 0;
    }
}
