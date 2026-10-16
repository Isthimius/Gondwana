using System.ComponentModel;
using System.Text.Json;
using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Assets;

/// <summary>Thin preview/commit interface over the shared scope-aware move transaction.</summary>
internal sealed class AssetsMoveCommand : Command<AssetsMoveCommand.Settings>
{
    /// <summary>Explicit physical endpoints, scope, and per-archive credentials.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>Gets the source loose file or archive path.</summary>
        [CommandArgument(0, "<source>")]
        public string Source { get; init; } = "";
        /// <summary>Gets the destination loose file or archive path.</summary>
        [CommandArgument(1, "<destination>")]
        public string Destination { get; init; } = "";
        /// <summary>Gets the explicit directory or single-GAF discovery scope.</summary>
        [CommandOption("--scope")]
        public string Scope { get; init; } = "";
        /// <summary>Gets the source packed entry, or null for loose storage.</summary>
        [CommandOption("--source-entry")]
        public string? SourceEntry { get; init; }
        /// <summary>Gets the destination packed entry, or null for loose storage.</summary>
        [CommandOption("--destination-entry")]
        public string? DestinationEntry { get; init; }
        /// <summary>Gets the exact type, required for packed sources.</summary>
        [CommandOption("--type")]
        public string? Type { get; init; }
        /// <summary>Gets whether to move the complete GAF.</summary>
        [CommandOption("--archive")]
        public bool Archive { get; init; }
        /// <summary>Gets whether to execute; omitted means preview only.</summary>
        [CommandOption("--commit")]
        public bool Commit { get; init; }
        /// <summary>Gets an optional JSON object mapping absolute archive paths to their passwords.</summary>
        [CommandOption("--credentials")]
        [Description("JSON object of absolute GAF paths to passwords; never printed.")]
        public string? Credentials { get; init; }
    }

    /// <inheritdoc/>
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settings.Scope)) throw new ArgumentException("An explicit --scope directory or GAF is required.");
            var credentials = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            if (settings.Credentials is not null)
                foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(settings.Credentials)) ?? throw new InvalidDataException("Invalid credential map."))
                {
                    if (!Path.IsPathFullyQualified(pair.Key)) throw new InvalidDataException("Credential keys must be absolute archive paths.");
                    credentials.Add(Path.GetFullPath(pair.Key), pair.Value);
                }
            AssetRelocationPlan plan;
            if (settings.Archive)
            {
                if (settings.SourceEntry is not null || settings.DestinationEntry is not null || settings.Type is not null) throw new ArgumentException("--archive cannot be combined with entry/type selectors.");
                plan = AssetRelocationPlan.MoveArchive(settings.Source, settings.Destination, settings.Scope, path => credentials.GetValueOrDefault(path), cancellationToken);
            }
            else
            {
                if (settings.SourceEntry is null && AssetArchiveFormat.IsArchive(settings.Source)) throw new ArgumentException("Archive files require --archive.");
                if (settings.SourceEntry is not null && settings.Type is null) throw new ArgumentException("Packed sources require --type.");
                var type = settings.Type is null ? AssetImportPlan.InferType(settings.Source) : Enum.Parse<AssetTypes>(settings.Type, ignoreCase: true);
                plan = AssetRelocationPlan.Create(settings.Scope, [new(new(type, Path.GetFullPath(settings.Source), settings.SourceEntry),
                    new(type, Path.GetFullPath(settings.Destination), settings.DestinationEntry))], path => credentials.GetValueOrDefault(path), cancellationToken);
            }
            foreach (var line in plan.Preview) AnsiConsole.WriteLine(line);
            if (settings.Commit) { plan.Commit(cancellationToken); AnsiConsole.WriteLine("Move committed; original locations retired."); }
            else AnsiConsole.WriteLine("Preview only. Use --commit to execute after saving/closing affected editors.");
            return 0;
        }
        catch (Exception ex) { AnsiConsole.WriteLine(ex.Message); return 1; }
    }
}
