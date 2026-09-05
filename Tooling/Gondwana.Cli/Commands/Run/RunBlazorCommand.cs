using System.ComponentModel;
using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Run;

internal sealed class RunBlazorCommand : Command<RunBlazorCommand.Settings>
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
        [Description("Browser target framework. Auto-detected when the project has a single browser target.")]
        public string? Framework { get; init; }

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

        AnsiConsole.MarkupLine($"Running [bold]{Markup.Escape(Path.GetFileNameWithoutExtension(csprojPath!))}[/] with Gondwana WebGL in the browser...");

        var workloadExit = ProjectHelper.EnsureBlazorWasmToolsInstalled(settings.SkipWorkload);
        if (workloadExit != 0)
            return workloadExit;

        AnsiConsole.MarkupLine("[dim]Starting Blazor WebAssembly dev server...[/]");
        var runArgs = new List<string>
        {
            "run",
            "--project", csprojPath!,
            "-c", settings.Configuration,
            "-f", framework!,
        };

        return ProcessHelper.RunLiveAndOpenUrl("dotnet", runArgs);
    }
}
