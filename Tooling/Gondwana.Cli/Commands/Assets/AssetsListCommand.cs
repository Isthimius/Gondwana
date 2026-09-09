using System.ComponentModel;
using Gondwana.Assets;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Assets;

internal sealed class AssetsListCommand : Command<AssetsListCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("The asset file to inspect.")]
        public string File { get; init; } = string.Empty;

        [CommandOption("-t|--type")]
        [Description("Filter by asset type (e.g. Image, Audio, Video, Font, Cursor, Svg, Misc).")]
        public string? TypeFilter { get; init; }

        [CommandOption("-p|--password")]
        [Description("Password required to open a password-protected or encrypted bundle.")]
        public string? Password { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken) => Display(settings, summary: false);

    internal static int Display(Settings settings, bool summary)
    {
        try
        {
            AssetTypes? filter = null;
            if (settings.TypeFilter is not null)
            {
                if (!Enum.TryParse<AssetTypes>(settings.TypeFilter, true, out var parsed) || !Enum.IsDefined(parsed))
                    throw new ArgumentException("Unknown asset type: " + settings.TypeFilter);
                filter = parsed;
            }
            using var bundle = BundleHelper.Open(settings.File, settings.Password);
            var entries = bundle.GetAllEntries().OrderBy(e => e.AssetType).ThenBy(e => e.AssetName, StringComparer.Ordinal).ToArray();
            if (summary)
            {
                AnsiConsole.WriteLine($"Bundle: {Path.GetFullPath(settings.File)}");
                AnsiConsole.WriteLine($"Entries: {entries.Length}");
                AnsiConsole.WriteLine($"Bundle size: {new FileInfo(settings.File).Length} bytes");
            }
            var table = new Table().AddColumn("Type").AddColumn("Name").AddColumn("Bytes");
            foreach (var entry in entries.Where(e => filter is null || e.AssetType == filter))
            {
                using var stream = bundle[entry.AssetType, entry.AssetName]!;
                table.AddRow(entry.AssetType.ToString(), Markup.Escape(entry.AssetName), stream.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            AnsiConsole.Write(table);
            return 0;
        }
        catch (Exception ex) { return ProjectCommand.Fail(ex); }
    }
}