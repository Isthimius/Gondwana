using Gondwana.Drawing.Tilesheets.GTS;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gondwana.Cli.Commands.Tilesheets;

internal sealed class TilesheetSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the file.
    /// </summary>
    [CommandArgument(0, "<file.gts>")]
    public string File { get; init; } = "";
}

internal sealed class TilesheetValidateCommand : Command<TilesheetSettings>
{
    /// <inheritdoc/>
    protected override int Execute(CommandContext context, TilesheetSettings settings, CancellationToken cancellationToken)
    {
        var results = TilesheetInspection.AllFromFile(settings.File);
        foreach (var result in results)
            foreach (var error in result.Errors) AnsiConsole.MarkupLine($"[red]Fail:[/] {Markup.Escape(result.Definition?.Name ?? "document")} :: {Markup.Escape(error)}");
        bool valid = results.All(result => result.Errors.Count == 0);
        if (valid) AnsiConsole.WriteLine($"OK: {results.Count} tilesheet definition(s) validated.");
        return valid ? 0 : 1;
    }
}

internal sealed class TilesheetInfoCommand : Command<TilesheetSettings>
{
    /// <inheritdoc/>
    protected override int Execute(CommandContext context, TilesheetSettings settings, CancellationToken cancellationToken)
    {
        var results = TilesheetInspection.AllFromFile(settings.File);
        foreach (var result in results)
        {
            if (result.Definition is { } definition)
            {
                AnsiConsole.WriteLine($"Tilesheet: {definition.Name}");
                AnsiConsole.WriteLine($"Image: {definition.Image?.FilePath ?? definition.Image?.AssetsFilePath} {definition.Image?.AssetEntryName}");
                AnsiConsole.WriteLine($"Dimensions: {result.Width?.ToString() ?? "unknown"} x {result.Height?.ToString() ?? "unknown"}");
                AnsiConsole.WriteLine($"Regions: {definition.Regions.Count}; mask: {definition.Mask is not null}; premultiply alpha: {definition.PremultiplyAlpha}");
                foreach (var region in definition.Regions.Where(r => r is not null))
                {
                    var grid = TilesheetDefinitionValidator.GridSize(region);
                    AnsiConsole.WriteLine($"  {region.Name}: area {region.Area}; tile {region.TileSize}; grid {grid.Columns} x {grid.Rows}; frame metadata {region.Frames.Count}");
                    AnsiConsole.WriteLine($"    collision {region.CollisionType}; insets L/T/R/B {region.CollisionAdjust.Left}/{region.CollisionAdjust.Top}/{region.CollisionAdjust.Right}/{region.CollisionAdjust.Bottom}; overhang {region.Overhang}");
                }
            }
            foreach (var error in result.Errors) AnsiConsole.MarkupLine($"[red]Fail:[/] {Markup.Escape(error)}");
        }
        return results.All(result => result.Errors.Count == 0) ? 0 : 1;
    }
}
