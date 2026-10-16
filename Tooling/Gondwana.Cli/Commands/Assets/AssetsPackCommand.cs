using System.ComponentModel;
using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Assets;

/// <summary>Packs typed assets and semantically relocated definition documents in one transaction.</summary>
internal sealed class AssetsPackCommand : Command<AssetsPackCommand.Settings>
{
    /// <summary>
    /// Defines command-line settings for AssetsPackCommand.
    /// </summary>
    public sealed class Settings : CommandSettings, IPackSettings
    {
        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        [CommandArgument(0, "<source>")]
        [Description("The source directory containing files to pack.")]
        public string Source { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the output.
        /// </summary>
        [CommandArgument(1, "<output>")]
        [Description("The output asset file path (e.g. game.assets or game.gaf).")]
        public string Output { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the default type.
        /// </summary>
        [CommandOption("-t|--type")]
        [Description("Default asset type for files whose type cannot be inferred (default: Misc).")]
        [DefaultValue("Misc")]
        public string DefaultType { get; init; } = "Misc";

        /// <summary>
        /// Gets or sets whether recurse is enabled.
        /// </summary>
        [CommandOption("-r|--recurse")]
        [Description("Recurse into subdirectories (default: true).")]
        [DefaultValue(true)]
        public bool Recurse { get; init; } = true;

        /// <summary>
        /// Gets or sets whether append is enabled.
        /// </summary>
        [CommandOption("-a|--append")]
        [Description("Append to an existing bundle instead of overwriting it (default: false).")]
        [DefaultValue(false)]
        public bool Append { get; init; } = false;

        /// <summary>Gets whether verified dependencies outside the final package are allowed.</summary>
        [CommandOption("--allow-external")]
        [Description("Allow existing explicit external loose/GAF dependencies; missing and ambiguous references still fail.")]
        public bool AllowExternal { get; init; }

        /// <summary>
        /// Gets or sets the type map path.
        /// </summary>
        [CommandOption("-m|--type-map")]
        [Description("Path to a JSON file that maps asset types to file extensions. " +
                     "Defaults to 'gondwana-asset-types.json' in the current directory or next to the executable.")]
        public string? TypeMapPath { get; init; }

        /// <summary>
        /// Gets or sets the password used to open the assets file.
        /// </summary>
        [CommandOption("-p|--password")]
        [Description("Password to protect the bundle. Required when --encrypt is specified.")]
        public string? Password { get; init; }

        /// <summary>
        /// Gets or sets whether encrypt is enabled.
        /// </summary>
        [CommandOption("-e|--encrypt")]
        [Description("Encrypt the bundle using AES-256. Requires --password <value> to be specified.")]
        [DefaultValue(false)]
        public bool Encrypt { get; init; }
    }

    /// <inheritdoc/>
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(settings.Source);
        var output = Path.GetFullPath(settings.Output);

        if (!Directory.Exists(source))
        {
            AnsiConsole.MarkupLine($"[red]Source directory not found: {Markup.Escape(source)}[/]");
            return 1;
        }

        if (settings.Encrypt && string.IsNullOrEmpty(settings.Password))
        {
            AnsiConsole.MarkupLine("[red]--encrypt requires a password. Use --password <value> to specify one.[/]");
            return 1;
        }

        if (!Enum.TryParse<AssetTypes>(settings.DefaultType, ignoreCase: true, out var defaultType) || !Enum.IsDefined(defaultType))
        {
            AnsiConsole.MarkupLine($"[red]Invalid asset type '{Markup.Escape(settings.DefaultType)}'. Valid types: {string.Join(", ", Enum.GetNames<AssetTypes>())}[/]");
            return 1;
        }

        var typeMap = AssetTypeMap.Load(settings.TypeMapPath, out var mapError);
        if (typeMap is null)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(mapError!)}[/]");
            return 1;
        }

        try
        {
            var plan = SemanticPackPlan.Create(settings, typeMap, defaultType, cancellationToken);
            int packed = plan.Commit();
            AnsiConsole.MarkupLine($"[green]Packed {packed} asset(s) into {Markup.Escape(output)}.[/]");
            return 0;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Packing canceled; the destination was not changed.[/]");
            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Packing failed: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
