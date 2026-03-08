using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Provides orthogonal (rectangular grid) coordinate system implementation for scene layers.
/// Tiles are arranged in a standard rectangular grid with no rotation or skewing.
/// </summary>
internal sealed class OrthogonalCoordinates : ISceneLayerCoordinates
{
    /// <summary>
    /// Gets the anchor pixel position for a given scene layer coordinate.
    /// </summary>
    /// <param name="sceneLayer">The scene layer containing tile dimensions and origin.</param>
    /// <param name="layerPoint">The layer coordinates to convert.</param>
    /// <returns>The pixel position as a <see cref="Point"/>.</returns>
    public Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF layerPoint)
    {
        int W = sceneLayer.TileWidth;
        int H = sceneLayer.TileHeight;

        var origin = sceneLayer.OriginPx;

        int x = (int)(W * layerPoint.X) - origin.X;
        int y = (int)(H * layerPoint.Y) - origin.Y;

        return new Point(x, y);
    }

    /// <summary>
    /// Converts a pixel position to scene layer coordinates.
    /// </summary>
    /// <param name="sceneLayer">The scene layer containing tile dimensions and origin.</param>
    /// <param name="pixelPt">The pixel position to convert.</param>
    /// <returns>The scene layer coordinates as a <see cref="PointF"/>.</returns>
    public PointF GetSceneLayerCoordinatesAtPixel(SceneLayer sceneLayer, PointF pixelPt)
    {
        int W = sceneLayer.TileWidth;
        int H = sceneLayer.TileHeight;

        int originX = sceneLayer.OriginPx.X;
        int originY = sceneLayer.OriginPx.Y;

        float gx = (pixelPt.X + originX) / W;
        float gy = (pixelPt.Y + originY) / H;

        return new PointF(gx, gy);
    }

    /// <summary>
    /// Gets all scene layer tiles that intersect with the specified pixel range.
    /// Updated to properly consider overhang in all directions.
    /// </summary>
    /// <param name="sceneLayer">The scene layer to query tiles from.</param>
    /// <param name="worldPixelRange">The rectangular pixel range to check for intersections.</param>
    /// <param name="includeOverhang">Whether to include tile overhang in intersection calculations.</param>
    /// <returns>A list of <see cref="SceneLayerTile"/> objects that intersect with the pixel range.</returns>
    public List<SceneLayerTile> GetSceneLayerTilesInPixelRange(SceneLayer sceneLayer, Rectangle worldPixelRange, bool includeOverhang)
    {
        var retVal = new List<SceneLayerTile>();

        // 1) Find coarse grid bounds via inverse transform (unchanged)
        PointF ptUL = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(worldPixelRange.Left, worldPixelRange.Top));
        PointF ptBR = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(worldPixelRange.Right - 1, worldPixelRange.Bottom - 1));

        int minY = (int)Math.Floor(ptUL.Y) - 1;
        int maxY = (int)Math.Ceiling(ptBR.Y) + 1;
        int minX = (int)Math.Floor(ptUL.X) - 1;
        int maxX = (int)Math.Ceiling(ptBR.X) + 1;

        // 2) Scan candidate grid cells and include if their overhang-aware rect intersects
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var gPt = sceneLayer[x, y];
                if (gPt == null) continue;

                // Overhang-aware pixel rect
                var rect = GetPixelRangeForTile(gPt, includeOverhang);
                if (rect.IntersectsWith(worldPixelRange))
                    retVal.Add(gPt);
            }
        }

        return retVal;
    }

    /// <summary>
    /// Gets the pixel range (bounding rectangle) for a tile.
    /// </summary>
    /// <param name="tile">The tile to get the pixel range for.</param>
    /// <param name="includeOverhang">Whether to include the tile's overhang pixels in the range.</param>
    /// <returns>A <see cref="Rectangle"/> representing the pixel range of the tile.</returns>
    public Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang)
    {
        var layer = tile.SceneLayer;
        int W = layer.TileWidth;
        int H = layer.TileHeight;

        int originX = layer.OriginPx.X;
        int originY = layer.OriginPx.Y;

        var baseRect = new Rectangle
        {
            X = (int)(W * tile.SceneLayerCoordinates.X) - originX,
            Y = (int)(H * tile.SceneLayerCoordinates.Y) - originY,
            Width = W,
            Height = H
        };

        // Apply full overhang (Left/Top/Right/Bottom)
        return TileBounds.ApplyOverhang(baseRect, tile.OverhangPixels, includeOverhang);
    }

    /// <summary>
    /// Gets the combined pixel range (bounding rectangle) for a list of tiles.
    /// </summary>
    /// <param name="tileList">The list of tiles to get the combined pixel range for.</param>
    /// <param name="includeOverhang">Whether to include overhang pixels in the range.</param>
    /// <returns>A <see cref="Rectangle"/> representing the union of all tile pixel ranges.</returns>
    public Rectangle GetPixelRangeForTileList(List<Tile> tileList, bool includeOverhang)
    {
        Rectangle retVal = Rectangle.Empty;

        foreach (Tile tile in tileList)
        {
            var rect = GetPixelRangeForTile(tile, includeOverhang);
            retVal = retVal.IsEmpty ? rect : Rectangle.Union(retVal, rect);
        }

        return retVal;
    }

    /// <summary>
    /// Gets the adjacent scene layer tile in the specified cardinal direction.
    /// </summary>
    /// <param name="layerPoint">The source scene layer tile.</param>
    /// <param name="direction">The cardinal direction to get the adjacent tile.</param>
    /// <returns>The adjacent <see cref="SceneLayerTile"/>, or null if no tile exists in that direction.</returns>
    public SceneLayerTile GetAdjacentSceneLayerTile(SceneLayerTile layerPoint, CardinalDirections direction)
    {
        SceneLayer sceneLayer = layerPoint.SceneLayer;

        switch (direction)
        {
            case CardinalDirections.N:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X, layerPoint.GridCoordinatesAbs.Y - 1];

            case CardinalDirections.NE:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X + 1, layerPoint.GridCoordinatesAbs.Y - 1];

            case CardinalDirections.E:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X + 1, layerPoint.GridCoordinatesAbs.Y];

            case CardinalDirections.SE:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X + 1, layerPoint.GridCoordinatesAbs.Y + 1];

            case CardinalDirections.S:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X, layerPoint.GridCoordinatesAbs.Y + 1];

            case CardinalDirections.SW:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X - 1, layerPoint.GridCoordinatesAbs.Y + 1];

            case CardinalDirections.W:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X - 1, layerPoint.GridCoordinatesAbs.Y];

            case CardinalDirections.NW:
                return sceneLayer[layerPoint.GridCoordinatesAbs.X - 1, layerPoint.GridCoordinatesAbs.Y - 1];

            default:
                return null;
        }
    }

    /// <summary>
    /// Gets the polygon points that define the rectangular shape of a tile.
    /// </summary>
    /// <param name="tile">The tile to get polygon points for.</param>
    /// <param name="includeOverhang">Whether to include overhang pixels in the polygon.</param>
    /// <returns>An array of <see cref="Point"/> objects defining the tile's rectangular corners.</returns>
    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        // Square polygon using the overhang-aware rect
        var r = GetPixelRangeForTile(tile, includeOverhang);
        return new[]
        {
                new Point(r.Left,  r.Top),
                new Point(r.Right, r.Top),
                new Point(r.Right, r.Bottom),
                new Point(r.Left,  r.Bottom)
            };
    }

    /// <summary>
    /// Finds the equivalent scene layer coordinates within the specified bounds using modulo wrapping.
    /// </summary>
    /// <param name="valColRow">The coordinates to wrap.</param>
    /// <param name="xUpperBound">The upper bound for the x-coordinate (inclusive).</param>
    /// <param name="yUpperBound">The upper bound for the y-coordinate (inclusive).</param>
    /// <returns>The wrapped coordinates as a <see cref="PointF"/>.</returns>
    public PointF FindEquivalentSceneLayerCoordinates(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);

        if (modX < 0)
            modX += xUpperBound + 1;

        if (modY < 0)
            modY += yUpperBound + 1;

        return new PointF(modX, modY);
    }
}