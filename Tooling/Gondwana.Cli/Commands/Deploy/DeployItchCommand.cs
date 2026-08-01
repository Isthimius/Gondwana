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
        [Description("Skip the dotnet publish step and deploy an existing Blazor publish output.")]
        public bool SkipBuild { get; init; }

        [CommandOption("--skip-workload")]
        [Description("Skip 'dotnet workload install wasm-tools' during the publish step.")]
        public bool SkipWorkload { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(ItchGame))
                return ValidationResult.Error("--itch-game is required.");

            var parts = ItchGame.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length == 2
                ? ValidationResult.Success()
                : ValidationResult.Error("--itch-game must be in the form 'user/game'.");
        }
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

        var butlerCheck = ProcessHelper.Run("butler", "--version", out var butlerExit);
        if (butlerExit != 0)
        {
            AnsiConsole.MarkupLine("[red]butler not found on PATH.[/]");
            AnsiConsole.MarkupLine("[dim]Install it from https://itch.io/docs/butler/ and run 'butler login'.[/]");
            return 1;
        }

        var zipPath = Path.Combine(Path.GetTempPath(), $"gondwana-blazor-{Path.GetRandomFileName()}.zip");
        var createdZipPath = ProjectHelper.CreateBlazorItchPackage(csprojPath!, settings.Configuration, settings.SkipBuild, settings.SkipWorkload, zipPath, out var exitCode);
        if (exitCode != 0)
            return exitCode;

        if (createdZipPath is null)
        {
            AnsiConsole.MarkupLine("[red]Blazor publish wwwroot not found.[/]");
            AnsiConsole.MarkupLine("[dim]Run without --skip-build or publish the Blazor project first.[/]");
            return 1;
        }

        try
        {
            AnsiConsole.MarkupLine($"Uploading [bold]{Markup.Escape(settings.ItchGame!)}[/] to channel [bold]{Markup.Escape(settings.ItchChannel)}[/]...");

            var pushExit = ProcessHelper.RunLive("butler", new[]
            {
                "push",
                createdZipPath,
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
        var itchParts = settings.ItchGame!.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Console.WriteLine($"https://{itchParts[0]}.itch.io/{itchParts[1]}");
        return 0;
    }
}
