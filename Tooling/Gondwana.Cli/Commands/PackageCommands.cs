using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands;

internal class ProjectSettings : CommandSettings
{
    [CommandOption("-p|--project <PATH>")]
    [Description("Project file or directory (default: current directory).")]
    public string? Project { get; init; }
}

internal static class ProjectCommand
{
    public static ProjectPackages Load(string? path)
    {
        if (!ProjectHelper.TryResolveProject(path, out var project, out var error)) throw new InvalidOperationException(error);
        return new ProjectPackages(project!);
    }

    public static int Fail(Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Fail:[/] {Markup.Escape(ex.Message)}");
        return 1;
    }

    public static void PrintChanges(IReadOnlyList<string> changes)
    {
        foreach (var change in changes) AnsiConsole.WriteLine(change);
        if (changes.Count == 0) AnsiConsole.WriteLine("Already up to date. No files changed.");
    }
}

internal sealed class AddCommand : Command<AddCommand.Settings>
{
    public sealed class Settings : ProjectSettings
    {
        [CommandArgument(0, "<feature>")]
        [Description("widgets, audio, midi, gamepad, video, or hosting.")]
        public string Feature { get; init; } = "";
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var project = ProjectCommand.Load(settings.Project);
            var changes = project.Add(settings.Feature);
            ProjectCommand.PrintChanges(changes);
            if (changes.Count > 0) project.Save();
            return 0;
        }
        catch (Exception ex) { return ProjectCommand.Fail(ex); }
    }
}

internal sealed class UpgradeCommand : AsyncCommand<UpgradeCommand.Settings>
{
    public sealed class Settings : ProjectSettings
    {
        [CommandOption("--version <VERSION>")]
        [Description("Exact version, including prerelease if explicitly desired. Defaults to latest common stable version on nuget.org.")]
        public string? Version { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print all changes without writing files.")]
        public bool DryRun { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var project = ProjectCommand.Load(settings.Project);
            project.EnsureEditable();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var version = settings.Version ?? await PackageVersions.LatestCommonStable(project.Packages.Select(p => p.Name), async name =>
            {
                var data = await http.GetFromJsonAsync<JsonElement>($"https://api.nuget.org/v3-flatcontainer/{name.ToLowerInvariant()}/index.json", cancellationToken);
                return data.GetProperty("versions").EnumerateArray().Select(v => v.GetString()!).ToArray();
            });
            if (settings.Version is null && project.Packages.Any(p => PackageVersions.StableNumber(p.Version!) is { } current && current > PackageVersions.StableNumber(version)))
                throw new InvalidOperationException("Latest common stable version would downgrade an existing reference. Choose --version explicitly.");
            if (settings.Version is null && project.Packages.Any(p => p.Version!.Contains('-')))
                throw new InvalidOperationException("Project uses prerelease packages. Choose --version explicitly to change release tracks.");
            var changes = project.Upgrade(version);
            ProjectCommand.PrintChanges(changes);
            if (settings.DryRun) AnsiConsole.WriteLine("No files changed (--dry-run).");
            else if (changes.Count > 0) project.Save();
            return 0;
        }
        catch (Exception ex) { return ProjectCommand.Fail(ex); }
    }
}

internal static class PackageVersions
{
    public static Version? StableNumber(string value)
    {
        if (value.Contains('-') || !Version.TryParse(value.Split('+')[0], out var version)) return null;
        return new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision));
    }

    // Network seam: tests supply a deterministic package index.
    public static async Task<string> LatestCommonStable(IEnumerable<string> names, Func<string, Task<string[]>> getVersions)
    {
        HashSet<string>? common = null;
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var versions = (await getVersions(name)).Where(v => StableNumber(v) is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (common is null) common = versions;
            else common.IntersectWith(versions);
        }
        return common?.OrderByDescending(StableNumber).FirstOrDefault()
            ?? throw new InvalidOperationException("No common stable version found on nuget.org for the referenced Gondwana packages. Use --version for a private feed or explicit version.");
    }
}
