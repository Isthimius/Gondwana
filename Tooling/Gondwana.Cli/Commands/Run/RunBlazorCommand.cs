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

        [CommandOption("--skip-workload")]
        [Description("Skip 'dotnet workload install wasm-tools'. Use when the workload is already installed.")]
        public bool SkipWorkload { get; init; }
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

        // Check if this is a Blazor WebAssembly project
        var csprojContent = File.ReadAllText(csprojPath);
        if (!csprojContent.Contains("Microsoft.NET.Sdk.BlazorWebAssembly", StringComparison.OrdinalIgnoreCase) &&
            !csprojContent.Contains("Gondwana.Blazor", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] This project does not appear to be a Blazor WebAssembly project.");
            AnsiConsole.MarkupLine("[dim]Expected Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" or Gondwana.Blazor package reference. Continuing anyway...[/]");
        }

        AnsiConsole.MarkupLine($"Running [bold]{Markup.Escape(Path.GetFileNameWithoutExtension(csprojPath))}[/] in the browser...");

        // 1. Install wasm-tools workload
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

        // 2. Run the Blazor dev server
        AnsiConsole.MarkupLine("[dim]Starting Blazor WebAssembly dev server...[/]");
        var runArgs = new List<string>
        {
            "run",
            "--project", csprojPath,
            "-c", settings.Configuration,
        };

        return ProcessHelper.RunLive("dotnet", runArgs);
    }
}