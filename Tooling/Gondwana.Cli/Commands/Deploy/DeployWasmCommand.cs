using System.ComponentModel;
using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Deploy;

internal sealed class DeployWasmCommand : Command<DeployWasmCommand.Settings>
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

        [CommandOption("--web-root")]
        [Description("Local destination directory for the AppBundle contents.")]
        public string? WebRoot { get; init; }

        [CommandOption("--remote-host")]
        [Description("SSH remote in the form user@host, used with --remote-path.")]
        public string? RemoteHost { get; init; }

        [CommandOption("--remote-path")]
        [Description("Remote destination path, used with --remote-host.")]
        public string? RemotePath { get; init; }

        [CommandOption("--skip-build")]
        [Description("Skip the dotnet publish step and deploy an existing AppBundle.")]
        public bool SkipBuild { get; init; }

        [CommandOption("--skip-workload")]
        [Description("Skip 'dotnet workload install wasm-tools' during the publish step.")]
        public bool SkipWorkload { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var useLocal = !string.IsNullOrWhiteSpace(settings.WebRoot);
        var useRemote = !string.IsNullOrWhiteSpace(settings.RemoteHost) && !string.IsNullOrWhiteSpace(settings.RemotePath);

        if (!useLocal && !useRemote)
        {
            AnsiConsole.MarkupLine("[red]Specify a deployment target: --web-root <path> or --remote-host <user@host> --remote-path <path>[/]");
            return 1;
        }

        if (useLocal && useRemote)
        {
            AnsiConsole.MarkupLine("[red]Specify either --web-root or --remote-host/--remote-path, not both.[/]");
            return 1;
        }

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

        if (useLocal)
        {
            var webRoot = Path.GetFullPath(settings.WebRoot!);
            AnsiConsole.MarkupLine($"Deploying to local web root [bold]{Markup.Escape(webRoot)}[/]...");
            ProjectHelper.MirrorDirectory(appBundle, webRoot);
            AnsiConsole.MarkupLine("[green]Deployed to local web root![/]");
            Console.WriteLine(webRoot);
        }
        else
        {
            var rsyncCheck = ProcessHelper.Run("rsync", "--version", out var rsyncExit);
            if (rsyncExit != 0)
            {
                AnsiConsole.MarkupLine("[red]rsync not found on PATH.[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"Deploying to [bold]{Markup.Escape(settings.RemoteHost!)}:{Markup.Escape(settings.RemotePath!)}[/] via rsync...");
            var deployExit = ProcessHelper.RunLive("rsync", new[]
            {
                "-avz",
                "--delete",
                appBundle + Path.DirectorySeparatorChar,
                $"{settings.RemoteHost}:{settings.RemotePath!.TrimEnd('/', '\\')}/"
            });

            if (deployExit != 0)
            {
                AnsiConsole.MarkupLine("[red]rsync failed.[/]");
                return deployExit;
            }

            AnsiConsole.MarkupLine("[green]Remote deploy succeeded![/]");
            Console.WriteLine($"{settings.RemoteHost}:{settings.RemotePath!.TrimEnd('/', '\\')}/");
        }

        AnsiConsole.MarkupLine("[yellow]Reminder:[/] your web server must send these headers for .NET WASM threading to work:");
        AnsiConsole.MarkupLine("[dim]  Cross-Origin-Opener-Policy:   same-origin[/]");
        AnsiConsole.MarkupLine("[dim]  Cross-Origin-Embedder-Policy: require-corp[/]");
        AnsiConsole.MarkupLine("[dim]The site must also be served over HTTPS.[/]");
        return 0;
    }
}
