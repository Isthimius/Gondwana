using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Templates;

internal sealed class TemplatesInstallCommand : AsyncCommand<TemplatesInstallCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--prerelease")]
        [Description("Install the latest available Gondwana.Templates version, including prereleases.")]
        public bool Prerelease { get; init; }

        [CommandOption("--version <VERSION>")]
        [Description("Install the exact Gondwana.Templates version (stable or prerelease).")]
        public string? Version { get; init; }
    }

    /// <inheritdoc/>
    protected override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (settings.Prerelease && settings.Version is not null)
        {
            AnsiConsole.MarkupLine("[red]Choose either --prerelease or --version, not both.[/]");
            return Task.FromResult(1);
        }

        if (settings.Version is not null && !TemplatePackageHelper.IsValidVersion(settings.Version))
        {
            AnsiConsole.MarkupLine("[red]Invalid --version. Specify an exact NuGet package version, such as 2.6.2-beta.1.[/]");
            return Task.FromResult(1);
        }

        return TemplatePackageHelper.EnsureInstalledOrUpdatedAsync(settings.Prerelease, settings.Version, cancellationToken);
    }
}
