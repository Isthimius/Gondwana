using System.ComponentModel;
using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Publish;

internal sealed class PublishWasmCommand : Command<PublishWasmCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("-p|--project")]
        [Description("Path to the .csproj file or directory containing a single .csproj. Defaults to the current directory.")]
        public string? Project { get; init; }

        [CommandOption("-c|--configuration")]
        [Description("Build configuration. Defaults to 'Release'.")]
        public string Configuration { get; init; } = "Release";

        [CommandOption("--skip-workload")]
        [Description("Skip 'dotnet workload install wasm-tools'. Use when the workload is already installed.")]
        public bool SkipWorkload { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Resolve project path
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

        // Check this project actually targets net8.0-browser
        var csprojContent = File.ReadAllText(csprojPath);
        if (!csprojContent.Contains("net8.0-browser", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] This project does not appear to target [bold]net8.0-browser[/].");
            AnsiConsole.MarkupLine("[dim]Make sure <TargetFrameworks> includes 'net8.0-browser'. Continuing anyway...[/]");
        }

        AnsiConsole.MarkupLine($"Publishing [bold]{Markup.Escape(Path.GetFileNameWithoutExtension(csprojPath))}[/] for WASM...");

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

        // 2. Publish for net8.0-browser
        var publishArgs = new List<string>
        {
            "publish", csprojPath,
            "-f", "net8.0-browser",
            "-c", settings.Configuration
        };

        var exitCode = ProcessHelper.RunLive("dotnet", publishArgs);

        if (exitCode != 0)
        {
            AnsiConsole.MarkupLine("[red]dotnet publish failed.[/]");
            return exitCode;
        }

        // 3. Locate and report AppBundle
        var projectDir = Path.GetDirectoryName(csprojPath)!;
        var appBundle = Path.Combine(projectDir, "bin", settings.Configuration, "net8.0-browser", "browser-wasm", "AppBundle");

        if (!Directory.Exists(appBundle))
        {
            // Try to find it elsewhere
            var binDir = Path.Combine(projectDir, "bin");
            if (Directory.Exists(binDir))
            {
                var candidate = Directory.GetDirectories(binDir, "AppBundle", SearchOption.AllDirectories)
                                         .FirstOrDefault();
                if (candidate is not null)
                    appBundle = candidate;
            }
        }

        AnsiConsole.MarkupLine("[green]Publish succeeded![/]");
        if (Directory.Exists(appBundle))
        {
            AnsiConsole.MarkupLine($"[dim]AppBundle: {Markup.Escape(appBundle)}[/]");
            AnsiConsole.MarkupLine("[dim]To deploy to itch.io, use scripts/Deploy-Gondwana-Itch.ps1[/]");
            AnsiConsole.MarkupLine("[dim]To deploy to a website, use scripts/Deploy-Gondwana-Website.ps1[/]");
        }

        return 0;
    }
}
