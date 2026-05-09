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

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(settings.File);

        if (!System.IO.File.Exists(path))
        {
            AnsiConsole.MarkupLine($"[red]File not found: {Markup.Escape(path)}[/]");
            return 1;
        }

        AssetTypes? typeFilter = null;
        if (settings.TypeFilter is not null)
        {
            if (!Enum.TryParse<AssetTypes>(settings.TypeFilter, ignoreCase: true, out var parsedType))
            {
                AnsiConsole.MarkupLine($"[red]Invalid type '{Markup.Escape(settings.TypeFilter)}'. Valid types: {string.Join(", ", Enum.GetNames<AssetTypes>())}[/]");
                return 1;
            }

            typeFilter = parsedType;
        }

        AssetsFile assetFile;
        try
        {
            assetFile = AssetsFile.LoadOrCreate(path, settings.Password);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to open asset file: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        using (assetFile)
        {
            var entries = assetFile.GetAllEntries()
                                   .OrderBy(e => e.AssetType)
                                   .ThenBy(e => e.AssetName)
                                   .Where(e => typeFilter is null || e.AssetType == typeFilter)
                                   .ToList();

            if (entries.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No assets found.[/]");
                return 0;
            }

            var table = new Table();
            table.AddColumn("Type");
            table.AddColumn("Name");
            table.AddColumn(new TableColumn("Size").RightAligned());

            foreach (var entry in entries)
            {
                using var stream = assetFile[entry.AssetType, entry.AssetName];
                long sizeBytes = 0;

                if (stream is not null)
                {
                    if (stream.CanSeek)
                    {
                        sizeBytes = stream.Length;
                    }
                    else
                    {
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        sizeBytes = ms.Length;
                    }
                }

                table.AddRow(
                    entry.AssetType.ToString(),
                    Markup.Escape(entry.AssetName),
                    FormatSize(sizeBytes));
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"[dim]{entries.Count} asset(s) in {Markup.Escape(path)}[/]");
        }

        return 0;
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int index = 0;

        while (value >= 1024 && index < suffixes.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return $"{value:0.##} {suffixes[index]}";
    }
}
