using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Diagonal-Isometric (Diagonal Matrix) – same diamond projection
/// but kept separate for alternative adjacency/rounding if needed.
/// Uses identical math for now (clean, predictable behavior).
/// </summary>
internal sealed class IsometricRhombicCoordinates : ISceneLayerCoordinates
{
    /// <summary>
    /// Gets the anchor pixel position for a given scene layer coordinate.
    /// </summary>
    /// <param name="sceneLayer">The scene layer containing tile dimensions and origin.</param>
    /// <param name="gp">The grid-space coordinates (x,y).</param>
    /// <returns>The pixel position as a <see cref="Point"/>.</returns>
    public Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF gp)
    {
        int W = sceneLayer.TileWidth;
        int H = sceneLayer.TileHeight;

        int originX = sceneLayer.OriginPx.X;
        int originY = sceneLayer.OriginPx.Y;

        // gp is in grid-space (x,y) already.
        float dx = gp.X;
        float dy = gp.Y;

        float px = (dx - dy) * (W / 2f) - originX;
        float py = (dx + dy) * (H / 2f) - originY;

        return new Point((int)Math.Floor(px), (int)Math.Floor(py));
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

        float a = (pixelPt.X + originX) / (W / 2f);
        float b = (pixelPt.Y + originY) / (H / 2f);

        float dx = (a + b) / 2f;
        float dy = (b - a) / 2f;

        return new PointF(dx, dy);
    }

    /// <summary>
    /// Gets all scene layer tiles that intersect with the specified pixel range.
    /// </summary>
    /// <param name="sceneLayer">The scene layer to query tiles from.</param>
    /// <param name="worldPixelRange">The rectangular pixel range to check for intersections.</param>
    /// <param name="includeOverhang">Whether to include tile overhang in intersection calculations.</param>
    /// <returns>A list of <see cref="SceneLayerTile"/> objects that intersect with the pixel range.</returns>
    public List<SceneLayerTile> GetSceneLayerTilesInPixelRange(SceneLayer  sceneLayer, Rectangle worldPixelRange, bool includeOverhang)
    {
        var result = new List<SceneLayerTile>();
        var ul = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(worldPixelRange.Left, worldPixelRange.Top));
        var ur = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(worldPixelRange.Right, worldPixelRange.Top));
        var ll = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(worldPixelRange.Left, worldPixelRange.Bottom));
        var lr = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(worldPixelRange.Right, worldPixelRange.Bottom));

        int minX = (int)Math.Floor(new[] { ul.X, ur.X, ll.X, lr.X }.Min()) - 1;
        int maxX = (int)Math.Ceiling(new[] { ul.X, ur.X, ll.X, lr.X }.Max()) + 1;
        int minY = (int)Math.Floor(new[] { ul.Y, ur.Y, ll.Y, lr.Y }.Min()) - 1;
        int maxY = (int)Math.Ceiling(new[] { ul.Y, ur.Y, ll.Y, lr.Y }.Max()) + 1;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var gp =  sceneLayer[x, y];
                if (gp == null) continue;
                var r = GetPixelRangeForTile(gp, includeOverhang);
                if (r.IntersectsWith(worldPixelRange)) result.Add(gp);
            }
        }
        return result;
    }

    /// <summary>
    /// Gets the pixel range (bounding rectangle) for a tile.
    /// </summary>
    /// <param name="tile">The tile to get the pixel range for.</param>
    /// <param name="includeOverhang">Whether to include the tile's overhang pixels in the range.</param>
    /// <returns>A <see cref="Rectangle"/> representing the pixel range of the tile.</returns>
    public Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang)
    {
        var top = GetAnchorPixelAtSceneLayerCoordinates(tile.SceneLayer, tile.SceneLayerCoordinates);
        int W = tile.SceneLayer.TileWidth; int H = tile.SceneLayer.TileHeight;
        var rect = new Rectangle(top.X - W / 2, top.Y, W, H);
        return TileBounds.ApplyOverhang(rect, tile.OverhangPixels, includeOverhang);
    }

    /// <summary>
    /// Gets the combined pixel range (bounding rectangle) for a list of tiles.
    /// </summary>
    /// <param name="tileList">The list of tiles to get the combined pixel range for.</param>
    /// <param name="includeOverhang">Whether to include overhang pixels in the range.</param>
    /// <returns>A <see cref="Rectangle"/> representing the union of all tile pixel ranges.</returns>
    public Rectangle GetPixelRangeForTileList(List<Tile> tileList, bool includeOverhang)
    {
        Rectangle ret = Rectangle.Empty;
        foreach (var t in tileList)
        {
            var r = GetPixelRangeForTile(t, includeOverhang);
            ret = ret.IsEmpty ? r : Rectangle.Union(ret, r);
        }
        return ret;
    }

    /// <summary>
    /// Gets the adjacent scene layer tile in the specified cardinal direction.
    /// </summary>
    /// <param name="gp">The source scene layer tile.</param>
    /// <param name="dir">The cardinal direction to get the adjacent tile.</param>
    /// <returns>The adjacent <see cref="SceneLayerTile"/>, or null if no tile exists in that direction.</returns>
    public SceneLayerTile GetAdjacentSceneLayerTile(SceneLayerTile gp, CardinalDirections dir)
    {
        var m = gp.SceneLayer; int x = gp.GridCoordinatesAbs.X; int y = gp.GridCoordinatesAbs.Y;
        return dir switch
        {
            CardinalDirections.N => m[x, y - 1],
            CardinalDirections.S => m[x, y + 1],
            CardinalDirections.E => m[x + 1, y],
            CardinalDirections.W => m[x - 1, y],
            CardinalDirections.NE => m[x + 1, y - 1],
            CardinalDirections.NW => m[x - 1, y - 1],
            CardinalDirections.SE => m[x + 1, y + 1],
            CardinalDirections.SW => m[x - 1, y + 1],
            _ => null
        };
    }

    /// <summary>
    /// Gets the polygon points that define the diamond shape of a tile.
    /// </summary>
    /// <param name="tile">The tile to get polygon points for.</param>
    /// <param name="includeOverhang">Whether to include overhang pixels in the polygon.</param>
    /// <returns>An array of <see cref="Point"/> objects defining the tile's diamond shape.</returns>
    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        var top = GetAnchorPixelAtSceneLayerCoordinates(tile.SceneLayer, tile.SceneLayerCoordinates);
        int W = tile.SceneLayer.TileWidth; int H = tile.SceneLayer.TileHeight;
        var oh = includeOverhang ? tile.OverhangPixels : Overhang.None;

        return new[]
        {
                new Point(top.X, top.Y - oh.Top),
                new Point(top.X + W/2 + oh.Right, top.Y + H/2),
                new Point(top.X, top.Y + H + oh.Bottom),
                new Point(top.X - W/2 - oh.Left, top.Y + H/2)
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
        if (modX < 0) modX += xUpperBound + 1;
        if (modY < 0) modY += yUpperBound + 1;
        return new PointF(modX, modY);
    }
}
