using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Verifies that the fixed grid renderer still visits tiles whose imported
/// overhang reaches into a viewport outside their underlying grid cell.
/// </summary>
[Collection("Global engine state")]
public sealed class RenderTileOverhangCandidateTests
{
    /// <summary>
    /// Exercises both projections at the edges of the conservative TMX import
    /// geometry envelope, where a cell-only viewport check would miss artwork.
    /// </summary>
    [Theory]
    [InlineData(CoordinateSystemTypes.Orthogonal, 32, 24, 32, 48, 0, 24, 0, 0)]
    [InlineData(CoordinateSystemTypes.IsometricRhombic, 32, 24, 40, 40, 0, 16, 8, 0)]
    [InlineData(CoordinateSystemTypes.IsometricRhombic, 64, 32, 64, 64, 0, 16, 0, 16)]
    public void FixedGridCandidateQueryIncludesOverhangIntersectingViewport(
        CoordinateSystemTypes projection, int cellWidth, int cellHeight,
        int imageWidth, int imageHeight,
        int left, int top, int right, int bottom)
    {
        using var bitmap = new SKBitmap(imageWidth, imageHeight);
        bitmap.Erase(SKColors.ForestGreen);
        using var sheet = TilesheetRegistry.Instance.LoadFromBitmap(
            $"overhang-candidate-{Guid.NewGuid():N}", bitmap);
        sheet.DefaultRegion.TileSize = new Size(imageWidth, imageHeight);
        sheet.DefaultRegion.Overhang = new Spacing(left, top, right, bottom);

        using var scene = new Scene();
        var layer = scene.AddLayer(8, 8, cellWidth, cellHeight,
            coordinateSystem: projection);
        var tile = layer[4, 4]!;
        tile.CurrentFrame = sheet.GetFrame(0, 0);

        var cell = layer.CoordinateSystem.GetPixelRangeForTile(tile, false);
        var artwork = tile.DrawLocationWorld;
        Assert.False(cell.Contains(artwork.Location));

        // Query the overhang top edge rather than any of the tile's nominal cell.
        var viewport = new Rectangle(artwork.Left + 1, artwork.Top + 1, 2, 2);
        Assert.True(artwork.IntersectsWith(viewport));
        Assert.False(cell.IntersectsWith(viewport));
        Assert.Contains(tile, RenderTileQuery.GetCandidates(
            layer.CoordinateSystem, layer, viewport, includeOverhang: true).Tiles);
    }
}
