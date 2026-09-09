using System.ComponentModel;
using Gondwana.Assets;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Assets;

internal sealed class AssetsExtractCommand : Command<AssetsExtractCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("The asset file to extract.")]
        public string File { get; init; } = string.Empty;

        [CommandArgument(1, "<output>")]
        [Description("The output directory to extract assets into.")]
        public string Output { get; init; } = string.Empty;

        [CommandOption("-t|--type")]
        [Description("Extract only assets of the specified type (e.g. Image, Audio, Svg).")]
        public string? TypeFilter { get; init; }

        [CommandOption("--overwrite")]
        [Description("Overwrite existing files.")]
        [DefaultValue(false)]
        public bool Overwrite { get; init; }

        [CommandOption("-p|--password")]
        [Description("Password required to open a password-protected or encrypted bundle.")]
        public string? Password { get; init; }
    }

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
            var count = BundleHelper.Extract(settings.File, settings.Output, settings.Password, type, settings.Overwrite);
            AnsiConsole.WriteLine($"Extracted {count} asset(s) to {Path.GetFullPath(settings.Output)}.");
            return 0;
        }
        catch (Exception ex) { return ProjectCommand.Fail(ex); }
    }
}