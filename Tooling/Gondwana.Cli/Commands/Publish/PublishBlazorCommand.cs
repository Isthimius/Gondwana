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

        var csprojContent = File.ReadAllText(csprojPath!);
        if (!csprojContent.Contains("Microsoft.NET.Sdk.BlazorWebAssembly", StringComparison.OrdinalIgnoreCase) &&
            !csprojContent.Contains("Gondwana.Blazor", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] This project does not appear to be a Blazor WebAssembly project.");
            AnsiConsole.MarkupLine("[dim]Expected Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" or Gondwana.Blazor package reference. Continuing anyway...[/]");
        }

        var projectName = Path.GetFileNameWithoutExtension(csprojPath);
        AnsiConsole.MarkupLine($"Publishing [bold]{Markup.Escape(projectName!)}[/] for Blazor WebAssembly...");

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

        AnsiConsole.MarkupLine($"[dim]Publishing in {settings.Configuration} configuration...[/]");
        var publishExit = ProcessHelper.RunLive("dotnet", new[]
        {
            "publish", csprojPath!, "-c", settings.Configuration
        });

        if (publishExit != 0)
        {
            AnsiConsole.MarkupLine("[red]dotnet publish failed.[/]");
            return publishExit;
        }

        var wwwroot = ProjectHelper.TryLocateBlazorPublishRoot(csprojPath!, settings.Configuration);
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
