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

        [CommandOption("-a|--append")]
        [Description("Append to an existing bundle instead of overwriting it (default: false).")]
        [DefaultValue(false)]
        public bool Append { get; init; } = false;

        [CommandOption("-m|--type-map")]
        [Description("Path to a JSON file that maps asset types to file extensions. " +
                     "Defaults to 'gondwana-asset-types.json' in the current directory or next to the executable.")]
        public string? TypeMapPath { get; init; }

        [CommandOption("-p|--password")]
        [Description("Password to protect the bundle. Required when --encrypt is specified.")]
        public string? Password { get; init; }

        [CommandOption("-e|--encrypt")]
        [Description("Encrypt the bundle using AES-256. Requires --password <value> to be specified.")]
        [DefaultValue(false)]
        public bool Encrypt { get; init; }
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

        if (settings.Encrypt && string.IsNullOrEmpty(settings.Password))
        {
            AnsiConsole.MarkupLine("[red]--encrypt requires a password. Use --password <value> to specify one.[/]");
            return 1;
        }

        if (!Enum.TryParse<AssetTypes>(settings.DefaultType, ignoreCase: true, out var defaultType))
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

        var searchOption = settings.Recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(source, "*", searchOption);

        if (files.Length == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No files found in {Markup.Escape(source)}.[/]");
            return 0;
        }

        // Default: overwrite (delete any existing bundle so stale entries are not retained).
        // With --append: load the existing bundle and merge new files into it.
        if (!settings.Append && File.Exists(output))
            File.Delete(output);

        var assetFile = AssetsFile.LoadOrCreate(output, settings.Password, settings.Encrypt);
        int packed = 0;
        int skipped = 0;

        AnsiConsole.Status().Start("Packing assets...", ctx =>
        {
            ctx.Spinner(Spinner.Known.Dots);

            foreach (var file in files)
            {
                ctx.Status($"Packing {Markup.Escape(Path.GetFileName(file))}...");

                var assetType = typeMap.Infer(file, defaultType);
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

        try
        {
            var outputDirectory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            assetFile.Save();
        }
        catch (IOException ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to save assets to {Markup.Escape(output)}: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            AnsiConsole.MarkupLine($"[red]Failed to save assets to {Markup.Escape(output)}: {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
        finally
        {
            assetFile.Dispose();
        }
        AnsiConsole.MarkupLine($"[green]Packed {packed} asset(s) into {Markup.Escape(output)}.[/]");

        if (!string.IsNullOrEmpty(settings.Password))
        {
            var protection = settings.Encrypt ? "AES-256 encrypted" : "password-protected";
            AnsiConsole.MarkupLine($"[dim]Bundle is {protection}.[/]");
        }

        if (skipped > 0)
            AnsiConsole.MarkupLine($"[yellow]{skipped} file(s) skipped.[/]");

        return 0;
    }
}

