using Spectre.Console;
using System.Text.RegularExpressions;

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
            var currentLine = lines[i];
            if (!currentLine.Contains(PackageId, StringComparison.OrdinalIgnoreCase))
                continue;

            var inlineVersion = TryExtractVersion(currentLine);
            if (!string.IsNullOrWhiteSpace(inlineVersion))
                return inlineVersion;

            for (var j = i + 1; j < lines.Length; j++)
            {
                var line = lines[j];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmed = line.Trim();
                if (trimmed.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                    return trimmed["Version:".Length..].Trim();

                var nearbyVersion = TryExtractVersion(line);
                if (!string.IsNullOrWhiteSpace(nearbyVersion))
                    return nearbyVersion;

                if (!char.IsWhiteSpace(line[0]))
                    break;
            }

            return "installed";
        }

        return null;
    }

    private static string? TryExtractVersion(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        var packageVersionMatch = Regex.Match(
            line,
            $@"{Regex.Escape(PackageId)}\s*::\s*(?<version>[^\s,;]+)",
            RegexOptions.IgnoreCase);
        if (packageVersionMatch.Success)
            return packageVersionMatch.Groups["version"].Value.Trim();

        var versionLabelMatch = Regex.Match(
            line,
            @"\bVersion\s*:\s*(?<version>[^\s,;]+)",
            RegexOptions.IgnoreCase);
        if (versionLabelMatch.Success)
            return versionLabelMatch.Groups["version"].Value.Trim();

        foreach (var token in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = token.Trim().Trim(',', ';', ':');
            if (Regex.IsMatch(candidate, @"^\d+(?:\.\d+){1,3}(?:[-+][0-9A-Za-z.\-]+)?$"))
                return candidate;
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
