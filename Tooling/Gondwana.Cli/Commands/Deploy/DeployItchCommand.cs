using System.ComponentModel;
using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Deploy;

internal sealed class DeployItchCommand : Command<DeployItchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--project")]
        [Description("Path to the .csproj file or directory containing a single .csproj. Defaults to the current directory.")]
        public string? Project { get; init; }

        [CommandOption("--itch-game")]
        [Description("The itch.io game slug in the form 'user/game'.")]
        public string? ItchGame { get; init; }

        [CommandOption("--itch-channel")]
        [Description("The itch.io release channel name. Defaults to 'html5'.")]
        [DefaultValue("html5")]
        public string ItchChannel { get; init; } = "html5";

        [CommandOption("-c|--configuration")]
        [Description("Build configuration. Defaults to 'Release'.")]
        [DefaultValue("Release")]
        public string Configuration { get; init; } = "Release";

        [CommandOption("--skip-build")]
        [Description("Skip the dotnet publish step and deploy an existing AppBundle.")]
        public bool SkipBuild { get; init; }

        [CommandOption("--skip-workload")]
        [Description("Skip 'dotnet workload install wasm-tools' during the publish step.")]
        public bool SkipWorkload { get; init; }

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(ItchGame)
                ? ValidationResult.Error("--itch-game is required.")
                : ValidationResult.Success();
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

        var butlerCheck = ProcessHelper.Run("butler", "--version", out var butlerExit);
        if (butlerExit != 0)
        {
            AnsiConsole.MarkupLine("[red]butler not found on PATH.[/]");
            AnsiConsole.MarkupLine("[dim]Install it from https://itch.io/docs/butler/ and run 'butler login'.[/]");
            return 1;
        }

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

        var zipPath = Path.Combine(Path.GetTempPath(), $"gondwana-wasm-{Path.GetRandomFileName()}.zip");

        try
        {
            ProjectHelper.CreateZipFromDirectoryContents(appBundle, zipPath);
            AnsiConsole.MarkupLine($"Uploading [bold]{Markup.Escape(settings.ItchGame!)}[/] to channel [bold]{Markup.Escape(settings.ItchChannel)}[/]...");

            var pushExit = ProcessHelper.RunLive("butler", new[]
            {
                "push",
                zipPath,
                $"{settings.ItchGame}:{settings.ItchChannel}"
            });

            if (pushExit != 0)
            {
                AnsiConsole.MarkupLine("[red]butler push failed.[/]");
                return pushExit;
            }
        }
        finally
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }

        AnsiConsole.MarkupLine("[green]Deployed to itch.io![/]");
        Console.WriteLine($"https://{settings.ItchGame!.Split('/')[0]}.itch.io/{settings.ItchGame.Split('/')[1]}");
        return 0;
    }
}
