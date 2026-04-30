using System.ComponentModel;
using Gondwana.Assets;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Assets;

internal sealed class AssetsPackCommand : Command<AssetsPackCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<source>")]
        [Description("The source directory containing files to pack.")]
        public string Source { get; init; } = string.Empty;

        [CommandArgument(1, "<output>")]
        [Description("The output asset file path (e.g. game.assets or game.gaf).")]
        public string Output { get; init; } = string.Empty;

        [CommandOption("-t|--type")]
        [Description("Default asset type for files whose type cannot be inferred (default: Misc).")]
        [DefaultValue("Misc")]
        public string DefaultType { get; init; } = "Misc";

        [CommandOption("-r|--recurse")]
        [Description("Recurse into subdirectories (default: true).")]
        [DefaultValue(true)]
        public bool Recurse { get; init; } = true;
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var source = Path.GetFullPath(settings.Source);
        var output = Path.GetFullPath(settings.Output);

        if (!Directory.Exists(source))
        {
            AnsiConsole.MarkupLine($"[red]Source directory not found: {Markup.Escape(source)}[/]");
            return 1;
        }

        if (!Enum.TryParse<AssetTypes>(settings.DefaultType, ignoreCase: true, out var defaultType))
        {
            AnsiConsole.MarkupLine($"[red]Invalid asset type '{Markup.Escape(settings.DefaultType)}'. Valid types: {string.Join(", ", Enum.GetNames<AssetTypes>())}[/]");
            return 1;
        }

        var searchOption = settings.Recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(source, "*.*", searchOption);

        if (files.Length == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No files found in {Markup.Escape(source)}.[/]");
            return 0;
        }

        var assetFile = AssetsFile.LoadOrCreate(output);
        int packed = 0;
        int skipped = 0;

        AnsiConsole.Status().Start("Packing assets...", ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);

            foreach (var file in files)
            {
                ctx.Status($"Packing {Markup.Escape(Path.GetFileName(file))}...");

                var assetType = InferAssetType(file, defaultType);
                var assetName = Path.GetRelativePath(source, file).Replace('\\', '/');

                try
                {
                    assetFile.Add(assetType, file, assetName);
                    packed++;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"  [yellow]Skipped {Markup.Escape(Path.GetFileName(file))}: {Markup.Escape(ex.Message)}[/]");
                    skipped++;
                }
            }
        });

        assetFile.Save();
        assetFile.Dispose();

        AnsiConsole.MarkupLine($"[green]Packed {packed} asset(s) into {Markup.Escape(output)}.[/]");

        if (skipped > 0)
            AnsiConsole.MarkupLine($"[yellow]{skipped} file(s) skipped.[/]");

        return 0;
    }

    private static AssetTypes InferAssetType(string filePath, AssetTypes fallback)
    {
        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();

        return ext switch
        {
            "png" or "jpg" or "jpeg" or "bmp" or "gif" or "webp" or "tiff" or "ico" => AssetTypes.Image,
            "wav" or "mp3" or "ogg" or "flac" or "aac" or "wma" or "mid" or "midi" => AssetTypes.Audio,
            "mp4" or "avi" or "mkv" or "mov" or "wmv" or "webm" or "m4v"           => AssetTypes.Video,
            "cur" or "ani"                                                           => AssetTypes.Cursor,
            "ttf" or "otf" or "woff" or "woff2"                                     => AssetTypes.Font,
            _ => fallback,
        };
    }
}
