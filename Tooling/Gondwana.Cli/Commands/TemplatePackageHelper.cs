using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace Gondwana.Cli.Commands;

internal static class TemplatePackageHelper
{
    /// <summary>
    /// The package id.
    /// </summary>
    public const string PackageId = "Gondwana.Templates";

    /// <summary>
    /// Gets the currently installed Gondwana template package version.
    /// </summary>
    /// <returns>The requested string, or <see langword="null"/> when unavailable.</returns>
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

    /// <summary>
    /// Ensures that the Gondwana project templates are installed at the requested version.
    /// </summary>
    /// <returns>Zero on success, or a nonzero template-installation exit code.</returns>
    public static int EnsureInstalledOrUpdated()
    {
        var installedVersion = GetInstalledVersion();
        return string.IsNullOrWhiteSpace(installedVersion)
            ? InstallTemplates()
            : UpdateInstalledTemplates(installedVersion);
    }

    /// <summary>
    /// Installs an explicitly selected template version, or retains the original stable-only
    /// install/update behavior when no selection is supplied.
    /// </summary>
    public static async Task<int> EnsureInstalledOrUpdatedAsync(
        bool prerelease, string? version, CancellationToken cancellationToken)
    {
        if (!prerelease && version is null)
            return EnsureInstalledOrUpdated();

        if (prerelease && version is not null)
        {
            AnsiConsole.MarkupLine("[red]Choose either --prerelease or --version, not both.[/]");
            return 1;
        }

        if (version is not null && !IsValidVersion(version))
        {
            AnsiConsole.MarkupLine("[red]Invalid template package version.[/]");
            return 1;
        }

        if (prerelease)
        {
            try
            {
                // --prerelease on dotnet new install requires SDK 11; resolve the exact
                // version ourselves so the Gondwana CLI also works with SDKs 8-10.
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var index = await http.GetFromJsonAsync<JsonElement>(
                    "https://api.nuget.org/v3-flatcontainer/gondwana.templates/index.json",
                    cancellationToken);
                version = LatestAvailableVersion(
                    index.GetProperty("versions").EnumerateArray().Select(v => v.GetString()!));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                or JsonException or InvalidOperationException)
            {
                AnsiConsole.MarkupLine($"[red]Unable to resolve the latest Gondwana.Templates version:[/] {Markup.Escape(ex.Message)}");
                return 1;
            }
        }

        return InstallTemplates(version);
    }

    private static readonly Regex ExactVersionPattern = new(
        @"^(?<core>\d+\.\d+\.\d+(?:\.\d+)?)(?:-(?<pre>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Checks that a supplied package version is an exact numeric NuGet version.
    /// </summary>
    internal static bool IsValidVersion(string? value) =>
        value is not null
        && ExactVersionPattern.Match(value) is { Success: true } match
        && Version.TryParse(match.Groups["core"].Value, out _);

    /// <summary>
    /// Selects the highest SemVer version; prereleases are included in the candidate
    /// set, but a newer stable release still takes precedence.
    /// </summary>
    internal static string LatestAvailableVersion(IEnumerable<string> versions) =>
        versions.Where(IsValidVersion)
            .OrderByDescending(v => v, Comparer<string>.Create(CompareVersions))
            .FirstOrDefault()
        ?? throw new InvalidOperationException("No valid Gondwana.Templates versions were found on nuget.org.");

    private static int CompareVersions(string? left, string? right)
    {
        var a = ExactVersionPattern.Match(left!);
        var b = ExactVersionPattern.Match(right!);
        var core = Version.Parse(a.Groups["core"].Value).CompareTo(
            Version.Parse(b.Groups["core"].Value));
        if (core != 0) return core;

        var aPre = a.Groups["pre"].Value;
        var bPre = b.Groups["pre"].Value;
        if (aPre.Length == 0 || bPre.Length == 0)
            return aPre.Length == 0 ? (bPre.Length == 0 ? 0 : 1) : -1;

        var aParts = aPre.Split('.');
        var bParts = bPre.Split('.');
        for (var i = 0; i < Math.Min(aParts.Length, bParts.Length); i++)
        {
            var aNumeric = long.TryParse(aParts[i], out var aNumber);
            var bNumeric = long.TryParse(bParts[i], out var bNumber);
            int comparison;
            if (aNumeric && bNumeric)
                comparison = aNumber.CompareTo(bNumber);
            else if (aNumeric != bNumeric)
                comparison = aNumeric ? -1 : 1;
            else
                comparison = string.Compare(aParts[i], bParts[i], StringComparison.OrdinalIgnoreCase);
            if (comparison != 0) return comparison;
        }
        return aParts.Length.CompareTo(bParts.Length);
    }

    /// <summary>
    /// Returns the external dotnet command arguments for stable or exact version installs.
    /// </summary>
    internal static string[] InstallArguments(string? version = null) =>
        version is null
            ? ["new", "install", PackageId]
            : ["new", "install", $"{PackageId}@{version}", "--force"];

    /// <summary>
    /// Updates the installed Gondwana project templates.
    /// </summary>
    /// <returns>The exit code returned by the template update command.</returns>
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

    private static int InstallTemplates(string? version = null)
    {
        AnsiConsole.MarkupLine(version is null
            ? "Installing [bold]Gondwana.Templates[/] from NuGet..."
            : $"Installing [bold]Gondwana.Templates[/] version [bold]{Markup.Escape(version)}[/] from NuGet...");
        var exitCode = ProcessHelper.RunLive("dotnet", InstallArguments(version));

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
