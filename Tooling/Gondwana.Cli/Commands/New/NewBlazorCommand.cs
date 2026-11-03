using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.New;

internal sealed class NewBlazorCommand : Command<NewBlazorCommand.Settings>
{
    /// <summary>
    /// Defines command-line settings for NewBlazorCommand.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        [CommandArgument(0, "<name>")]
        [Description("The name of the new project.")]
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the output.
        /// </summary>
        [CommandOption("-o|--output")]
        [Description("The directory to place the generated output in. Defaults to a new folder named <name> in the current directory.")]
        public string? Output { get; init; }

        /// <summary>
        /// Gets or sets whether to skip the WebAssembly workload preflight.
        /// </summary>
        [CommandOption("--skip-workload")]
        [Description("Generate the project without checking or installing the wasm-tools workload.")]
        public bool SkipWorkload { get; init; }
    }

    /// <inheritdoc/>
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            AnsiConsole.MarkupLine("[red]Project name is required.[/]");
            return 1;
        }

        // Resolve the SDK from the intended project directory, not the caller's shell.
        // If the output does not yet exist, its nearest existing ancestor has the
        // global.json scope that will apply when the project is created.
        string outputDirectory;
        string sdkWorkingDirectory;
        try
        {
            outputDirectory = Path.GetFullPath(settings.Output ?? settings.Name);
            sdkWorkingDirectory = BlazorScaffoldPrerequisites.ResolveSdkWorkingDirectory(outputDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            AnsiConsole.MarkupLine($"[red]Invalid project output directory: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        var sdkVersion = ProcessHelper.Run("dotnet", "--version", out var sdkExitCode, sdkWorkingDirectory);
        var prerequisites = BlazorScaffoldPrerequisites.Prepare(
            sdkVersion,
            sdkExitCode,
            settings.SkipWorkload,
            checkWorkload: () =>
            {
                var success = ProjectHelper.TryGetInstalledWorkloadVersion("wasm-tools", out var version, out var error, sdkWorkingDirectory);
                return new BlazorWorkloadProbe(success, version, error);
            },
            confirmInstall: () => AnsiConsole.Confirm(
                "The .NET 10 wasm-tools workload is missing or incompatible. Install it now?", defaultValue: true),
            installWorkload: () => ProcessHelper.RunLive("dotnet", new[] { "workload", "install", "wasm-tools" }, sdkWorkingDirectory),
            canPrompt: !Console.IsInputRedirected && !Console.IsOutputRedirected);

        if (!prerequisites.CanCreate)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(prerequisites.Message)}[/]");
            return 1;
        }

        AnsiConsole.MarkupLine(prerequisites.WorkloadReady
            ? $"[green]{Markup.Escape(prerequisites.Message)}[/]"
            : $"[yellow]{Markup.Escape(prerequisites.Message)}[/]");

        // Use an absolute -o so changing the child process working directory does not
        // reinterpret a relative --output path (or the default project directory).
        var args = new List<string> { "new", "gondwana-blazor", "-n", settings.Name, "-o", outputDirectory };

        AnsiConsole.MarkupLine($"Creating Gondwana Blazor WebAssembly/WebGL project: [bold]{Markup.Escape(settings.Name)}[/]");

        var exitCode = ProcessHelper.RunLive("dotnet", args, sdkWorkingDirectory);

        if (exitCode == 0)
        {
            NewSolutionHelper.CreateHoldingSolution(settings.Name, outputDirectory, outputDirectory);
            AnsiConsole.MarkupLine($"[green]Project '{Markup.Escape(settings.Name)}' created successfully.[/]");
            if (prerequisites.WorkloadReady)
                AnsiConsole.MarkupLine("[green]Blazor environment ready: .NET 10 SDK and wasm-tools verified.[/]");
            else
                AnsiConsole.MarkupLine("[yellow]Blazor environment not fully verified: install wasm-tools before building.[/]");
            AnsiConsole.MarkupLine($"[dim]Run: cd {Markup.Escape(settings.Output ?? settings.Name)} && gondwana run blazor[/]");
            AnsiConsole.MarkupLine($"[dim]Publish: cd {Markup.Escape(settings.Output ?? settings.Name)} && gondwana publish blazor[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Project creation failed. Is the Gondwana template installed?[/]");
            AnsiConsole.MarkupLine("[dim]Run: gondwana templates install[/]");
        }

        return exitCode;
    }
}
