using Spectre.Console;

namespace Gondwana.Cli.Commands;

internal static class TemplatePackageHelper
{
    public const string PackageId = "Gondwana.Templates";

    public static string? GetInstalledVersion()
    {
        var output = ProcessHelper.Run("dotnet", "new uninstall", out var exitCode);
        if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            return null;

        var lines = output.Replace("\r", string.Empty).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!string.Equals(lines[i].Trim(), PackageId, StringComparison.OrdinalIgnoreCase))
                continue;

            for (var j = i + 1; j < lines.Length; j++)
            {
                var line = lines[j];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmed = line.Trim();
                if (trimmed.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                    return trimmed["Version:".Length..].Trim();

                if (!char.IsWhiteSpace(line[0]))
                    break;
            }

            return "installed";
        }

        return null;
    }

    public static int EnsureInstalledOrUpdated()
    {
        var installedVersion = GetInstalledVersion();
        return string.IsNullOrWhiteSpace(installedVersion)
            ? InstallTemplates()
            : UpdateInstalledTemplates(installedVersion);
    }

    public static int UpdateInstalledTemplates()
    {
        var installedVersion = GetInstalledVersion();
        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            AnsiConsole.MarkupLine("[yellow]Gondwana.Templates is not installed. Run [bold]gondwana templates install[/].[/]");
            return 1;
        }

        return UpdateInstalledTemplates(installedVersion);
    }

    private static int InstallTemplates()
    {
        AnsiConsole.MarkupLine("Installing [bold]Gondwana.Templates[/] from NuGet...");
        var exitCode = ProcessHelper.RunLive("dotnet", ["new", "install", PackageId]);

        if (exitCode == 0)
        {
            var currentVersion = GetInstalledVersion();
            if (!string.IsNullOrWhiteSpace(currentVersion))
                AnsiConsole.MarkupLine($"[green]Templates installed successfully: {Markup.Escape(currentVersion)}.[/]");
            else
                AnsiConsole.MarkupLine("[green]Templates installed successfully.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Template installation failed.[/]");
        }

        return exitCode;
    }

    private static int UpdateInstalledTemplates(string installedVersion)
    {
        AnsiConsole.MarkupLine($"Checking [bold]Gondwana.Templates[/] for updates (current: {Markup.Escape(installedVersion)})...");
        var exitCode = ProcessHelper.RunLive("dotnet", ["new", "update"]);

        if (exitCode == 0)
        {
            var currentVersion = GetInstalledVersion();
            if (!string.IsNullOrWhiteSpace(currentVersion) &&
                !string.Equals(currentVersion, installedVersion, StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine($"[green]Templates updated successfully to {Markup.Escape(currentVersion)}.[/]");
            }
            else if (!string.IsNullOrWhiteSpace(currentVersion))
            {
                AnsiConsole.MarkupLine($"[green]Templates checked; current version retained: {Markup.Escape(currentVersion)}.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[green]Templates checked successfully.[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Template update failed.[/]");
        }

        return exitCode;
    }
}
