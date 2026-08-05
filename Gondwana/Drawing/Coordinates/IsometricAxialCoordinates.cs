using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Axial isometric projection using a horizontal column axis and a diagonal row axis.
/// Each grid cell renders a diamond inside its W×H footprint. Column anchors advance
/// by (W, 0), while row anchors advance by (W/2, H/2), packing the diamonds edge-to-edge.
/// The pixel anchor is the top vertex of the diamond.
/// </summary>
internal sealed class IsometricAxialCoordinates : ISceneLayerCoordinates
{
    // Precompute half sizes repeatedly used
    private static void WH(SceneLayer m, out int W, out int H, out float halfW, out float halfH)
    {
        W = m.TileWidth;
        H = m.TileHeight;
        halfW = W * 0.5f;
        halfH = H * 0.5f;
    }

    /// <summary>
    /// Converts scene layer grid coordinates to the anchor pixel position in world space.
    /// </summary>
    /// <param name="sceneLayer">The scene layer containing the coordinate system parameters.</param>
    /// <param name="gp">The grid coordinates to convert.</param>
    /// <returns>The anchor pixel position (top vertex of the diamond) in world space.</returns>
    public Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF gp)
    {
        WH(sceneLayer, out int W, out int H, out float halfW, out float halfH);

        int originX = sceneLayer.OriginPx.X;
        int originY = sceneLayer.OriginPx.Y;

        float px = -originX + gp.X * W + gp.Y * halfW;
        float py = -originY + gp.Y * halfH;

        return new Point((int)Math.Floor(px), (int)Math.Floor(py));
    }

    /// <summary>
    /// Converts a pixel position in world space to scene layer grid coordinates.
    /// </summary>
    /// <param name="sceneLayer">The scene layer containing the coordinate system parameters.</param>
    /// <param name="pixelPt">The pixel position in world space to convert.</param>
    /// <returns>The corresponding grid coordinates in the scene layer.</returns>
    public PointF GetSceneLayerCoordinatesAtPixel(SceneLayer sceneLayer, PointF pixelPt)
    {
        WH(sceneLayer, out int W, out int H, out float halfW, out float halfH);

        int originX = sceneLayer.OriginPx.X;
        int originY = sceneLayer.OriginPx.Y;

        float gyF = (pixelPt.Y + originY) / halfH;
        float gxF = (pixelPt.X + originX - gyF * halfW) / W;

        return new PointF(gxF, gyF);
    }

    /// <summary>
    /// Gets all scene layer tiles that intersect with the specified pixel range in world space.
    /// </summary>
    /// <param name="sceneLayer">The scene layer to query.</param>
    /// <param name="worldPixelRange">The rectangular pixel range in world space.</param>
    /// <param name="includeOverhang">Whether to include tile overhang when testing for intersection.</param>
    /// <returns>A list of tiles that intersect with the specified pixel range.</returns>
    public List<SceneLayerTile> GetSceneLayerTilesInPixelRange(SceneLayer sceneLayer, Rectangle worldPixelRange, bool includeOverhang)
    {
        var result = new List<SceneLayerTile>();
        WH(sceneLayer, out int W, out int H, out float halfW, out float halfH);

        // Corner → coarse grid bounds (continuous)
        var ul = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(worldPixelRange.Left, worldPixelRange.Top));
        var ur = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(worldPixelRange.Right, worldPixelRange.Top));
        var ll = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(worldPixelRange.Left, worldPixelRange.Bottom));
        var lr = GetSceneLayerCoordinatesAtPixel(sceneLayer, new PointF(worldPixelRange.Right, worldPixelRange.Bottom));

        int minX = (int)System.Math.Floor(System.Math.Min(System.Math.Min(ul.X, ur.X), System.Math.Min(ll.X, lr.X))) - 2;
        int maxX = (int)System.Math.Ceiling(System.Math.Max(System.Math.Max(ul.X, ur.X), System.Math.Max(ll.X, lr.X))) + 2;
        int minY = (int)System.Math.Floor(System.Math.Min(System.Math.Min(ul.Y, ur.Y), System.Math.Min(ll.Y, lr.Y))) - 2;
        int maxY = (int)System.Math.Ceiling(System.Math.Max(System.Math.Max(ul.Y, ur.Y), System.Math.Max(ll.Y, lr.Y))) + 2;

        // Clamp to layer bounds (no wrapping)
        int cols = sceneLayer.GridColumnCount;
        int rows = sceneLayer.GridRowCount;

        int xStart = System.Math.Max(minX, 0);
        int xEnd = System.Math.Min(maxX, cols - 1);
        int yStart = System.Math.Max(minY, 0);
        int yEnd = System.Math.Min(maxY, rows - 1);
        if (xStart > xEnd || yStart > yEnd) return result;

        for (int y = yStart; y <= yEnd; y++)
        {
            for (int x = xStart; x <= xEnd; x++)
            {
                var gp = sceneLayer[x, y];
                if (gp == null) continue;

                var r = GetPixelRangeForTile(gp, includeOverhang);
                if (r.IntersectsWith(worldPixelRange)) result.Add(gp);
            }
        }
        return result;
    }

    /// <summary>
    /// Gets the pixel bounding rectangle for a tile in world space.
    /// </summary>
    /// <param name="tile">The tile to get the bounds for.</param>
    /// <param name="includeOverhang">Whether to include the tile's overhang pixels in the bounds.</param>
    /// <returns>The rectangular pixel bounds of the tile in world space.</returns>
    public Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang)
    {
        WH(tile.SceneLayer, out int W, out int H, out float halfW, out float halfH);

        // Top vertex of the diamond
        var top = GetAnchorPixelAtSceneLayerCoordinates(tile.SceneLayer, tile.SceneLayerCoordinates);

        // Diamond fits exactly in W×H box whose top-left is (top.X - W/2, top.Y)
        var rect = new Rectangle(top.X - (int)halfW, top.Y, W, H);
        return TileBounds.ApplyOverhang(rect, tile.Overhang, includeOverhang);
    }

    /// <summary>
    /// Gets the combined pixel bounding rectangle for a list of tiles in world space.
    /// </summary>
    /// <param name="tileList">The list of tiles to compute bounds for.</param>
    /// <param name="includeOverhang">Whether to include each tile's overhang pixels in the bounds.</param>
    /// <returns>The union of all tile bounds, or an empty rectangle if the list is empty.</returns>
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
    /// Gets the scene layer tile adjacent to the specified tile in the given cardinal direction.
    /// </summary>
    /// <param name="gp">The reference tile.</param>
    /// <param name="dir">The cardinal direction to look for an adjacent tile.</param>
    /// <returns>The adjacent tile in the specified direction, or null if no tile exists there.</returns>
    public SceneLayerTile GetAdjacentSceneLayerTile(SceneLayerTile gp, CardinalDirections dir)
    {
        var m = gp.SceneLayer; int x = gp.GridCoordinatesAbs.X; int y = gp.GridCoordinatesAbs.Y;
        // Square-like adjacency over the rectangular index space
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
    /// Gets the polygon vertices that define the diamond shape of the tile in world space.
    /// </summary>
    /// <param name="tile">The tile to get polygon points for.</param>
    /// <param name="includeOverhang">Whether to extend the polygon to include overhang pixels.</param>
    /// <returns>An array of points representing the diamond vertices (top, right, bottom, left).</returns>
    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        WH(tile.SceneLayer, out int W, out int H, out _, out _);
        var top = GetAnchorPixelAtSceneLayerCoordinates(tile.SceneLayer, tile.SceneLayerCoordinates);
        var oh = includeOverhang ? tile.Overhang : Spacing.None;

        // Diamond vertices (top, right, bottom, left)
        return new[]
        {
            new Point(top.X,                   top.Y - oh.Top),
            new Point(top.X + W/2 + oh.Right, top.Y + H/2),
            new Point(top.X,                   top.Y + H + oh.Bottom),
            new Point(top.X - W/2 - oh.Left,  top.Y + H/2)
        };
    }

    /// <summary>
    /// Finds the equivalent scene layer coordinates within the bounds of the grid, wrapping coordinates as needed (torus mapping).
    /// </summary>
    /// <param name="valColRow">The input coordinates (may be outside grid bounds).</param>
    /// <param name="xUpperBound">The maximum X grid coordinate (inclusive).</param>
    /// <param name="yUpperBound">The maximum Y grid coordinate (inclusive).</param>
    /// <returns>The wrapped coordinates that fall within [0, xUpperBound] and [0, yUpperBound].</returns>
    public PointF FindEquivalentSceneLayerCoordinates(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        // For now, keep the simple torus mapping; if you prefer no wrap, clamp upstream.
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);
        if (modX < 0) modX += xUpperBound + 1;
        if (modY < 0) modY += yUpperBound + 1;
        return new PointF(modX, modY);
    }
}
