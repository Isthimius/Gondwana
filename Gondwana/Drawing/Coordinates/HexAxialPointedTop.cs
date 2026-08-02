using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Hexagonal – Pointed-Top layout (even-r horizontal layout)
/// Bounding rectangle is (W x H). Centers advance by (W/2 per row parity, 0.75*H vertically)
/// </summary>
internal sealed class HexAxialPointedTop : ISceneLayerCoordinates
{
    /// <summary>
    /// Gets the anchor pixel position for a tile at the specified scene layer coordinates.
    /// </summary>
    /// <param name="sceneLayer">The scene layer containing the tile.</param>
    /// <param name="gp">The scene layer coordinates (column, row).</param>
    /// <returns>The pixel position of the tile's anchor point.</returns>
    public Point GetAnchorPixelAtSceneLayerCoordinates(SceneLayer sceneLayer, PointF gp)
    {
        int width = sceneLayer.TileWidth;
        int height = sceneLayer.TileHeight;

        // The layout uses even-r offset coordinates. Interpolate the
        // staggered X offset between the surrounding integer rows.
        int baseRow = (int)MathF.Floor(gp.Y);
        float rowProgress = gp.Y - baseRow;

        // Preserve the existing integer-division behavior for odd tile widths.
        float halfWidth = width / 2;
        float currentRowOffsetX = (baseRow & 1) == 0 ? 0f : halfWidth;
        float nextRowOffsetX = ((baseRow + 1) & 1) == 0 ? 0f : halfWidth;
        float offsetX = currentRowOffsetX + ((nextRowOffsetX - currentRowOffsetX) * rowProgress);
        var origin = sceneLayer.OriginPx;

        float x = -origin.X + (gp.X * width) + offsetX;
        float y = -origin.Y + (gp.Y * height * 0.75f);

        return new Point(
            (int)MathF.Floor(x),
            (int)MathF.Floor(y));
    }

    /// <summary>
    /// Gets the scene layer coordinates (column, row) at the specified pixel position.
    /// Uses polygon hit-testing and nearest center calculation to determine the hexagonal tile.
    /// </summary>
    /// <param name="sceneLayer">The scene layer to query.</param>
    /// <param name="pixelPt">The pixel position to convert.</param>
    /// <returns>The scene layer coordinates as a PointF (column, row).</returns>
    public PointF GetSceneLayerCoordinatesAtPixel(SceneLayer sceneLayer, PointF pixelPt)
    {
        int W = sceneLayer.TileWidth;
        int H = sceneLayer.TileHeight;

        int originX = sceneLayer.OriginPx.X;
        int originY = sceneLayer.OriginPx.Y;

        float fy = (pixelPt.Y + originY) / (H * 0.75f);
        int approxRow = (int)Math.Round(fy);

        int baseX = -originX + ((approxRow & 1) == 0 ? 0 : W / 2);
        float fx = (pixelPt.X - baseX) / (float)W;
        int approxCol = (int)Math.Round(fx);

        var best = new Point(approxCol, approxRow);
        float bestDist = float.MaxValue;

        foreach (var cand in NeighborsPointedTop(approxCol, approxRow, includeSelf: true))
        {
            var poly = HexPolygonPointedTop(sceneLayer, cand.X, cand.Y, includeOverhang: false);

            // Only here: round once for PIP test
            var pInt = new Point((int)Math.Round(pixelPt.X), (int)Math.Round(pixelPt.Y));
            if (PointInPolygon(poly, pInt)) return new PointF(cand.X, cand.Y);

            float cx = -sceneLayer.OriginPx.X + cand.X * (float)W + ((cand.Y & 1) == 0 ? 0 : W / 2f) + W / 2f;
            float cy = -sceneLayer.OriginPx.Y + cand.Y * (H * 0.75f) + H / 2f;
            float d = (cx - pixelPt.X) * (cx - pixelPt.X) + (cy - pixelPt.Y) * (cy - pixelPt.Y);
            if (d < bestDist) { bestDist = d; best = cand; }
        }
        return new PointF(best.X, best.Y);
    }

    /// <summary>
    /// Gets all scene layer tiles that intersect with the specified pixel range.
    /// </summary>
    /// <param name="sceneLayer">The scene layer to query.</param>
    /// <param name="worldPixelRange">The pixel range to search within.</param>
    /// <param name="includeOverhang">Whether to include tile overhang pixels in the intersection test.</param>
    /// <returns>A list of tiles that intersect with the specified pixel range.</returns>
    public List<SceneLayerTile> GetSceneLayerTilesInPixelRange(SceneLayer sceneLayer, Rectangle worldPixelRange, bool includeOverhang)
    {
        var result = new List<SceneLayerTile>();
        int W = sceneLayer.TileWidth; int H = sceneLayer.TileHeight;

        int minRow = (int)Math.Floor((worldPixelRange.Top + sceneLayer.OriginPx.Y) / (H * 0.75f)) - 2;
        int maxRow = (int)Math.Ceiling((worldPixelRange.Bottom + sceneLayer.OriginPx.Y) / (H * 0.75f)) + 2;

        for (int row = minRow; row <= maxRow; row++)
        {
            int xOffset = ((row & 1) == 0 ? 0 : W / 2);
            int minCol = (int)Math.Floor((worldPixelRange.Left + sceneLayer.OriginPx.X - xOffset) / (float)W) - 2;
            int maxCol = (int)Math.Ceiling((worldPixelRange.Right + sceneLayer.OriginPx.X - xOffset) / (float)W) + 2;

            for (int col = minCol; col <= maxCol; col++)
            {
                var gp = sceneLayer[col, row]; if (gp == null) continue;
                var r = GetPixelRangeForTile(gp, includeOverhang);
                if (r.IntersectsWith(worldPixelRange)) result.Add(gp);
            }
        }
        return result;
    }

    /// <summary>
    /// Gets the pixel bounding rectangle for the specified tile.
    /// </summary>
    /// <param name="tile">The tile to get the pixel range for.</param>
    /// <param name="includeOverhang">Whether to include overhang pixels in the bounds.</param>
    /// <returns>A rectangle representing the tile's pixel bounds.</returns>
    public Rectangle GetPixelRangeForTile(Tile tile, bool includeOverhang)
    {
        var p = GetAnchorPixelAtSceneLayerCoordinates(tile.SceneLayer, tile.SceneLayerCoordinates);
        int W = tile.SceneLayer.TileWidth; int H = tile.SceneLayer.TileHeight;
        var rect = new Rectangle(p.X, p.Y, W, H);
        return TileBounds.ApplyOverhang(rect, tile.Overhang, includeOverhang);
    }

    /// <summary>
    /// Gets the combined pixel bounding rectangle for a list of tiles.
    /// </summary>
    /// <param name="tileList">The list of tiles to get the combined pixel range for.</param>
    /// <param name="includeOverhang">Whether to include overhang pixels in the bounds.</param>
    /// <returns>A rectangle representing the union of all tile bounds, or an empty rectangle if the list is empty.</returns>
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
    /// For pointed-top hexagons, valid directions are N, S, NE, NW, SE, SW. E and W return null.
    /// </summary>
    /// <param name="gp">The source tile.</param>
    /// <param name="dir">The cardinal direction to search.</param>
    /// <returns>The adjacent tile in the specified direction, or null if none exists or direction is invalid.</returns>
    public SceneLayerTile GetAdjacentSceneLayerTile(SceneLayerTile gp, CardinalDirections dir)
    {
        int x = gp.GridCoordinatesAbs.X; int y = gp.GridCoordinatesAbs.Y;
        bool even = (y & 1) == 0; var m = gp.SceneLayer;
        return dir switch
        {
            CardinalDirections.N => m[x, y - 1],
            CardinalDirections.S => m[x, y + 1],
            CardinalDirections.NE => m[x + (even ? 0 : 1), y - 1],
            CardinalDirections.NW => m[x - (even ? 1 : 0), y - 1],
            CardinalDirections.SE => m[x + (even ? 0 : 1), y + 1],
            CardinalDirections.SW => m[x - (even ? 1 : 0), y + 1],
            CardinalDirections.E => null,
            CardinalDirections.W => null,
            _ => null
        };
    }

    /// <summary>
    /// Gets the polygon vertices for the specified tile as an array of points.
    /// </summary>
    /// <param name="tile">The tile to get the polygon for.</param>
    /// <param name="includeOverhang">Whether to include overhang in the polygon calculation.</param>
    /// <returns>An array of six points representing the hexagonal tile's vertices.</returns>
    public Point[] GetPolygonPts(Tile tile, bool includeOverhang)
    {
        return HexPolygonPointedTop(tile.SceneLayer, (int)tile.SceneLayerCoordinates.X, (int)tile.SceneLayerCoordinates.Y, includeOverhang);
    }

    /// <summary>
    /// Finds equivalent scene layer coordinates within the specified bounds by wrapping values.
    /// </summary>
    /// <param name="valColRow">The input coordinates (column, row).</param>
    /// <param name="xUpperBound">The upper bound for the X coordinate (column).</param>
    /// <param name="yUpperBound">The upper bound for the Y coordinate (row).</param>
    /// <returns>The wrapped coordinates within bounds.</returns>
    public PointF FindEquivalentSceneLayerCoordinates(PointF valColRow, int xUpperBound, int yUpperBound)
    {
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);
        if (modX < 0) modX += xUpperBound + 1;
        if (modY < 0) modY += yUpperBound + 1;
        return new PointF(modX, modY);
    }

    // Helpers ---------------------------------------------------
    private static IEnumerable<Point> NeighborsPointedTop(int col, int row, bool includeSelf)
    {
        if (includeSelf) yield return new Point(col, row);
        bool even = (row & 1) == 0;
        yield return new Point(col, row - 1); // N
        yield return new Point(col, row + 1); // S
        yield return new Point(col + (even ? 0 : 1), row - 1); // NE
        yield return new Point(col - (even ? 1 : 0), row - 1); // NW
        yield return new Point(col + (even ? 0 : 1), row + 1); // SE
        yield return new Point(col - (even ? 1 : 0), row + 1); // SW
    }

    private static Point[] HexPolygonPointedTop(SceneLayer sceneLayer, int col, int row, bool includeOverhang)
    {
        int W = sceneLayer.TileWidth;
        int H = sceneLayer.TileHeight;

        int originX = sceneLayer.OriginPx.X;
        int originY = sceneLayer.OriginPx.Y;

        var p = new Point(
            -originX + col * W + ((row & 1) == 0 ? 0 : W / 2),
            -originY + (int)Math.Floor(row * (H * 0.75f)));

        var rect = new Rectangle(p.X, p.Y, W, H);
        var ohRect = TileBounds.ApplyOverhang(rect, includeOverhang ? new Spacing(0, 0, 0, 0) : Spacing.None, includeOverhang);
        int x = ohRect.X; int y = ohRect.Y;

        // Pointed-top vertices from bounding box
        return new[]
        {
                new Point(x + W/2, y),
                new Point(x + W, y + H/4),
                new Point(x + W, y + 3*H/4),
                new Point(x + W/2, y + H),
                new Point(x, y + 3*H/4),
                new Point(x, y + H/4)
            };
    }

    private static bool PointInPolygon(Point[] poly, Point p)
    {
        bool inside = false; int j = poly.Length - 1;
        for (int i = 0; i < poly.Length; i++)
        {
            bool intersect = ((poly[i].Y > p.Y) != (poly[j].Y > p.Y)) &&
                (p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (float)(poly[j].Y - poly[i].Y) + poly[i].X);
            if (intersect) inside = !inside; j = i;
        }
        return inside;
    }
}
