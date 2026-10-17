using System.Text.Json;
using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Assets;

/// <summary>Previews scoped deletion by default; committing known dangling references requires explicit force.</summary>
internal sealed class AssetsDeleteCommand : Command<AssetsDeleteCommand.Settings>
{
    /// <summary>Physical deletion target, explicit scope, and authorizations.</summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>Gets a loose file or archive path.</summary>
        [CommandArgument(0, "<source>")]
        public string Source { get; init; } = "";
        /// <summary>Gets the explicit directory or single-archive scope.</summary>
        [CommandOption("--scope")]
        public string Scope { get; init; } = "";
        /// <summary>Gets an exact typed packed entry, or null for a loose file.</summary>
        [CommandOption("--source-entry")]
        public string? SourceEntry { get; init; }
        /// <summary>Gets the asset type; required for packed entries.</summary>
        [CommandOption("--type")]
        public string? Type { get; init; }
        /// <summary>Gets whether the entire archive and all its entries are selected.</summary>
        [CommandOption("--archive")]
        public bool Archive { get; init; }
        /// <summary>Gets permission to execute rather than preview.</summary>
        [CommandOption("--commit")]
        public bool Commit { get; init; }
        /// <summary>Gets explicit permission to break the reviewed surviving relationships.</summary>
        [CommandOption("--force")]
        public bool Force { get; init; }
        /// <summary>Gets a JSON object mapping absolute archive paths to individual passwords.</summary>
        [CommandOption("--credentials")]
        public string? Credentials { get; init; }
    }

    /// <inheritdoc/>
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(settings.Scope)) throw new ArgumentException("An explicit --scope directory or archive is required.");
            var credentials = new Dictionary<string, string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            if (settings.Credentials is not null)
                foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(settings.Credentials)) ?? throw new InvalidDataException("Invalid credential map."))
                {
                    if (!Path.IsPathFullyQualified(pair.Key)) throw new InvalidDataException("Credential keys must be absolute archive paths.");
                    credentials.Add(Path.GetFullPath(pair.Key), pair.Value);
                }
            AssetDeletionPlan plan;
            if (settings.Archive)
            {
                if (settings.SourceEntry is not null || settings.Type is not null) throw new ArgumentException("--archive cannot be combined with entry/type selectors.");
                plan = AssetDeletionPlan.Create(settings.Scope, [], [settings.Source], path => credentials.GetValueOrDefault(path), cancellationToken);
            }
            else
            {
                if (settings.SourceEntry is null && AssetArchiveFormat.IsArchive(settings.Source)) throw new ArgumentException("Archive files require --archive or a typed --source-entry.");
                if (settings.SourceEntry is not null && settings.Type is null) throw new ArgumentException("Packed targets require --type.");
                var type = settings.Type is null ? AssetImportPlan.InferType(settings.Source) : Enum.Parse<AssetTypes>(settings.Type, ignoreCase: true);
                plan = AssetDeletionPlan.Create(settings.Scope, [new(type, Path.GetFullPath(settings.Source), settings.SourceEntry)],
                    credentials: path => credentials.GetValueOrDefault(path), cancellation: cancellationToken);
            }
            foreach (var line in plan.Preview) AnsiConsole.WriteLine(line);
            if (plan.ForceRequired && !settings.Force) return 2;
            if (settings.Commit) { plan.Commit(settings.Force, cancellationToken); AnsiConsole.WriteLine("Deletion committed. Surviving documents were not rewritten."); }
            else AnsiConsole.WriteLine("Preview only; --force alone never deletes content. Use --commit after reviewing.");
            return 0;
        }
        catch (Exception ex) { AnsiConsole.WriteLine(ex.Message); return 1; }
    }
}
