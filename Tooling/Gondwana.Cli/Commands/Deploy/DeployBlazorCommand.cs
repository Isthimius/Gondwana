using System.ComponentModel;
using Gondwana.Cli.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Deploy;

internal sealed class DeployBlazorCommand : Command<DeployBlazorCommand.Settings>
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
        [Description("Local destination directory for the published wwwroot contents.")]
        public string? WebRoot { get; init; }

        [CommandOption("--remote-host")]
        [Description("SSH remote in the form user@host, used with --remote-path.")]
        public string? RemoteHost { get; init; }

        [CommandOption("--remote-path")]
        [Description("Remote destination path, used with --remote-host.")]
        public string? RemotePath { get; init; }

        [CommandOption("--skip-build")]
        [Description("Skip the dotnet publish step and deploy an existing publish output.")]
        public bool SkipBuild { get; init; }

        [CommandOption("--skip-workload")]
        [Description("Skip 'dotnet workload install wasm-tools' during the publish step.")]
        public bool SkipWorkload { get; init; }

        [CommandOption("--no-mirror")]
        [Description("Do not remove stale files from the destination (no mirroring). By default the destination is mirrored (stale files are deleted).")]
        public bool NoMirror { get; init; }
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

        if (!ProjectHelper.IsBlazorWebAssemblyProject(csprojPath!))
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] This project does not appear to be a Blazor WebAssembly project.");
            AnsiConsole.MarkupLine("[dim]Expected Sdk=\"Microsoft.NET.Sdk.BlazorWebAssembly\" or a Gondwana.Blazor package/project reference. Continuing anyway...[/]");
        }

        var publishOutput = ProjectHelper.TryGetBlazorPublishRoot(csprojPath!, settings.Configuration, settings.SkipBuild, settings.SkipWorkload, out var exitCode);
        if (exitCode != 0)
            return exitCode;

        if (publishOutput is null || !Directory.Exists(publishOutput))
        {
            AnsiConsole.MarkupLine("[red]Blazor publish wwwroot not found.[/]");
            AnsiConsole.MarkupLine("[dim]Run without --skip-build or publish the Blazor project first.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[dim]Publish output: {Markup.Escape(publishOutput)}[/]");

        if (useLocal)
        {
            if (settings.NoMirror)
            {
                AnsiConsole.MarkupLine($"[dim]Copying to local web root: {Markup.Escape(settings.WebRoot!)}[/]");
                Directory.CreateDirectory(settings.WebRoot!);

                foreach (var file in Directory.GetFiles(publishOutput, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(publishOutput, file);
                    var destPath = Path.Combine(settings.WebRoot!, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(file, destPath, overwrite: true);
                }
            }
            else
            {
                AnsiConsole.MarkupLine($"[dim]Mirroring to local web root: {Markup.Escape(settings.WebRoot!)}[/]");
                ProjectHelper.MirrorDirectory(publishOutput, settings.WebRoot!);
            }

            AnsiConsole.MarkupLine("[green]Deployment complete.[/]");
            PrintServerReminder();
            return 0;
        }

        if (useRemote)
        {
            ProcessHelper.Run("rsync", "--version", out var rsyncCheckExit);
            if (rsyncCheckExit != 0)
            {
                AnsiConsole.MarkupLine("[red]rsync not found on PATH.[/]");
                AnsiConsole.MarkupLine("[dim]Install rsync (available on Linux/macOS natively, or via WSL / Git Bash on Windows).[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[dim]Deploying to {Markup.Escape(settings.RemoteHost!)}:{Markup.Escape(settings.RemotePath!)}[/]");

            var rsyncArgs = settings.NoMirror
                ? new[] { "-avz", $"{publishOutput}/", $"{settings.RemoteHost}:{settings.RemotePath}/" }
                : new[] { "-avz", "--delete", $"{publishOutput}/", $"{settings.RemoteHost}:{settings.RemotePath}/" };

            var rsyncExit = ProcessHelper.RunLive("rsync", rsyncArgs);

            if (rsyncExit == 0)
            {
                AnsiConsole.MarkupLine("[green]Deployment complete.[/]");
                PrintServerReminder();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]rsync deployment failed.[/]");
            }

            return rsyncExit;
        }

        return 0;
    }

    private static void PrintServerReminder()
    {
        AnsiConsole.MarkupLine("[yellow]Reminder:[/] your web server must send these headers on every request for .NET WASM threading to work:");
        AnsiConsole.MarkupLine("  [dim]Cross-Origin-Opener-Policy:   same-origin[/]");
        AnsiConsole.MarkupLine("  [dim]Cross-Origin-Embedder-Policy: require-corp[/]");
        AnsiConsole.MarkupLine("[yellow]The site must also be served over HTTPS.[/]");
    }
}