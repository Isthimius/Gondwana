using System.Drawing;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Coordinates;

/// <summary>
/// Hexagonal – Flat-Top layout (even-q vertical layout)
/// Bounding rectangle is (W x H). Centers advance by (0.75*W, H/2 per column parity)
/// </summary>
internal sealed class HexAxialFlatTopCoordinates : ISceneLayerCoordinates
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

        // The layout uses even-q offset coordinates. Interpolate the
        // staggered Y offset between the surrounding integer columns.
        int baseColumn = (int)MathF.Floor(gp.X);
        float columnProgress = gp.X - baseColumn;

        // Preserve the existing integer-division behavior for odd tile heights.
        float halfHeight = height / 2;
        float currentColumnOffsetY = (baseColumn & 1) == 0 ? 0f : halfHeight;
        float nextColumnOffsetY = ((baseColumn + 1) & 1) == 0 ? 0f : halfHeight;
        float offsetY = currentColumnOffsetY + ((nextColumnOffsetY - currentColumnOffsetY) * columnProgress);

        var origin = sceneLayer.OriginPx;
        float x = -origin.X + (gp.X * width * 0.75f);
        float y = -origin.Y + (gp.Y * height) + offsetY;

        return new Point((int)MathF.Floor(x), (int)MathF.Floor(y));
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

        float originX = sceneLayer.OriginPx.X;
        float originY = sceneLayer.OriginPx.Y;

        // --- 1) Coarse column estimate using centers, not box top-left ---

        // Center of column 0 is at originX + W/2
        float dxFromCol0Center = pixelPt.X - (-originX + W * 0.5f);

        // Columns advance by 0.75 * W horizontally
        float colF = dxFromCol0Center / (W * 0.75f);
        int approxCol = (int)Math.Round(colF);

        // --- 2) Coarse row estimate using centers and parity ---

        // Center Y of row 0 in this column:
        //   even col: originY + H/2
        //   odd  col: originY + H/2 + H/2 = originY + H
        float baseCenterY =
            -originY + H * 0.5f
            + ((approxCol & 1) == 0 ? 0f : H * 0.5f);

        float dyFromRow0Center = pixelPt.Y - baseCenterY;
        float rowF = dyFromRow0Center / H;
        int approxRow = (int)Math.Round(rowF);

        var best = new Point(approxCol, approxRow);
        float bestDist = float.MaxValue;

        // --- 3) Refine via polygon hit-test + nearest center ---

        foreach (var cand in NeighborsFlatTop(approxCol, approxRow, includeSelf: true))
        {
            var poly = HexPolygonFlatTop(sceneLayer, cand.X, cand.Y, includeOverhang: false);

            var pInt = new Point((int)Math.Round(pixelPt.X), (int)Math.Round(pixelPt.Y));
            if (PointInPolygon(poly, pInt))
                return new PointF(cand.X, cand.Y);

            // Center of candidate hex
            float cx = -originX + cand.X * (W * 0.75f) + W / 2f;
            float cy = -originY + cand.Y * (float)H
                       + ((cand.X & 1) == 0 ? 0 : H / 2) + H / 2f;

            float dx = cx - pixelPt.X;
            float dy = cy - pixelPt.Y;
            float d = dx * dx + dy * dy;

            if (d < bestDist)
            {
                bestDist = d;
                best = cand;
            }
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
        int W = sceneLayer.TileWidth;
        int H = sceneLayer.TileHeight;

        int originX = sceneLayer.OriginPx.X;
        int originY = sceneLayer.OriginPx.Y;

        int minCol = (int)Math.Floor((worldPixelRange.Left + originX) / (W * 0.75f)) - 2;
        int maxCol = (int)Math.Ceiling((worldPixelRange.Right + originX) / (W * 0.75f)) + 2;

        for (int col = minCol; col <= maxCol; col++)
        {
            int yOffset = ((col & 1) == 0 ? 0 : H / 2);
            int minRow = (int)Math.Floor((worldPixelRange.Top + originY - yOffset) / (float)H) - 2;
            int maxRow = (int)Math.Ceiling((worldPixelRange.Bottom + originY - yOffset) / (float)H) + 2;

            for (int row = minRow; row <= maxRow; row++)
            {
                var gp = sceneLayer[col, row];
                if (gp == null) continue;
                var r = GetPixelRangeForTile(gp, includeOverhang);
                if (r.IntersectsWith(worldPixelRange))
                    result.Add(gp);
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
        var rect = new Rectangle(p.X, p.Y, W, H); // hex image fits W x H box
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
    /// For flat-top hexagons, valid directions are E, W, NE, SE, NW, SW. N and S return null.
    /// </summary>
    /// <param name="gp">The source tile.</param>
    /// <param name="dir">The cardinal direction to search.</param>
    /// <returns>The adjacent tile in the specified direction, or null if none exists or direction is invalid.</returns>
    public SceneLayerTile GetAdjacentSceneLayerTile(SceneLayerTile gp, CardinalDirections dir)
    {
        int x = gp.GridCoordinatesAbs.X; int y = gp.GridCoordinatesAbs.Y;
        bool even = (x & 1) == 0;
        var m = gp.SceneLayer;
        return dir switch
        {
            CardinalDirections.E => m[x + 1, y],
            CardinalDirections.W => m[x - 1, y],
            CardinalDirections.NE => m[x + 1, y - (even ? 1 : 0)],
            CardinalDirections.SE => m[x + 1, y + (even ? 0 : 1)],
            CardinalDirections.NW => m[x - 1, y - (even ? 1 : 0)],
            CardinalDirections.SW => m[x - 1, y + (even ? 0 : 1)],
            CardinalDirections.N => null, // not a direct neighbor in hex grid
            CardinalDirections.S => null,
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
        return HexPolygonFlatTop(tile.SceneLayer, (int)tile.SceneLayerCoordinates.X, (int)tile.SceneLayerCoordinates.Y, includeOverhang);
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
        // wrap like square; hex maps are usually finite with wrap disabled, but keep parity sane
        float modX = valColRow.X % (xUpperBound + 1);
        float modY = valColRow.Y % (yUpperBound + 1);
        if (modX < 0) modX += xUpperBound + 1;
        if (modY < 0) modY += yUpperBound + 1;
        return new PointF(modX, modY);
    }

    #region helpers

    private static IEnumerable<Point> NeighborsFlatTop(int col, int row, bool includeSelf)
    {
        if (includeSelf) yield return new Point(col, row);
        bool even = (col & 1) == 0;
        yield return new Point(col + 1, row);
        yield return new Point(col - 1, row);
        yield return new Point(col + 1, row + (even ? 0 : 1));
        yield return new Point(col + 1, row - (even ? 1 : 0));
        yield return new Point(col - 1, row + (even ? 0 : 1));
        yield return new Point(col - 1, row - (even ? 1 : 0));
    }

    private static Point[] HexPolygonFlatTop(SceneLayer sceneLayer, int col, int row, bool includeOverhang)
    {
        int W = sceneLayer.TileWidth;
        int H = sceneLayer.TileHeight;

        int originX = sceneLayer.OriginPx.X;
        int originY = sceneLayer.OriginPx.Y;

        var p = new Point(
            -originX + (int)Math.Floor(col * (W * 0.75f)),
            -originY + row * H + ((col & 1) == 0 ? 0 : H / 2));

        var rect = new Rectangle(p.X, p.Y, W, H);
        var ohRect = TileBounds.ApplyOverhang(
            rect,
            includeOverhang ? new Spacing(0, 0, 0, 0) : Spacing.None,
            includeOverhang); // polygon uses base box; overhang impacts range, not shape

        int x = ohRect.X;
        int y = ohRect.Y;

        // Flat-top vertices from bounding box
        return new[]
        {
            new Point(x + W/4,    y),
            new Point(x + 3*W/4,  y),
            new Point(x + W,      y + H/2),
            new Point(x + 3*W/4,  y + H),
            new Point(x + W/4,    y + H),
            new Point(x,          y + H/2)
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

    #endregion helpers
}