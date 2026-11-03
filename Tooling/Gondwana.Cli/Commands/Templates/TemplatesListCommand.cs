using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Templates;

internal sealed class TemplatesListCommand : Command
{
    /// <inheritdoc/>
    protected override int Execute(CommandContext context, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("Installed [bold]Gondwana[/] templates:");
        AnsiConsole.MarkupLine(FormatInstalledVersion(TemplatePackageHelper.GetInstalledVersion()));
        AnsiConsole.WriteLine();

        var exitCode = ProcessHelper.RunLive("dotnet", "new list gondwana");
        return exitCode;
    }

    /// <summary>
    /// Formats the installed template package version without confusing the
    /// legacy "installed" sentinel with an actual NuGet version.
    /// </summary>
    internal static string FormatInstalledVersion(string? version) =>
        TemplatePackageHelper.IsValidVersion(version)
            ? $"Gondwana.Templates version: [bold]{Markup.Escape(version!)}[/]"
            : "[dim]Gondwana.Templates version: unavailable[/]";
}
