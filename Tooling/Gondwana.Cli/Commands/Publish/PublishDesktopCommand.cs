using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Publish;

internal sealed class PublishDesktopCommand : Command<PublishDesktopCommand.Settings>
{
    /// <summary>
    /// Defines command-line settings for PublishDesktopCommand.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the project.
        /// </summary>
        [CommandOption("-p|--project")]
        [Description("Path to the .csproj file or directory containing a single .csproj. Defaults to the current directory.")]
        public string? Project { get; init; }

        /// <summary>
        /// Gets or sets the configuration.
        /// </summary>
        [CommandOption("-c|--configuration")]
        [Description("Build configuration. Defaults to 'Release'.")]
        [DefaultValue("Release")]
        public string Configuration { get; init; } = "Release";

        /// <summary>
        /// Gets or sets the framework.
        /// </summary>
        [CommandOption("-f|--framework")]
        [Description("Desktop target framework to publish (for example 'net8.0' or 'net8.0-windows').")]
        public string? Framework { get; init; }

        /// <summary>
        /// Gets or sets the runtime.
        /// </summary>
        [CommandOption("-r|--runtime")]
        [Description("Runtime identifier to publish for (for example 'win-x64', 'linux-x64', 'osx-arm64').")]
        public string? Runtime { get; init; }

        /// <summary>
        /// Gets or sets the output.
        /// </summary>
        [CommandOption("-o|--output")]
        [Description("Publish output directory. If omitted, the default dotnet publish location is used.")]
        public string? Output { get; init; }

        /// <summary>
        /// Gets or sets whether self contained is enabled.
        /// </summary>
        [CommandOption("--self-contained")]
        [Description("Publish as self-contained.")]
        public bool SelfContained { get; init; }

        /// <summary>
        /// Gets or sets whether publish single file is enabled.
        /// </summary>
        [CommandOption("--publish-single-file")]
        [Description("Publish as a single-file executable.")]
        public bool PublishSingleFile { get; init; }
    }

    /// <inheritdoc/>
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!ProjectHelper.TryResolveProject(settings.Project, out var csprojPath, out var error))
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(error!)}[/]");
            if (error!.Contains("No .csproj found", StringComparison.Ordinal))
                AnsiConsole.MarkupLine("[dim]Pass -p|--project <path-to.csproj> to specify the project.[/]");
            return 1;
        }

        if (!ProjectHelper.TryResolveDesktopFramework(csprojPath!, settings.Framework, out var framework, out error))
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(error!)}[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"Publishing [bold]{Markup.Escape(Path.GetFileNameWithoutExtension(csprojPath))}[/] for [bold]{Markup.Escape(framework!)}[/]...");

        var publishArgs = new List<string>
        {
            "publish", csprojPath!,
            "-c", settings.Configuration,
            "-f", framework!
        };

        if (!string.IsNullOrWhiteSpace(settings.Runtime))
        {
            publishArgs.Add("-r");
            publishArgs.Add(settings.Runtime);
        }

        if (!string.IsNullOrWhiteSpace(settings.Output))
        {
            publishArgs.Add("-o");
            publishArgs.Add(settings.Output);
        }

        if (settings.SelfContained)
            publishArgs.Add("--self-contained");

        if (settings.PublishSingleFile)
            publishArgs.Add("/p:PublishSingleFile=true");

        var exitCode = ProcessHelper.RunLive("dotnet", publishArgs);
        if (exitCode != 0)
        {
            AnsiConsole.MarkupLine("[red]dotnet publish failed.[/]");
            return exitCode;
        }

        var publishDirectory = !string.IsNullOrWhiteSpace(settings.Output)
            ? Path.GetFullPath(settings.Output)
            : ProjectHelper.TryLocatePublishDirectory(csprojPath!, settings.Configuration, framework!, settings.Runtime);

        AnsiConsole.MarkupLine("[green]Publish succeeded![/]");
        if (publishDirectory is not null && Directory.Exists(publishDirectory))
        {
            Console.WriteLine(publishDirectory);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Could not locate publish output directory. Check the publish output above.");
        }

        return 0;
    }
}
