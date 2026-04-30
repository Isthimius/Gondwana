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
        [Description("Extract only assets of the specified type (e.g. Image, Audio).")]
        public string? TypeFilter { get; init; }

        [CommandOption("--overwrite")]
        [Description("Overwrite existing files.")]
        [DefaultValue(false)]
        public bool Overwrite { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var path   = Path.GetFullPath(settings.File);
        var outDir = Path.GetFullPath(settings.Output);

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
            assetFile = AssetsFile.LoadOrCreate(path);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to open asset file: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        Directory.CreateDirectory(outDir);

        int extracted = 0;
        int skipped   = 0;

        using (assetFile)
        {
            var entries = assetFile.GetAllEntries()
                                   .Where(e => typeFilter is null || e.AssetType == typeFilter)
                                   .OrderBy(e => e.AssetType)
                                   .ThenBy(e => e.AssetName)
                                   .ToList();

            foreach (var entry in entries)
            {
                var rawDest = Path.Combine(outDir, entry.AssetName.Replace('/', Path.DirectorySeparatorChar));
                var destPath = Path.GetFullPath(rawDest);

                // Guard against path-traversal: reject entries that escape the output directory.
                var outDirWithSeparator = Path.GetFullPath(outDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                                + Path.DirectorySeparatorChar;
                if (!destPath.StartsWith(outDirWithSeparator, StringComparison.Ordinal))
                {
                    AnsiConsole.MarkupLine($"  [red]Rejected (path traversal): {Markup.Escape(entry.AssetName)}[/]");
                    skipped++;
                    continue;
                }

                if (System.IO.File.Exists(destPath) && !settings.Overwrite)
                {
                    AnsiConsole.MarkupLine($"  [yellow]Skipped (exists): {Markup.Escape(entry.AssetName)}[/]");
                    skipped++;
                    continue;
                }

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                using var stream = assetFile[entry.AssetType, entry.AssetName];
                if (stream is null)
                {
                    AnsiConsole.MarkupLine($"  [yellow]Skipped (unreadable): {Markup.Escape(entry.AssetName)}[/]");
                    skipped++;
                    continue;
                }

                using var destStream = System.IO.File.Create(destPath);
                stream.CopyTo(destStream);
                extracted++;
                AnsiConsole.MarkupLine($"  [dim]{Markup.Escape(entry.AssetName)}[/]");
            }
        }

        AnsiConsole.MarkupLine($"[green]Extracted {extracted} asset(s) to {Markup.Escape(outDir)}.[/]");

        if (skipped > 0)
            AnsiConsole.MarkupLine($"[yellow]{skipped} asset(s) skipped.[/]");

        return 0;
    }
}
