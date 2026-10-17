using System.ComponentModel;
using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Assets;

/// <summary>Exports complete native documents with references rebased to their loose destinations.</summary>
internal sealed class AssetsExtractCommand : Command<AssetsExtractCommand.Settings>
{
    /// <summary>
    /// Defines command-line settings for AssetsExtractCommand.
    /// </summary>
    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the file.
        /// </summary>
        [CommandArgument(0, "<file>")]
        [Description("The asset file to extract.")]
        public string File { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the output.
        /// </summary>
        [CommandArgument(1, "<output>")]
        [Description("The output directory to extract assets into.")]
        public string Output { get; init; } = string.Empty;

        /// <summary>
        /// Gets or sets the type filter.
        /// </summary>
        [CommandOption("-t|--type")]
        [Description("Extract only assets of the specified type (e.g. Image, Audio, Svg).")]
        public string? TypeFilter { get; init; }

        /// <summary>Gets the exact entry name to export, optionally qualified by type.</summary>
        [CommandOption("--entry")]
        [Description("Export one exact entry name. Use --type when the name occurs in multiple asset types.")]
        public string? Entry { get; init; }

        /// <summary>
        /// Gets or sets whether overwrite is enabled.
        /// </summary>
        [CommandOption("--overwrite")]
        [Description("Overwrite existing files.")]
        [DefaultValue(false)]
        public bool Overwrite { get; init; }

        /// <summary>
        /// Gets or sets the password used to open the assets file.
        /// </summary>
        [CommandOption("-p|--password")]
        [Description("Password required to open a password-protected or encrypted bundle.")]
        public string? Password { get; init; }
    }

    /// <inheritdoc/>
    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            AssetTypes? type = null;
            if (settings.TypeFilter is not null)
            {
                if (!Enum.TryParse<AssetTypes>(settings.TypeFilter, true, out var parsed) || !Enum.IsDefined(parsed))
                    throw new ArgumentException("Unknown asset type: " + settings.TypeFilter);
                type = parsed;
            }
            var count = SemanticUnpackPlan.Create(settings.File, settings.Output, settings.Password, type, settings.Entry, settings.Overwrite, cancellationToken).Commit();
            AnsiConsole.WriteLine($"Extracted {count} asset(s) to {Path.GetFullPath(settings.Output)}.");
            return 0;
        }
        catch (Exception ex) { return ProjectCommand.Fail(ex); }
    }
}
