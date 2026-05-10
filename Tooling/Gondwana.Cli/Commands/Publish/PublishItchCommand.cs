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
        [Description("Output zip path. Defaults to bin/<Configuration>/net8.0-browser/browser-wasm/<ProjectName>-itch.zip.")]
        public string? Output { get; init; }

        [CommandOption("--skip-build")]
        [Description("Skip the dotnet publish step and package an existing AppBundle.")]
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

        if (!ProjectHelper.TargetsFramework(csprojPath!, "net8.0-browser"))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] This project does not appear to target [bold]net8.0-browser[/].");
            AnsiConsole.MarkupLine("[dim]Make sure <TargetFrameworks> includes 'net8.0-browser'. Continuing anyway...[/]");
        }

        var projectName = Path.GetFileNameWithoutExtension(csprojPath);
        AnsiConsole.MarkupLine($"Packaging [bold]{Markup.Escape(projectName)}[/] for itch.io...");

        if (!settings.SkipBuild)
        {
            if (!settings.SkipWorkload)
            {
                AnsiConsole.MarkupLine("[dim]Installing wasm-tools workload...[/]");
                var workloadExit = ProcessHelper.RunLive("dotnet", "workload install wasm-tools");
                if (workloadExit != 0)
                {
                    AnsiConsole.MarkupLine("[red]dotnet workload install wasm-tools failed.[/]");
                    return workloadExit;
                }
            }

            var publishExit = ProcessHelper.RunLive("dotnet", new[]
            {
                "publish", csprojPath!, "-f", "net8.0-browser", "-c", settings.Configuration
            });

            if (publishExit != 0)
            {
                AnsiConsole.MarkupLine("[red]dotnet publish failed.[/]");
                return publishExit;
            }
        }

        var appBundle = ProjectHelper.TryLocateAppBundle(csprojPath!, settings.Configuration);
        if (appBundle is null || !Directory.Exists(appBundle))
        {
            AnsiConsole.MarkupLine("[red]AppBundle not found.[/]");
            AnsiConsole.MarkupLine("[dim]Run without --skip-build or publish the project for net8.0-browser first.[/]");
            return 1;
        }

        var defaultZipPath = Path.Combine(Path.GetDirectoryName(appBundle)!, $"{projectName}-itch.zip");
        var zipPath = ProjectHelper.CreateZipFromDirectoryContents(appBundle, settings.Output ?? defaultZipPath);

        AnsiConsole.MarkupLine("[green]Itch package created![/]");
        Console.WriteLine(zipPath);
        AnsiConsole.MarkupLine("[dim]Use 'gondwana deploy itch' to upload it with butler.[/]");
        return 0;
    }
}
